using System.Windows.Forms;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>撮影フィードバック・アイコン状態管理（撮影カウンター・トースト・アイコン色切替）</summary>
public class Notifier
{
    private readonly ConfigStore _config;
    private NotifyIcon? _notifyIcon;
    private readonly object _stateLock = new();

    // カウンター
    private int _todayCount;
    private DateTime _countDate = DateTime.Today;

    // アイコンセット（5種）
    private Icon? _recordingIcon;
    private Icon? _pausedIcon;
    private Icon? _capturedIcon;
    private Icon? _processingIcon;
    private Icon? _errorIcon;

    // 状態
    private bool _isPaused;
    private int _processingCount;
    private bool _errorActive;
    private bool _capturedActive;
    private System.Threading.Timer? _capturedTimer;
    private System.Threading.Timer? _errorTimer;

    public Notifier(ConfigStore config)
    {
        _config = config;
    }

    /// <summary>
    /// NotifyIcon と5種類のアイコンを登録する。
    /// NotifyIconWrapper.Initialize() から呼ぶ。
    /// </summary>
    public void SetNotifyIcon(NotifyIcon icon,
        Icon? recordingIcon, Icon? pausedIcon,
        Icon? capturedIcon, Icon? processingIcon, Icon? errorIcon)
    {
        _notifyIcon      = icon;
        _recordingIcon   = recordingIcon;
        _pausedIcon      = pausedIcon;
        _capturedIcon    = capturedIcon;
        _processingIcon  = processingIcon;
        _errorIcon       = errorIcon;
    }

    // ---- 基本状態の切替（一時停止 / 録画中） ----

    /// <summary>一時停止状態を更新しアイコン・ツールチップを反映する。UIスレッドから呼ぶこと。</summary>
    public void SetBaseState(bool paused)
    {
        lock (_stateLock)
        {
            _isPaused = paused;
            if (_notifyIcon != null)
                _notifyIcon.Text = paused
                    ? "AutoScreenshot [一時停止中]"
                    : $"AutoScreenshot - 本日 {_todayCount} 枚撮影";
        }
        ApplyDisplayState();
    }

    // ---- 処理中カウンター（ExportService から呼ぶ） ----

    /// <summary>バックグラウンド処理の開始を通知する。スレッドセーフ。</summary>
    public void BeginProcessing()
    {
        Interlocked.Increment(ref _processingCount);
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(ApplyDisplayState);
    }

    /// <summary>バックグラウンド処理の完了を通知する。スレッドセーフ。</summary>
    public void EndProcessing()
    {
        Interlocked.Decrement(ref _processingCount);
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(ApplyDisplayState);
    }

    /// <summary>エラー発生を通知する。アイコンを赤に切り替え、5秒後に基本状態へ自動復帰する。スレッドセーフ。</summary>
    public void ShowError()
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            lock (_stateLock) { _errorActive = true; }
            ApplyDisplayState();
            _errorTimer?.Dispose();
            _errorTimer = new System.Threading.Timer(_ =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    lock (_stateLock) { _errorActive = false; }
                    ApplyDisplayState();
                });
            }, null, 5000, System.Threading.Timeout.Infinite);
        });
    }

    // ---- 撮影フィードバック ----

    /// <summary>撮影完了を通知する。カウンター更新・フラッシュ・トーストを実行する。スレッドセーフ。</summary>
    public void OnCaptured()
    {
        int count;
        lock (_stateLock)
        {
            if (DateTime.Today != _countDate)
            {
                _todayCount = 0;
                _countDate = DateTime.Today;
            }
            count = ++_todayCount;
        }

        var cfg = _config.Config.Notification;

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (cfg.ShowCounter && _notifyIcon != null)
                _notifyIcon.Text = $"AutoScreenshot - 本日 {count} 枚撮影";

            if (cfg.IconFlash && _notifyIcon != null)
                FlashCaptured();

            if (cfg.Toast)
                ShowToast();
        });
    }

    // ---- その他の通知 ----

    public void ShowDiskWarning(long freeMb)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _notifyIcon?.ShowBalloonTip(5000, "AutoScreenshot - ディスク容量警告",
                $"空き容量が少なくなっています: {freeMb}MB", ToolTipIcon.Warning);
        });
    }

    /// <summary>汎用バルーン通知。バックグラウンドスレッドから呼び出し可能。</summary>
    public void ShowBalloon(string title, string message)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        });
    }

    /// <summary>手順書生成完了通知（NF-03）。</summary>
    public void ShowManualGeneratedToast(bool llmUsed = false)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_notifyIcon == null) return;
            string msg = llmUsed ? "手順書を生成しました（LLM最適化済み）" : "手順書を生成しました";
            _notifyIcon.ShowBalloonTip(3000, "AutoScreenshot", msg, ToolTipIcon.Info);
        });
    }

    // ---- プライベート ----

    private void FlashCaptured()
    {
        // UIスレッドから呼ばれる前提
        if (_notifyIcon == null || _capturedIcon == null) return;

        lock (_stateLock) { _capturedActive = true; }
        _notifyIcon.Icon = _capturedIcon;

        _capturedTimer?.Dispose();
        _capturedTimer = new System.Threading.Timer(_ =>
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                lock (_stateLock) { _capturedActive = false; }
                ApplyDisplayState();
            });
        }, null, 200, System.Threading.Timeout.Infinite);
    }

    /// <summary>
    /// 現在の状態に応じてアイコンを決定する。
    /// 優先度: Error > Captured(一時的) > Processing > Paused/Recording
    /// UIスレッドから呼ぶこと。
    /// </summary>
    private void ApplyDisplayState()
    {
        if (_notifyIcon == null) return;

        bool errorActive, capturedActive, isPaused;
        int processingCount;
        lock (_stateLock)
        {
            errorActive     = _errorActive;
            capturedActive  = _capturedActive;
            isPaused        = _isPaused;
            processingCount = _processingCount;
        }

        if (errorActive && _errorIcon != null)
        {
            _notifyIcon.Icon = _errorIcon;
            return;
        }
        if (capturedActive && _capturedIcon != null)
        {
            _notifyIcon.Icon = _capturedIcon;
            return;
        }
        if (processingCount > 0 && _processingIcon != null)
        {
            _notifyIcon.Icon = _processingIcon;
            return;
        }
        _notifyIcon.Icon = isPaused ? _pausedIcon : _recordingIcon;
    }

    private void ShowToast()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.ShowBalloonTip(1500, "AutoScreenshot", "スクリーンショットを保存しました", ToolTipIcon.None);
    }
}
