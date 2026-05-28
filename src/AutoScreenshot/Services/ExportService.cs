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
    public async Task ExportImagesAsync(ProjectInfo project)
    {
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            var steps = ActiveSteps(project);
            if (steps.Count == 0) return;

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outDir = Path.Combine(project.ProjectFolder, "exports", $"{ts}_images");
            Directory.CreateDirectory(outDir);

            int n = 0;
            foreach (var step in steps)
            {
                if (step.ImagePath == null) continue;
                var (srcPath, isTemp) = ResolveAnnotatedImagePath(project, step);
                if (!File.Exists(srcPath)) { if (isTemp) try { File.Delete(srcPath); } catch { } continue; }
                string dest = Path.Combine(outDir, $"{++n:D3}_{Path.GetFileName(step.ImagePath)}");
                File.Copy(srcPath, dest, overwrite: true);
                if (isTemp) try { File.Delete(srcPath); } catch { }
            }

            await RecordExport(project, ExportType.Images, outDir);
            OpenFolderIfEnabled(outDir);
            Log.Information("画像エクスポート完了: {Dir}", outDir);
        }
        finally { sem.Release(); }
    }

    /// <summary>Markdown 手順書を exports/ へ生成する（FR-PJ05）。</summary>
    public async Task ExportMarkdownAsync(ProjectInfo project)
    {
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
                await _mdWriter.WriteAsync(session, outPath, cfg.ChapterTimeGapMinutes, cfg.TemplateMarkdownPath);

                await RecordExport(project, ExportType.Markdown, Path.Combine("exports", Path.GetFileName(outPath)));
                OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
                Log.Information("Markdown エクスポート完了: {Path}", outPath);
            }
            finally { CleanupTemps(temps); }
        }
        finally { sem.Release(); }
    }

    /// <summary>Word 手順書を exports/ へ生成する（FR-PJ05）。</summary>
    public async Task ExportDocxAsync(ProjectInfo project)
    {
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
                await _docxWriter.WriteAsync(session, outPath, cfg.ChapterTimeGapMinutes, cfg.TemplateDotxPath);

                await RecordExport(project, ExportType.Docx, Path.Combine("exports", Path.GetFileName(outPath)));
                OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
                Log.Information("Word エクスポート完了: {Path}", outPath);
            }
            finally { CleanupTemps(temps); }
        }
        finally { sem.Release(); }
    }

    /// <summary>HTML 手順書を exports/ へ生成する（FR-A）。</summary>
    public async Task ExportHtmlAsync(ProjectInfo project)
    {
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            var steps = ActiveSteps(project);
            if (steps.Count == 0) return;

            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string slug = MakeSlug(project.Title);
            string outPath = Path.Combine(project.ProjectFolder, "exports", $"{ts}_{slug}.html");

            await _htmlWriter.WriteAsync(project, outPath);

            await RecordExport(project, ExportType.Html, Path.Combine("exports", Path.GetFileName(outPath)));
            OpenFolderIfEnabled(Path.GetDirectoryName(outPath)!);
            Log.Information("HTML エクスポート完了: {Path}", outPath);
        }
        finally { sem.Release(); }
    }

    /// <summary>動画を exports/ へ生成する（FR-PJ06）。バックグラウンド実行。</summary>
    public async Task ExportVideoAsync(ProjectInfo project)
    {
        var (session, temps) = BuildAnnotatedSession(project);
        if (session.Steps.Count == 0) { CleanupTemps(temps); return; }
        try { await _videoGenerator.GenerateAsync(session); }
        finally { CleanupTemps(temps); }
    }

    /// <summary>プロジェクト（images/ + thumbs/ + project.json）を ZIP に圧縮する（FR-PJ07）。</summary>
    public async Task ExportZipAsync(ProjectInfo project, string destZipPath)
    {
        var sem = GetLock(project.ProjectId);
        await sem.WaitAsync();
        try
        {
            await Task.Run(() =>
            {
                using var zip = ZipFile.Open(destZipPath, ZipArchiveMode.Create);
                AddFolderToZip(zip, project.ProjectFolder, "project.json");
                AddDirectoryToZip(zip, Path.Combine(project.ProjectFolder, "images"), "images");
                AddDirectoryToZip(zip, Path.Combine(project.ProjectFolder, "thumbs"), "thumbs");
            });

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
        if (step.ImagePath == null) return ("", false);
        string srcPath = Path.Combine(project.ProjectFolder, step.ImagePath.Replace('/', '\\'));

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
            string? imagePath = ps.ImagePath != null
                ? Path.Combine(project.ProjectFolder, ps.ImagePath.Replace('/', '\\'))
                : null;

            if (imagePath != null && ps.Annotations?.Count > 0)
            {
                using var bmp = AnnotationRenderer.Render(imagePath, ps.Annotations);
                if (bmp != null)
                {
                    string tmp = Path.Combine(Path.GetTempPath(), $"ascann_{Guid.NewGuid():N}.png");
                    bmp.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);
                    temps.Add(tmp);
                    imagePath = tmp;
                }
            }

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
                ImagePath            = imagePath,
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

    /// <summary>ProjectInfo から ManualSession を構築する（既存 Writer への橋渡し）。</summary>
    private ManualSession BuildSession(ProjectInfo project)
    {
        var session = new ManualSession
        {
            Title    = project.Title,
            EndedAt  = project.EndedAt?.LocalDateTime,
            Digest   = project.Digest,
        };

        foreach (var ps in ActiveSteps(project))
        {
            // 画像パスをフルパスに変換
            string? imagePath = ps.ImagePath != null
                ? Path.Combine(project.ProjectFolder, ps.ImagePath.Replace('/', '\\'))
                : null;

            var step = new ManualStep
            {
                StepNumber         = ps.StepNumber,
                Timestamp          = ps.Timestamp.LocalDateTime,
                TriggerType        = Enum.TryParse<TriggerType>(ps.TriggerType, out var tt) ? tt : TriggerType.MouseLeftClick,
                UiElementName      = ps.UiElementName,
                UiControlType      = ps.UiControlType,
                CursorPosition     = new System.Drawing.Point(ps.CursorX, ps.CursorY),
                WindowTitle        = ps.WindowTitle,
                ProcessName        = ps.ProcessName,
                InputText          = ps.InputText,
                KeyCodes           = ps.KeyCodes,
                ImagePath          = imagePath,
                DescriptionRuleBased = ps.DescriptionRuleBased,
                DescriptionLlm     = ps.DescriptionOverride ?? ps.DescriptionLlm,
                NeedsReview        = ps.NeedsReview,
            };
            session.Steps.Add(step);
        }
        return session;
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

    private static void AddDirectoryToZip(ZipArchive zip, string dirPath, string entryPrefix)
    {
        if (!Directory.Exists(dirPath)) return;
        foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(dirPath, file).Replace('\\', '/');
            string entry = $"{entryPrefix}/{rel}";
            zip.CreateEntryFromFile(file, entry);
        }
    }
}
