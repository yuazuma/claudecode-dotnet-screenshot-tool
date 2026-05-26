using System.Windows.Forms;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>撮影時フィードバック（アイコン点滅・トースト・カウンター）</summary>
public class Notifier
{
    private readonly ConfigStore _config;
    private NotifyIcon? _notifyIcon;
    private int _todayCount;
    private DateTime _countDate = DateTime.Today;
    private Icon? _normalIcon;
    private Icon? _flashIcon;

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

    public void OnCaptured()
    {
        if (DateTime.Today != _countDate)
        {
            _todayCount = 0;
            _countDate = DateTime.Today;
        }
        _todayCount++;

        var cfg = _config.Config.Notification;

        if (cfg.ShowCounter && _notifyIcon != null)
            _notifyIcon.Text = $"AutoScreenshot - 本日 {_todayCount} 枚撮影";

        if (cfg.IconFlash && _notifyIcon != null)
            FlashIcon();

        if (cfg.Toast)
            ShowToast();
    }

    private void FlashIcon()
    {
        if (_notifyIcon == null || _flashIcon == null) return;

        _notifyIcon.Icon = _flashIcon;
        var timer = new System.Threading.Timer(_ =>
        {
            _notifyIcon.Icon = _normalIcon;
        }, null, 200, System.Threading.Timeout.Infinite);
    }

    private void ShowToast()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.ShowBalloonTip(1500, "AutoScreenshot", "スクリーンショットを保存しました", ToolTipIcon.None);
    }

    public void SetPausedState(bool paused, NotifyIcon icon)
    {
        icon.Text = paused ? "AutoScreenshot [一時停止中]" : $"AutoScreenshot - 本日 {_todayCount} 枚撮影";
    }
}
