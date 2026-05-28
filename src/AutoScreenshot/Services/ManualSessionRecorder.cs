using System.Collections.Concurrent;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>操作手順書のセッション記録・生成を管理するサービス</summary>
public class ManualSessionRecorder
{
    private readonly ConfigStore _config;
    private readonly UiaService _uia;
    private readonly OcrService _ocr;
    private readonly Notifier? _notifier;
    private readonly MarkdownManualWriter _mdWriter   = new();
    private readonly DocxManualWriter     _docxWriter = new();
    private VideoGenerator? _videoGenerator;

    // プロジェクト機能
    private readonly ProjectStore? _projectStore;
    private ProjectInfo? _currentProject;

    // インクリメンタル LLM キュー（FR-B）
    private readonly ConcurrentQueue<(ProjectInfo project, ProjectStep step)> _llmQueue = new();
    private readonly SemaphoreSlim _llmSemaphore = new(1, 1);
    private readonly CancellationTokenSource _llmCts = new();
    private Task? _llmWorkerTask;

    private ManualSession? _current;
    private readonly object _lock = new();

    public ManualSessionRecorder(ConfigStore config, UiaService uia, OcrService ocr,
        Notifier? notifier = null, ProjectStore? projectStore = null)
    {
        _config       = config;
        _uia          = uia;
        _ocr          = ocr;
        _notifier     = notifier;
        _projectStore = projectStore;
    }

    /// <summary>現在のプロジェクト情報を返す（プロジェクト機能が無効な場合は null）。</summary>
    public ProjectInfo? CurrentProject { get { lock (_lock) { return _currentProject; } } }

    /// <summary>VideoGenerator を設定する（NotifyIconWrapper から呼ぶ）。</summary>
    public void SetVideoGenerator(VideoGenerator generator) => _videoGenerator = generator;

