using System.Diagnostics;
using System.Windows.Forms;
using AutoScreenshot.Models;
using AutoScreenshot.Native;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>各種トリガーの調停・クールダウン・除外判定・撮影キュー投入</summary>
public class TriggerOrchestrator : IDisposable
{
    private readonly ConfigStore _config;
    private readonly HookService _hook;
    private readonly CaptureService _capture;
    private readonly FileStorage _storage;
    private readonly DiffDetector _diffDetector;
    private readonly MetadataLogger _logger;
    private readonly Notifier _notifier;

    private bool _paused;
    private readonly Dictionary<TriggerType, DateTime> _lastCapture = [];
    private readonly System.Threading.Timer _diffTimer;
    private DateTime _lastKeyboardActivity = DateTime.MinValue;
    private System.Threading.Timer? _keyboardIdleTimer;

    public TriggerOrchestrator(
        ConfigStore config, HookService hook, CaptureService capture,
        FileStorage storage, DiffDetector diffDetector,
        MetadataLogger logger, Notifier notifier)
    {
        _config = config;
        _hook = hook;
        _capture = capture;
        _storage = storage;
        _diffDetector = diffDetector;
        _logger = logger;
        _notifier = notifier;

        _hook.MouseEvent += OnMouseEvent;
        _hook.KeyboardActivity += OnKeyboardActivity;
        _hook.ActiveWindowChanged += OnActiveWindowChanged;

        int interval = (int)(_config.Config.Triggers.ScreenDiffIntervalSeconds * 1000);
        _diffTimer = new System.Threading.Timer(OnDiffTimer, null, interval, interval);
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        Log.Information("撮影 {State}", paused ? "一時停止" : "再開");
    }

    public void CaptureNow()
    {
        FireCapture(TriggerType.ManualCapture);
    }

    private void OnMouseEvent(object? sender, TriggerType triggerType)
    {
        if (_paused) return;
        var cfg = _config.Config.Triggers;

        bool enabled = triggerType switch
        {
            TriggerType.MouseLeftClick  => cfg.MouseLeftClick,
            TriggerType.MouseRightClick => cfg.MouseRightClick,
            TriggerType.MouseMiddleClick => cfg.MouseMiddleClick,
            TriggerType.MouseWheel       => cfg.MouseWheel,
            _ => false,
        };

        if (!enabled) return;
        if (!CheckCooldown(triggerType, cfg.CooldownMouseClick)) return;
        if (IsExcludedApp()) return;

        FireCapture(triggerType);
    }

    private void OnKeyboardActivity(object? sender, EventArgs e)
    {
        if (_paused || !_config.Config.Triggers.Keyboard) return;

        _lastKeyboardActivity = DateTime.UtcNow;
        _keyboardIdleTimer?.Dispose();
        double idleSec = _config.Config.Triggers.KeyboardIdleSeconds;
        _keyboardIdleTimer = new System.Threading.Timer(_ =>
        {
            if (_paused || IsExcludedApp()) return;
            if (!CheckCooldown(TriggerType.Keyboard, _config.Config.Triggers.CooldownKeyboard)) return;
            FireCapture(TriggerType.Keyboard);
        }, null, (int)(idleSec * 1000), System.Threading.Timeout.Infinite);
    }

    private void OnActiveWindowChanged(object? sender, EventArgs e)
    {
        if (_paused || !_config.Config.Triggers.ActiveWindowChange) return;
        if (!CheckCooldown(TriggerType.ActiveWindowChange, _config.Config.Triggers.CooldownActiveWindow)) return;
        if (IsExcludedApp()) return;

        FireCapture(TriggerType.ActiveWindowChange);
    }

    private void OnDiffTimer(object? state)
    {
        try
        {
            if (_paused || !_config.Config.Triggers.ScreenDiff) return;

            double threshold = _config.Config.Triggers.ScreenDiffThresholdPercent;
            var changedScreens = _diffDetector.DetectChangedScreens(threshold);
            Log.Debug("差分チェック: 変化モニタ数={Count}", changedScreens.Count);
            if (changedScreens.Count == 0) return;

            if (!CheckCooldown(TriggerType.ScreenDiff, _config.Config.Triggers.CooldownScreenDiff)) return;
            if (IsExcludedApp()) return;

            // キーボード・マウス直後の差分は除外
            if ((DateTime.UtcNow - _lastKeyboardActivity).TotalSeconds < 1.0) return;

            FireCapture(TriggerType.ScreenDiff);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OnDiffTimer 例外");
        }
    }

    private void FireCapture(TriggerType trigger)
    {
        Task.Run(async () =>
        {
            try
            {
                NativeMethods.GetCursorPos(out var pt);
                var cursorPos = new System.Drawing.Point(pt.X, pt.Y);
                string title = GetActiveWindowTitle();
                string procName = GetActiveProcessName();

                var screenshots = _capture.CaptureAllScreens();
                var cfg = _config.Config;

                foreach (var (bmp, monitorIdx, _) in screenshots)
                {
                    var evt = new TriggerEvent(trigger, DateTime.Now, cursorPos, title, procName, monitorIdx);
                    byte[] data = _capture.Encode(bmp, cfg.Storage.ImageFormat, cfg.Storage.JpegQuality);
                    string path = await _storage.SaveAsync(data, cfg.Storage.ImageFormat, evt);
                    bmp.Dispose();

                    if (cfg.Metadata.SidecarTextLog)
                        await _logger.LogEventAsync(evt, path);
                }

                _notifier.OnCaptured();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FireCapture 失敗: {Trigger}", trigger);
            }
        });
    }

    private bool CheckCooldown(TriggerType type, double cooldownSeconds)
    {
        lock (_lastCapture)
        {
            if (_lastCapture.TryGetValue(type, out var last))
            {
                if ((DateTime.UtcNow - last).TotalSeconds < cooldownSeconds)
                    return false;
            }
            _lastCapture[type] = DateTime.UtcNow;
            return true;
        }
    }

    private bool IsExcludedApp()
    {
        var excludes = _config.Config.Privacy.ExcludeApps;
        if (excludes.Count == 0) return false;

        string title = GetActiveWindowTitle();
        string proc = GetActiveProcessName();

        return excludes.Any(pattern =>
            MatchWildcard(title, pattern) || MatchWildcard(proc, pattern));
    }

    private static bool MatchWildcard(string text, string pattern)
    {
        // 簡易ワイルドカード: * のみ対応
        if (!pattern.Contains('*'))
            return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        string regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(text, regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    private static string GetActiveWindowTitle()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        var sb = new System.Text.StringBuilder(256);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetActiveProcessName()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _diffTimer.Dispose();
        _keyboardIdleTimer?.Dispose();
        _hook.MouseEvent -= OnMouseEvent;
        _hook.KeyboardActivity -= OnKeyboardActivity;
        _hook.ActiveWindowChanged -= OnActiveWindowChanged;
    }
}
