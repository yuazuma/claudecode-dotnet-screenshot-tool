using System.IO.Compression;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// プロジェクトからの各種エクスポート操作を一元管理するサービス。
/// トレイメニューおよび ProjectViewWindow の双方から呼ばれる。
/// </summary>
public class ExportService
{
    private readonly ConfigStore _config;
    private readonly ProjectStore _projectStore;
    private readonly Notifier? _notifier;
    private readonly MarkdownManualWriter _mdWriter = new();
    private readonly DocxManualWriter _docxWriter = new();
    private readonly HtmlManualWriter _htmlWriter = new();
    private readonly VideoGenerator _videoGenerator;

    // エクスポート中の多重実行防止（プロジェクト ID → SemaphoreSlim）
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim>
        _exportLocks = new();

    public ExportService(ConfigStore config, ProjectStore projectStore,
        VideoGenerator videoGenerator, Notifier? notifier = null)
    {
        _config = config;
        _projectStore = projectStore;
        _videoGenerator = videoGenerator;
        _notifier = notifier;
    }

    // ---- 公開 API ----

    /// <summary>非削除ステップの画像を exports/images/ へコピーする（FR-PJ04）。</summary>
    public async Task ExportImagesAsync(ProjectInfo project,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        _notifier?.BeginProcessing();
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync(ct);
        try
        {
            var steps = ActiveSteps(project);
            if (steps.Count == 0) return;

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outDir = Path.Combine(project.ProjectFolder, "exports", $"{ts}_images");
            Directory.CreateDirectory(outDir);

            int n = 0;
            int imgTotal = steps.Count(s => s.AfterImagePath != null);
            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                if (step.AfterImagePath == null) continue;
                progress?.Report(new AutoScreenshot.Models.ExportProgress(
                    "画像を書き出し中...", ++n - 1 < 0 ? 0 : n, imgTotal, outDir));
                var (srcPath, isTemp) = ResolveAnnotatedImagePath(project, step);
                if (!File.Exists(srcPath)) { if (isTemp) try { File.Delete(srcPath); } catch { } continue; }
                string dest = Path.Combine(outDir, $"{++n:D3}_{Path.GetFileName(step.AfterImagePath)}");
                File.Copy(srcPath, dest, overwrite: true);
                if (isTemp) try { File.Delete(srcPath); } catch { }
            }

            // before 画像を before/ サブフォルダにコピー
            // before が未取得のステップは素の after 画像（アノテーション適用前）をフォールバックとして使用する
            bool anyBefore = steps.Any(s => s.BeforeImagePath != null || s.AfterImagePath != null);
            if (anyBefore)
            {
                string beforeOutDir = Path.Combine(outDir, "before");
                Directory.CreateDirectory(beforeOutDir);
                int nb = 0;
                foreach (var step in steps)
                {
                    // before が未取得の場合は素の after 画像をフォールバックとして使用
                    string? beforeRelPath = step.BeforeImagePath ?? step.AfterImagePath;
                    if (beforeRelPath == null) continue;
                    string beforeSrc = Path.Combine(project.ProjectFolder, beforeRelPath.Replace('/', '\\'));
                    if (!File.Exists(beforeSrc)) continue;
                    string beforeDest = Path.Combine(beforeOutDir, $"{++nb:D3}_{Path.GetFileName(beforeRelPath)}");
                    File.Copy(beforeSrc, beforeDest, overwrite: true);
                }
            }

            await RecordExport(project, ExportType.Images, outDir);
            OpenFolderIfEnabled(outDir);
            Log.Information("画像エクスポート完了: {Dir}", outDir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "画像エクスポートエラー");
            _notifier?.ShowError();
        }
        finally
        {
            sem.Release();
            _notifier?.EndProcessing();
        }
    }

    /// <summary>Markdown 手順書を exports/ へ生成する（FR-PJ05）。</summary>
    public async Task ExportMarkdownAsync(ProjectInfo project,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        _notifier?.BeginProcessing();
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            var (session, temps) = BuildAnnotatedSession(project);
            if (session.Steps.Count == 0) return;
            try
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string slug = MakeSlug(project.Title);
                string outPath = Path.Combine(project.ProjectFolder, "exports", $"{ts}_{slug}.md");

                var cfg = _config.Config.ManualGen;
                await _mdWriter.WriteAsync(session, outPath, cfg.ChapterTimeGapMinutes, cfg.TemplateMarkdownPath, progress, ct);

                await RecordExport(project, ExportType.Markdown, Path.Combine("exports", Path.GetFileName(outPath)));
                OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
                Log.Information("Markdown エクスポート完了: {Path}", outPath);
            }
            finally { CleanupTemps(temps); }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Markdown エクスポートエラー");
            _notifier?.ShowError();
        }
        finally
        {
            sem.Release();
            _notifier?.EndProcessing();
        }
    }

    /// <summary>Word 手順書を exports/ へ生成する（FR-PJ05）。</summary>
    public async Task ExportDocxAsync(ProjectInfo project,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        _notifier?.BeginProcessing();
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            var (session, temps) = BuildAnnotatedSession(project);
            if (session.Steps.Count == 0) return;
            try
            {
                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string slug = MakeSlug(project.Title);
                string outPath = Path.Combine(project.ProjectFolder, "exports", $"{ts}_{slug}.docx");

                var cfg = _config.Config.ManualGen;
                await _docxWriter.WriteAsync(session, outPath, cfg.ChapterTimeGapMinutes, cfg.TemplateDotxPath, progress, ct);

                await RecordExport(project, ExportType.Docx, Path.Combine("exports", Path.GetFileName(outPath)));
                OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
                Log.Information("Word エクスポート完了: {Path}", outPath);
            }
            finally { CleanupTemps(temps); }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Word エクスポートエラー");
            _notifier?.ShowError();
        }
        finally
        {
            sem.Release();
            _notifier?.EndProcessing();
        }
    }

    /// <summary>HTML 手順書を exports/ へ生成する（FR-A）。</summary>
    public async Task ExportHtmlAsync(ProjectInfo project,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        _notifier?.BeginProcessing();
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            var steps = ActiveSteps(project);
            if (steps.Count == 0) return;

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string slug = MakeSlug(project.Title);
            string outPath = Path.Combine(project.ProjectFolder, "exports", $"{ts}_{slug}.html");

            await _htmlWriter.WriteAsync(project, outPath, progress, ct);

            await RecordExport(project, ExportType.Html, Path.Combine("exports", Path.GetFileName(outPath)));
            OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
            Log.Information("HTML エクスポート完了: {Path}", outPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "HTML エクスポートエラー");
            _notifier?.ShowError();
        }
        finally
        {
            sem.Release();
            _notifier?.EndProcessing();
        }
    }

    /// <summary>動画を exports/ へ生成する（FR-PJ06）。バックグラウンド実行。</summary>
    public async Task ExportVideoAsync(ProjectInfo project,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        _notifier?.BeginProcessing();
        var (session, temps) = BuildAnnotatedSession(project);
        if (session.Steps.Count == 0) { CleanupTemps(temps); _notifier?.EndProcessing(); return; }
        try
        {
            await _videoGenerator.GenerateAsync(session, progress, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "動画エクスポートエラー");
            _notifier?.ShowError();
        }
        finally
        {
            CleanupTemps(temps);
            _notifier?.EndProcessing();
        }
    }

    /// <summary>プロジェクト（images/ + thumbs/ + project.json）を ZIP に圧縮する（FR-PJ07）。</summary>
    public async Task ExportZipAsync(ProjectInfo project, string destZipPath,
        IProgress<AutoScreenshot.Models.ExportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            int zipN = 0;
            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(destZipPath, ZipArchiveMode.Create);
                AddFolderToZip(zip, project.ProjectFolder, "project.json");
                AddDirectoryToZip(zip, Path.Combine(project.ProjectFolder, "images"), "images",
                    f => { zipN++; progress?.Report(new AutoScreenshot.Models.ExportProgress("ZIP を作成中...", zipN, 0, destZipPath)); ct.ThrowIfCancellationRequested(); });
                AddDirectoryToZip(zip, Path.Combine(project.ProjectFolder, "thumbs"), "thumbs",
                    f => { zipN++; progress?.Report(new AutoScreenshot.Models.ExportProgress("ZIP を作成中...", zipN, 0, destZipPath)); });
            }, ct);

            Log.Information("ZIP エクスポート完了: {Path}", destZipPath);
        }
        finally { sem.Release(); }
    }

    // ---- ヘルパー ----

    private static List<ProjectStep> ActiveSteps(ProjectInfo project)
        => project.Steps.Where(s => !s.IsDeleted).ToList();

    /// <summary>
    /// アノテーションがある場合は焼き込んだ画像を一時ファイルに書き出してパスを返す。
    /// ない場合は元パスをそのまま返す。使用後は isTemp=true なら削除すること。
    /// </summary>
    private static (string path, bool isTemp) ResolveAnnotatedImagePath(ProjectInfo project, ProjectStep step)
    {
        if (step.AfterImagePath == null) return (string.Empty, false);
        string srcPath = Path.Combine(project.ProjectFolder, step.AfterImagePath.Replace('/', '\\'));

        if (step.Annotations == null || step.Annotations.Count == 0)
            return (srcPath, false);

        using var bmp = AnnotationRenderer.Render(srcPath, step.Annotations);
        if (bmp == null) return (srcPath, false);

        string tmp = Path.Combine(Path.GetTempPath(), $"ascproj_ann_{Guid.NewGuid():N}.png");
        bmp.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
        return (tmp, true);
    }

    /// <summary>アノテーション焼き込み済み ManualSession を構築する。アノテーションがあるステップは一時 PNG を生成する。</summary>
    private (ManualSession session, List<string> temps) BuildAnnotatedSession(ProjectInfo project)
    {
        var temps = new List<string>();
        var session = new ManualSession
        {
            Title   = project.Title,
            EndedAt = project.EndedAt?.LocalDateTime,
            Digest  = project.Digest,
        };

        foreach (var ps in ActiveSteps(project))
        {
            // after 画像（アノテーション焼き込み対象）
            string? rawAfterPath = ps.AfterImagePath != null
                ? Path.Combine(project.ProjectFolder, ps.AfterImagePath.Replace('/', '\\'))
                : null;

            string? afterImagePath = rawAfterPath;
            if (afterImagePath != null && ps.Annotations?.Count > 0)
            {
                using var bmp = AnnotationRenderer.Render(afterImagePath, ps.Annotations);
                if (bmp != null)
                {
                    string tmp = Path.Combine(Path.GetTempPath(), $"ascann_{Guid.NewGuid():N}.png");
                    bmp.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                    temps.Add(tmp);
                    afterImagePath = tmp;
                }
            }

            // before 画像（証跡・アノテーション付与しない）
            // before が未取得の場合は素の after 画像（アノテーション適用前）をフォールバックとして使用する
            string? beforeImagePath;
            if (ps.BeforeImagePath != null)
                beforeImagePath = Path.Combine(project.ProjectFolder, ps.BeforeImagePath.Replace('/', '\\'));
            else
                beforeImagePath = rawAfterPath;  // フォールバック

            session.Steps.Add(new ManualStep
            {
                StepNumber           = ps.StepNumber,
                Timestamp            = ps.Timestamp.LocalDateTime,
                TriggerType          = Enum.TryParse<TriggerType>(ps.TriggerType, out var tt) ? tt : TriggerType.MouseLeftClick,
                UiElementName        = ps.UiElementName,
                UiControlType        = ps.UiControlType,
                CursorPosition       = new System.Drawing.Point(ps.CursorX, ps.CursorY),
                WindowTitle          = ps.WindowTitle,
                ProcessName          = ps.ProcessName,
                InputText            = ps.InputText,
                KeyCodes             = ps.KeyCodes,
                AfterImagePath       = afterImagePath,
                BeforeImagePath      = beforeImagePath,
                DescriptionRuleBased = ps.DescriptionRuleBased,
                DescriptionLlm       = ps.DescriptionOverride ?? ps.DescriptionLlm,
                NeedsReview          = ps.NeedsReview,
            });
        }
        return (session, temps);
    }

    private static void CleanupTemps(List<string> temps)
    {
        foreach (var t in temps) try { File.Delete(t); } catch { }
    }

    private async Task RecordExport(ProjectInfo project, ExportType type, string relPath)
    {
        await _projectStore.RecordExportAsync(project, new ExportRecord
        {
            Type = type.ToString(),
            OutputPath = relPath,
        });
    }

    private void OpenFolderIfEnabled(string folder)
    {
        if (_config.Config.Project.OpenFolderOnExportComplete)
            System.Diagnostics.Process.Start("explorer.exe", folder);
    }

    private SemaphoreSlim GetLock(Guid projectId)
        => _exportLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));

    private static string MakeSlug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else sb.Append('_');
        }
        string s = sb.ToString().Trim('_');
        return s.Length > 40 ? s[..40] : (s.Length == 0 ? "export" : s);
    }

    private static void AddFolderToZip(ZipArchive zip, string folder, string entryName)
    {
        string path = Path.Combine(folder, entryName);
        if (File.Exists(path)) zip.CreateEntryFromFile(path, entryName);
    }

    private static void AddDirectoryToZip(ZipArchive zip, string dirPath, string entryPrefix,
        Action<string>? onEach = null)
    {
        if (!Directory.Exists(dirPath)) return;
        foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            string entry = $"{entryPrefix}/{rel}";
            zip.CreateEntryFromFile(file, entry);
            onEach?.Invoke(file);
        }
    }
}