    /// <summary>新しいセッションを開始する。title が空なら既定タイトルを使用する。</summary>
    public void StartSession(string title = "")
    {
        string resolvedTitle = string.IsNullOrWhiteSpace(title)
            ? $"操作手順書 {DateTime.Now:yyyy-MM-dd HH:mm}"
            : title;

        lock (_lock)
        {
            _current = new ManualSession { Title = resolvedTitle };
            Log.Information("手順書セッション開始: {Title} ({Id})", _current.Title, _current.SessionId);
        }

        // プロジェクト機能が有効な場合はプロジェクトフォルダを非同期作成
        if (_projectStore != null && _config.Config.Project.Enabled)
        {
            Task.Run(async () =>
            {
                try
                {
                    var project = await _projectStore.CreateProjectAsync(resolvedTitle);
                    lock (_lock) { _currentProject = project; }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "プロジェクト作成失敗");
                }
            });
        }
    }

    /// <summary>TriggerOrchestrator から呼ばれる。イベントとスクリーンショットパスを記録する。</summary>
    public async Task RecordStepAsync(TriggerEvent evt, string? imagePath, System.Drawing.Rectangle monitorBounds)
    {
        var cfg = _config.Config.ManualGen;
        if (!cfg.Enabled) return;

        if (evt.Type is TriggerType.ScreenDiff or TriggerType.ManualCapture) return;

        lock (_lock) { if (_current == null) return; }

        string? uiName = null;
        string? ctrlType = null;
        bool needsReview = false;

        if (evt.Type != TriggerType.ActiveWindowChange)
        {
            (uiName, ctrlType) = evt.Type == TriggerType.Keyboard
                ? await _uia.GetFocusedElementAsync()
                : await _uia.GetElementAtAsync(evt.CursorPosition);

            if (uiName == null && imagePath != null)
                uiName = await _ocr.RecognizeNearbyTextAsync(imagePath, evt.CursorPosition, monitorBounds);

            needsReview = uiName == null;
        }

        ManualStep? step = null;
        lock (_lock)
        {
            if (_current == null) return;

            bool includeImage = cfg.ScreenshotMode switch
            {
                ScreenshotMode.All          => true,
                ScreenshotMode.WindowChange => evt.Type == TriggerType.ActiveWindowChange,
                ScreenshotMode.None         => false,
                _                           => false,
            };

            string? inputText = null;
            string? keyCodes  = null;
            if (evt.Type == TriggerType.Keyboard)
            {
                inputText = cfg.KeyboardMode switch
                {
                    KeyboardMode.RealText => evt.InputText,
                    KeyboardMode.Both     => evt.InputText,
                    _                    => null,
                };
                keyCodes = cfg.KeyboardMode switch
                {
                    KeyboardMode.KeyCode => evt.KeyCodes,
                    KeyboardMode.Both    => evt.KeyCodes,
                    _                   => null,
                };
            }

            step = new ManualStep
            {
                StepNumber     = _current.Steps.Count + 1,
                Timestamp      = evt.Timestamp,
                TriggerType    = evt.Type,
                UiElementName  = uiName,
                UiControlType  = ctrlType,
                CursorPosition = evt.CursorPosition,
                WindowTitle    = evt.ActiveWindowTitle,
                ProcessName    = evt.ActiveProcessName,
                ImagePath      = includeImage ? imagePath : null,
                NeedsReview    = needsReview,
                InputText      = inputText,
                KeyCodes       = keyCodes,
            };
            step.DescriptionRuleBased = RuleBasedDescriber.Describe(step);
            _current.Steps.Add(step);
        }

        // プロジェクト機能: project.json 追記 + サムネイル生成（非同期・撮影をブロックしない）
        if (_projectStore != null && _config.Config.Project.Enabled && step != null)
        {
            _ = RecordProjectStepAsync(step, imagePath);
        }
    }

    private async Task RecordProjectStepAsync(ManualStep step, string? imagePath)
    {
        ProjectInfo? project;
        lock (_lock) { project = _currentProject; }
        if (project == null) return;

        string? thumbRelPath = null;
        if (imagePath != null && File.Exists(imagePath))
        {
            string thumbFileName = $"step_{step.StepNumber:D3}.jpg";
            string thumbPath = Path.Combine(project.ProjectFolder, "thumbs", thumbFileName);
            thumbRelPath = $"thumbs/{thumbFileName}";
            int maxWidth = _config.Config.Project.ThumbnailMaxWidth;
            _ = ThumbnailService.GenerateAsync(imagePath, thumbPath, maxWidth);
        }

        // images/ 配下への相対パス
        string? imageRelPath = imagePath != null
            ? Path.GetRelativePath(project.ProjectFolder, imagePath).Replace('\\', '/')
            : null;

        var pStep = new ProjectStep
        {
            StepNumber           = step.StepNumber,
            Timestamp            = new DateTimeOffset(step.Timestamp),
            TriggerType          = step.TriggerType.ToString(),
            UiElementName        = step.UiElementName,
            UiControlType        = step.UiControlType,
            CursorX              = step.CursorPosition.X,
            CursorY              = step.CursorPosition.Y,
            WindowTitle          = step.WindowTitle,
            ProcessName          = step.ProcessName,
            InputText            = step.InputText,
            KeyCodes             = step.KeyCodes,
            ImagePath            = imageRelPath,
            ThumbPath            = thumbRelPath,
            DescriptionRuleBased = step.DescriptionRuleBased,
            DescriptionLlm       = step.DescriptionLlm,
            NeedsReview          = step.NeedsReview,
        };

        try
        {
            await _projectStore!.AppendStepAsync(project, pStep);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "project.json ステップ追記失敗");
            return;
        }

        // インクリメンタル LLM: LLM 有効かつ IncrementalLlm オン の場合はキューに積む（FR-B）
        var cfg = _config.Config;
        if (cfg.Project.IncrementalLlm &&
            cfg.ManualGen.LlmEnabled &&
            !string.IsNullOrWhiteSpace(cfg.ManualGen.LlmEndpoint) &&
            !string.IsNullOrWhiteSpace(cfg.ManualGen.LlmApiKey))
        {
            _llmQueue.Enqueue((project, pStep));
            EnsureLlmWorkerRunning();
        }
    }

    private void EnsureLlmWorkerRunning()
    {
        if (_llmWorkerTask is { IsCompleted: false }) return;
        _llmWorkerTask = Task.Run(RunLlmQueueAsync);
    }

    private async Task RunLlmQueueAsync()
    {
        var cfg = _config.Config.ManualGen;
        string endpoint, apiKey;
        try
        {
            endpoint = DpapiHelper.Unprotect(cfg.LlmEndpoint!);
            apiKey   = DpapiHelper.Unprotect(cfg.LlmApiKey!);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "インクリメンタル LLM: 認証情報の復号失敗");
            return;
        }

        var llm = new LlmService(endpoint, apiKey, cfg.LlmDeploymentName);

        while (_llmQueue.TryDequeue(out var item) && !_llmCts.Token.IsCancellationRequested)
        {
            await _llmSemaphore.WaitAsync(_llmCts.Token).ConfigureAwait(false);
            try
            {
                var (project, pStep) = item;
                string? improved = await llm.ImproveStepDescriptionAsync(
                    pStep.TriggerType, pStep.UiElementName, pStep.WindowTitle,
                    pStep.DescriptionRuleBased, _llmCts.Token);

                if (improved != null)
                {
                    pStep.DescriptionLlm = improved;
                    try { await _projectStore!.WriteProjectJsonAsync(project); }
                    catch (Exception ex) { Log.Warning(ex, "インクリメンタル LLM: project.json 更新失敗"); }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log.Warning(ex, "インクリメンタル LLM: ステップ処理失敗"); }
            finally { _llmSemaphore.Release(); }
        }
    }

    /// <summary>LLM キューが空になるまで最大 maxWaitMs 待機する（セッション終了時用）。</summary>
    private async Task DrainLlmQueueAsync(int maxWaitMs = 60_000)
    {
        if (_llmQueue.IsEmpty) return;
        Log.Information("インクリメンタル LLM キューをドレイン中（最大 {Max}s）...", maxWaitMs / 1000);

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (!_llmQueue.IsEmpty && DateTime.UtcNow < deadline)
            await Task.Delay(500).ConfigureAwait(false);

        if (!_llmQueue.IsEmpty)
            Log.Warning("インクリメンタル LLM キュードレイン タイムアウト（未処理 {N} 件）", _llmQueue.Count);
    }

    /// <summary>現セッションを保存して完了させ、新しいセッションを開始する（手動区切り）。</summary>
    public void SplitSession(string nextTitle = "")
    {
        Task.Run(async () =>
        {
            ManualSession? completed;
            ProjectInfo? completedProject;
            lock (_lock)
            {
                completed = _current;
                completedProject = _currentProject;
                _current = null;
                _currentProject = null;
            }
            if (completed != null)
            {
                completed.EndedAt = DateTime.Now;
                if (completedProject != null)
                {
                    completedProject.EndedAt = DateTimeOffset.Now;
                    try { await _projectStore!.WriteProjectJsonAsync(completedProject); }
                    catch (Exception ex) { Log.Warning(ex, "プロジェクト終了時の project.json 更新失敗"); }
                }
                await WriteSessionAsync(completed, completedProject);
            }
            StartSession(nextTitle);
        });
    }

    /// <summary>現セッションのスナップショットから動画を手動生成する。</summary>
    public void GenerateVideoNow(VideoGenerator generator)
    {
        ManualSession? snapshot;
        lock (_lock)
        {
            if (_current == null) return;
            snapshot = new ManualSession { Title = _current.Title, EndedAt = DateTime.Now };
            snapshot.Steps.AddRange(_current.Steps);
        }
        _ = generator.GenerateAsync(snapshot);
    }

    /// <summary>現セッションのスナップショットを保存する（セッションは継続）。</summary>
    public void GenerateNow()
    {
        Task.Run(async () =>
        {
            ManualSession? snapshot;
            ProjectInfo? projectSnapshot;
            lock (_lock)
            {
                if (_current == null) return;
                snapshot = new ManualSession { Title = _current.Title, EndedAt = DateTime.Now };
                snapshot.Steps.AddRange(_current.Steps);
                projectSnapshot = _currentProject;
            }
            await WriteSessionAsync(snapshot, projectSnapshot);
        });
    }

    /// <summary>AutoScreenshot 終了時に呼ばれる。</summary>
    public async Task StopSessionAsync()
    {
        ManualSession? completed;
        ProjectInfo? completedProject;
        lock (_lock)
        {
            completed = _current;
            completedProject = _currentProject;
            _current = null;
            _currentProject = null;
        }
        if (completed != null)
        {
            completed.EndedAt = DateTime.Now;
            if (completedProject != null)
            {
                completedProject.EndedAt = DateTimeOffset.Now;
                try { await _projectStore!.WriteProjectJsonAsync(completedProject); }
                catch (Exception ex) { Log.Warning(ex, "プロジェクト終了時の project.json 更新失敗"); }
            }
            await WriteSessionAsync(completed, completedProject);
        }

        // インクリメンタル LLM ワーカーをキャンセルして完了を待つ
        _llmCts.Cancel();
        if (_llmWorkerTask != null)
        {
            try { await _llmWorkerTask.ConfigureAwait(false); }
            catch { /* OperationCanceledException は無視 */ }
        }
    }

    private async Task WriteSessionAsync(ManualSession session, ProjectInfo? project)
    {
        var cfg = _config.Config.ManualGen;
        var projCfg = _config.Config.Project;

        // プロジェクト機能が有効な場合、AutoExport 設定に従って出力
        bool mdEnabled    = projCfg.Enabled ? projCfg.AutoExportMarkdown : cfg.OutputMarkdown;
        bool docxEnabled  = projCfg.Enabled ? projCfg.AutoExportDocx     : cfg.OutputDocx;
        bool videoEnabled = projCfg.Enabled ? projCfg.AutoExportVideo    : false;
        bool htmlEnabled  = projCfg.Enabled && projCfg.AutoExportHtml;

        if (!cfg.Enabled) return;
        if (session.Steps.Count == 0)
        {
            Log.Debug("手順書: ステップ0件のためスキップ");
            return;
        }

        // LLM 連携（インクリメンタル LLM が有効な場合はキュードレインのみ、無効の場合は一括処理）
        bool llmUsed = false;
        bool incrementalActive = projCfg.Enabled && projCfg.IncrementalLlm;
        if (cfg.LlmEnabled &&
            !string.IsNullOrWhiteSpace(cfg.LlmEndpoint) &&
            !string.IsNullOrWhiteSpace(cfg.LlmApiKey))
        {
            if (incrementalActive)
            {
                // インクリメンタル LLM モード: キューが空になるまで待機
                await DrainLlmQueueAsync();
                // LLM 結果が既に各ステップに反映済みなので project.json から session に同期
                if (project != null)
                {
                    foreach (var ps in project.Steps)
                    {
                        var ms = session.Steps.FirstOrDefault(s => s.StepNumber == ps.StepNumber);
                        if (ms != null && ps.DescriptionLlm != null)
                            ms.DescriptionLlm = ps.DescriptionLlm;
                    }
                    session.Digest = project.Digest;
                }
                llmUsed = project?.Steps.Any(s => s.DescriptionLlm != null) == true;
            }
            else
            {
                // 一括 LLM モード（従来動作）
                try
                {
                    string endpoint = DpapiHelper.Unprotect(cfg.LlmEndpoint);
                    string apiKey   = DpapiHelper.Unprotect(cfg.LlmApiKey);
                    if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
                    {
                        var llm = new LlmService(endpoint, apiKey, cfg.LlmDeploymentName);
                        Log.Information("LLM 操作テキスト改善を開始...");
                        await llm.ImproveDescriptionsAsync(session);
                        session.Digest = await llm.GenerateDigestAsync(session);
                        llmUsed = true;

                        if (project != null && _projectStore != null)
                        {
                            SyncLlmResultsToProject(session, project);
                            try { await _projectStore.WriteProjectJsonAsync(project); }
                            catch (Exception ex) { Log.Warning(ex, "LLM 結果の project.json 反映失敗"); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "LLM 処理中にエラーが発生しました。ルールベースで続行します。");
                }
            }
        }

        // 出力先フォルダ
        string folder;
        if (projCfg.Enabled && project != null)
            folder = Path.Combine(project.ProjectFolder, "exports");
        else
            folder = string.IsNullOrWhiteSpace(cfg.OutputFolder)
                ? Path.Combine(_config.Config.Storage.SaveFolder, "manuals")
                : cfg.OutputFolder;

        string slug = MakeSlug(session.Title);
        string fileBase = $"{session.StartedAt:yyyyMMdd_HHmmss}_{slug}";

        try
        {
            int gap = cfg.ChapterTimeGapMinutes;
            if (mdEnabled)
                await _mdWriter.WriteAsync(session, Path.Combine(folder, fileBase + ".md"), gap,
                    cfg.TemplateMarkdownPath);
            if (docxEnabled)
                await _docxWriter.WriteAsync(session, Path.Combine(folder, fileBase + ".docx"), gap,
                    cfg.TemplateDotxPath);
            if (htmlEnabled && project != null)
                await new HtmlManualWriter().WriteAsync(project, Path.Combine(folder, fileBase + ".html"));

            _notifier?.ShowManualGeneratedToast(llmUsed);

            if (projCfg.Enabled && project != null)
            {
                // エクスポート記録
                if (mdEnabled && _projectStore != null)
                    await _projectStore.RecordExportAsync(project, new Models.ExportRecord
                    {
                        Type = ExportType.Markdown.ToString(),
                        OutputPath = Path.Combine("exports", fileBase + ".md"),
                    });
                if (htmlEnabled && _projectStore != null)
                    await _projectStore.RecordExportAsync(project, new Models.ExportRecord
                    {
                        Type = ExportType.Html.ToString(),
                        OutputPath = Path.Combine("exports", fileBase + ".html"),
                    });

                // エクスポート完了時にフォルダを開く
                if (projCfg.OpenFolderOnExportComplete)
                    System.Diagnostics.Process.Start("explorer.exe", folder);
            }

            // 動画自動生成（v1.1.0 の VideoGen.AutoGenerateWithManual または v1.2.0 AutoExportVideo）
            bool autoVideo = projCfg.Enabled ? videoEnabled : _config.Config.VideoGen.AutoGenerateWithManual;
            if (autoVideo && _videoGenerator != null)
                _ = _videoGenerator.GenerateAsync(session);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手順書書き出し失敗");
        }
    }

    private static void SyncLlmResultsToProject(ManualSession session, ProjectInfo project)
    {
        project.Digest = session.Digest;
        foreach (var step in project.Steps)
        {
            var match = session.Steps.FirstOrDefault(s => s.StepNumber == step.StepNumber);
            if (match?.DescriptionLlm != null)
                step.DescriptionLlm = match.DescriptionLlm;
        }
    }

    private static string MakeSlug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else if (c == ' ' || c == '-') sb.Append('_');
        }
        string s = sb.ToString().TrimStart('_');
        return s.Length > 40 ? s[..40] : s;
    }
}
