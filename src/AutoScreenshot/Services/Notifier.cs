using System.Windows.Forms;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>撮影時フィードバック（アイコン点滅・トースト・カウンター）</summary>
public class Notifier
{
    private readonly ConfigStore _config;
    private NotifyIcon? _notifyIcon;
    private readonly object _countLock = new();
    private int _todayCount;
    private DateTime _countDate = DateTime.Today;
    private Icon? _normalIcon;
    private Icon? _flashIcon;
    private System.Threading.Timer? _flashTimer;

    public Notifier(ConfigStore config)
    {
        _config = config;
    }

    public void SetNotifyIcon(NotifyIcon icon, Icon? normalIcon = null, Icon? flashIcon = null)
    {
        _notifyIcon = icon;
        _normalIcon = normalIcon;
        _flashIcon = flashIcon;
    }

    // Task.Run から呼ばれるためスレッドセーフ実装
    public void OnCaptured()
    {
        int count;
        lock (_countLock)
        {
            if (DateTime.Today != _countDate)
            {
                _todayCount = 0;
                _countDate = DateTime.Today;
            }
            count = ++_todayCount;
        }

        var cfg = _config.Config.Notification;

        // NotifyIcon は UI スレッドから操作する必要がある
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (cfg.ShowCounter && _notifyIcon != null)
                _notifyIcon.Text = $"AutoScreenshot - 本日 {count} 枚撮影";

            if (cfg.IconFlash && _notifyIcon != null)
                FlashIcon();

            if (cfg.Toast)
                ShowToast();
        });
    }

    public void ShowDiskWarning(long freeMb)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_notifyIcon == null) return;
            _notifyIcon.ShowBalloonTip(5000, "AutoScreenshot - ディスク容量警告",
                $"空き容量が少なくなっています: {freeMb}MB", ToolTipIcon.Warning);
        });
    }

    private void FlashIcon()
    {
        // 本メソッドは UI スレッドから呼ばれる前提
        if (_notifyIcon == null || _flashIcon == null) return;

        _notifyIcon.Icon = _flashIcon;
        _flashTimer?.Dispose();
        _flashTimer = new System.Threading.Timer(_ =>
        {
            // タイマーコールバックはバックグラウンドスレッドのため UI スレッドに戻す
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_notifyIcon != null)
                    _notifyIcon.Icon = _normalIcon;
            });
        }, null, 200, System.Threading.Timeout.Infinite);
    }

    private void ShowToast()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.ShowBalloonTip(1500, "AutoScreenshot", "スクリーンショットを保存しました", ToolTipIcon.None);
    }

    /// <summary>汎用バルーン通知。バックグラウンドスレッドから呼び出し可能。</summary>
    public void ShowBalloon(string title, string message)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        });
    }

    /// <summary>手順書生成完了通知（NF-03）。LLM 使用時はその旨を付記する。</summary>
    public void ShowManualGeneratedToast(bool llmUsed = false)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_notifyIcon == null) return;
            string msg = llmUsed ? "手順書を生成しました（LLM最適化済み）" : "手順書を生成しました";
            _notifyIcon.ShowBalloonTip(3000, "AutoScreenshot", msg, ToolTipIcon.Info);
        });
    }

    public void SetPausedState(bool paused, NotifyIcon icon)
    {
        lock (_countLock)
        {
            icon.Text = paused ? "AutoScreenshot [一時停止中]" : $"AutoScreenshot - 本日 {_todayCount} 枚撮影";
        }
    }
}
