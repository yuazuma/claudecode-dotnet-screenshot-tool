using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>操作手順書のセッション記録・生成を管理するサービス</summary>
public class ManualSessionRecorder
{
    private readonly ConfigStore _config;
    private readonly MarkdownManualWriter _mdWriter = new();

    private ManualSession? _current;
    private readonly object _lock = new();

    public ManualSessionRecorder(ConfigStore config)
    {
        _config = config;
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
    public void RecordStep(TriggerEvent evt, string? imagePath)
    {
        var cfg = _config.Config.ManualGen;
        if (!cfg.Enabled) return;

        // E-05: 差分検知と手動撮影は記録しない
        if (evt.Type is TriggerType.ScreenDiff or TriggerType.ManualCapture) return;

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

            var step = new ManualStep
            {
                StepNumber      = _current.Steps.Count + 1,
                Timestamp       = evt.Timestamp,
                TriggerType     = evt.Type,
                CursorPosition  = evt.CursorPosition,
                WindowTitle     = evt.ActiveWindowTitle,
                ProcessName     = evt.ActiveProcessName,
                ImagePath       = includeImage ? imagePath : null,
                // Phase 2 で UIA/OCR を追加: UiElementName / UiControlType は null のまま
                NeedsReview     = false,
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

        string folder = string.IsNullOrWhiteSpace(cfg.OutputFolder)
            ? Path.Combine(_config.Config.Storage.SaveFolder, "manuals")
            : cfg.OutputFolder;

        string slug = MakeSlug(session.Title);
        string fileBase = $"{session.StartedAt:yyyyMMdd_HHmmss}_{slug}";

        try
        {
            if (cfg.OutputMarkdown)
                await _mdWriter.WriteAsync(session, Path.Combine(folder, fileBase + ".md"));
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
