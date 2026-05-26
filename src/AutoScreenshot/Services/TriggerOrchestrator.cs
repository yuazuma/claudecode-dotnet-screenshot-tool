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
    private readonly MaskingService _masking;

    private bool _paused;
    private readonly Dictionary<TriggerType, DateTime> _lastCapture = [];
    private readonly System.Threading.Timer _diffTimer;
    private int _diffIntervalMs;
    private DateTime _lastKeyboardActivity = DateTime.MinValue;
    private DateTime _lastMouseActivity = DateTime.MinValue;
    private System.Threading.Timer? _keyboardIdleTimer;

    public TriggerOrchestrator(
        ConfigStore config, HookService hook, CaptureService capture,
        FileStorage storage, DiffDetector diffDetector,
        MetadataLogger logger, Notifier notifier, MaskingService masking)
    {
        _config = config;
        _hook = hook;
        _capture = capture;
        _storage = storage;
        _diffDetector = diffDetector;
        _logger = logger;
        _notifier = notifier;
        _masking = masking;

        _hook.MouseEvent += OnMouseEvent;
        _hook.KeyboardActivity += OnKeyboardActivity;
        _hook.ActiveWindowChanged += OnActiveWindowChanged;

        _diffIntervalMs = (int)(_config.Config.Triggers.ScreenDiffIntervalSeconds * 1000);
        _diffTimer = new System.Threading.Timer(OnDiffTimer, null, _diffIntervalMs, _diffIntervalMs);

        _config.ConfigChanged += OnConfigChanged;
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

    private void OnConfigChanged(object? sender, EventArgs e)
    {
        int newMs = (int)(_config.Config.Triggers.ScreenDiffIntervalSeconds * 1000);
        if (newMs != _diffIntervalMs)
        {
            _diffIntervalMs = newMs;
            _diffTimer.Change(_diffIntervalMs, _diffIntervalMs);
            Log.Information("差分検知タイマー間隔を更新: {Interval}ms", _diffIntervalMs);
        }
    }

    private void OnMouseEvent(object? sender, TriggerType triggerType)
    {
        if (_paused) return;
        _lastMouseActivity = DateTime.UtcNow;
        var cfg = _config.Config.Triggers;

        // トリガー有効チェック + クールダウン値の決定
        (bool enabled, double cooldown) = triggerType switch
        {
            TriggerType.MouseLeftClick   => (cfg.MouseLeftClick,   cfg.CooldownMouseClick),
            TriggerType.MouseRightClick  => (cfg.MouseRightClick,  cfg.CooldownMouseClick),
            TriggerType.MouseMiddleClick => (cfg.MouseMiddleClick, cfg.CooldownMouseClick),
            TriggerType.MouseDragDrop    => (cfg.MouseDragDrop,    cfg.CooldownMouseDragDrop),
            TriggerType.MouseWheel       => (cfg.MouseWheel,       cfg.CooldownMouseWheel),
            _ => (false, 0),
        };

        if (!enabled) return;
        if (!CheckCooldown(triggerType, cooldown)) return;
        if (IsExcludedApp()) return;

        Log.Debug("TriggerOrchestrator: 撮影キュー投入 ({Trigger})", triggerType);
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
            if (changedScreens.Count == 0) return;

            Log.Debug("差分チェック: 変化モニタ数={Count} ({Indices})",
                changedScreens.Count, string.Join(",", changedScreens));

            if (!CheckCooldown(TriggerType.ScreenDiff, _config.Config.Triggers.CooldownScreenDiff)) return;
            if (IsExcludedApp()) return;

            // マウス・キーボード直後 (1秒以内) の差分はトリガーイベント起因とみなして除外
            if ((DateTime.UtcNow - _lastKeyboardActivity).TotalSeconds < 1.0) return;
            if ((DateTime.UtcNow - _lastMouseActivity).TotalSeconds < 1.0) return;

            FireCapture(TriggerType.ScreenDiff, changedScreens);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OnDiffTimer 例外");
        }
    }

    private void FireCapture(TriggerType trigger, IReadOnlyList<int>? screenIndices = null)
    {
        Task.Run(async () =>
        {
            try
            {
                NativeMethods.GetCursorPos(out var pt);
                var cursorPos = new System.Drawing.Point(pt.X, pt.Y);
                string title = GetActiveWindowTitle();
                string procName = GetActiveProcessName();

                var screenshots = screenIndices != null
                    ? _capture.CaptureScreensByIndex(screenIndices)
                    : _capture.CaptureAllScreens();
                var cfg = _config.Config;

                foreach (var (bmp, monitorIdx, bounds) in screenshots)
                {
                    var evt = new TriggerEvent(trigger, DateTime.Now, cursorPos, title, procName, monitorIdx);

                    // パスワード欄マスキング
                    if (cfg.Privacy.MaskPasswordFields)
                        _masking.ApplyMasking(bmp, bounds);

                    // カーソル位置オーバーレイ描画
                    if (cfg.Metadata.ImageOverlay)
                        _capture.DrawImageOverlay(bmp, cursorPos, bounds, trigger);

                    // タイムスタンプ焼き込み
                    if (cfg.Metadata.BurnTimestamp)
                        _capture.BurnTimestamp(bmp, evt.Timestamp);

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
        _config.ConfigChanged -= OnConfigChanged;
        _diffTimer.Dispose();
        _keyboardIdleTimer?.Dispose();
        _hook.MouseEvent -= OnMouseEvent;
        _hook.KeyboardActivity -= OnKeyboardActivity;
        _hook.ActiveWindowChanged -= OnActiveWindowChanged;
    }
}
