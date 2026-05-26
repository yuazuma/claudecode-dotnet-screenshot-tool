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

    private ManualSession? _current;
    private readonly object _lock = new();

    public ManualSessionRecorder(ConfigStore config, UiaService uia, OcrService ocr, Notifier? notifier = null)
    {
        _config   = config;
        _uia      = uia;
        _ocr      = ocr;
        _notifier = notifier;
    }

    /// <summary>新しいセッションを開始する。title が空なら既定タイトルを使用する。</summary>
    public void StartSession(string title = "")
    {
        lock (_lock)
        {
            _current = new ManualSession
            {
                Title = string.IsNullOrWhiteSpace(title)
                    ? $"操作手順書 {DateTime.Now:yyyy-MM-dd HH:mm}"
                    : title,
            };
            Log.Information("手順書セッション開始: {Title} ({Id})", _current.Title, _current.SessionId);
        }
    }

    /// <summary>TriggerOrchestrator から呼ばれる。イベントとスクリーンショットパスを記録する。</summary>
    public async Task RecordStepAsync(TriggerEvent evt, string? imagePath, System.Drawing.Rectangle monitorBounds)
    {
        var cfg = _config.Config.ManualGen;
        if (!cfg.Enabled) return;

        // E-05: 差分検知と手動撮影は記録しない
        if (evt.Type is TriggerType.ScreenDiff or TriggerType.ManualCapture) return;

        lock (_lock) { if (_current == null) return; }

        // UIA / OCR は lock 外で非同期実行 (U-01〜U-04)
        string? uiName = null;
        string? ctrlType = null;
        bool needsReview = false;

        if (evt.Type != TriggerType.ActiveWindowChange)
        {
            // キーボードはフォーカス要素、それ以外はカーソル位置の要素
            (uiName, ctrlType) = evt.Type == TriggerType.Keyboard
                ? await _uia.GetFocusedElementAsync()
                : await _uia.GetElementAtAsync(evt.CursorPosition);

            // UIA 失敗 → OCR フォールバック (U-02)
            if (uiName == null && imagePath != null)
                uiName = await _ocr.RecognizeNearbyTextAsync(imagePath, evt.CursorPosition, monitorBounds);

            // 両方失敗 → 要レビューマーク (U-03)
            needsReview = uiName == null;
        }

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

            // E-04: KeyboardMode に基づいてキー入力テキストを設定
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

            var step = new ManualStep
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
    }

    /// <summary>現セッションを保存して完了させ、新しいセッションを開始する（手動区切り）。</summary>
    public void SplitSession(string nextTitle = "")
    {
        Task.Run(async () =>
        {
            ManualSession? completed;
            lock (_lock)
            {
                completed = _current;
                _current = null;
            }
            if (completed != null)
            {
                completed.EndedAt = DateTime.Now;
                await WriteSessionAsync(completed);
            }
            StartSession(nextTitle);
        });
    }

    /// <summary>現セッションのスナップショットを保存する（セッションは継続）。</summary>
    public void GenerateNow()
    {
        Task.Run(async () =>
        {
            ManualSession? snapshot;
            lock (_lock)
            {
                if (_current == null) return;
                // シャロウコピー（Steps リストをコピー）
                snapshot = new ManualSession
                {
                    Title     = _current.Title,
                    EndedAt   = DateTime.Now,
                };
                snapshot.Steps.AddRange(_current.Steps);
            }
            await WriteSessionAsync(snapshot);
        });
    }

    /// <summary>AutoScreenshot 終了時に呼ばれる。</summary>
    public async Task StopSessionAsync()
    {
        ManualSession? completed;
        lock (_lock)
        {
            completed = _current;
            _current = null;
        }
        if (completed != null)
        {
            completed.EndedAt = DateTime.Now;
            await WriteSessionAsync(completed);
        }
    }

    private async Task WriteSessionAsync(ManualSession session)
    {
        var cfg = _config.Config.ManualGen;
        if (!cfg.Enabled) return;
        if (session.Steps.Count == 0)
        {
            Log.Debug("手順書: ステップ0件のためスキップ");
            return;
        }

        // LLM 連携 (L-02: エンドポイントと API キーの両方が設定されている場合のみ実行)
        bool llmUsed = false;
        if (cfg.LlmEnabled &&
            !string.IsNullOrWhiteSpace(cfg.LlmEndpoint) &&
            !string.IsNullOrWhiteSpace(cfg.LlmApiKey))
        {
            try
            {
                string endpoint = DpapiHelper.Unprotect(cfg.LlmEndpoint);
                string apiKey   = DpapiHelper.Unprotect(cfg.LlmApiKey);

                if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
                {
                    var llm = new LlmService(endpoint, apiKey, cfg.LlmDeploymentName);
                    Log.Information("LLM 操作テキスト改善を開始...");
                    await llm.ImproveDescriptionsAsync(session);            // L-03
                    session.Digest = await llm.GenerateDigestAsync(session); // L-04
                    llmUsed = true;
                    Log.Information("LLM 処理完了");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "LLM 処理中にエラーが発生しました。ルールベースで続行します。"); // L-06
            }
        }

        string folder = string.IsNullOrWhiteSpace(cfg.OutputFolder)
            ? Path.Combine(_config.Config.Storage.SaveFolder, "manuals")
            : cfg.OutputFolder;

        string slug = MakeSlug(session.Title);
        string fileBase = $"{session.StartedAt:yyyyMMdd_HHmmss}_{slug}";

        try
        {
            int gap = cfg.ChapterTimeGapMinutes;
            if (cfg.OutputMarkdown)
                await _mdWriter.WriteAsync(session, Path.Combine(folder, fileBase + ".md"), gap,
                    cfg.TemplateMarkdownPath);
            if (cfg.OutputDocx)
                await _docxWriter.WriteAsync(session, Path.Combine(folder, fileBase + ".docx"), gap,
                    cfg.TemplateDotxPath);

            // NF-03: 手順書生成完了をトースト通知
            _notifier?.ShowManualGeneratedToast(llmUsed);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "手順書書き出し失敗");
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
