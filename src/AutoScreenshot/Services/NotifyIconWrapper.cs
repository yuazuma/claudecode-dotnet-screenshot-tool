using System.Drawing;
using System.Windows.Forms;
using System.Windows;
using AutoScreenshot.Views;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>タスクトレイアイコンの管理とメニュー制御</summary>
public class NotifyIconWrapper : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly ConfigStore _config;
    private readonly HookService _hook;
    private readonly CaptureService _capture;
    private readonly DiffDetector _diffDetector;
    private readonly FileStorage _storage;
    private readonly MetadataLogger _metadataLogger;
    private readonly Notifier _notifier;
    private readonly TriggerOrchestrator _orchestrator;

    private bool _paused;

    public NotifyIconWrapper()
    {
        _config = new ConfigStore();
        _config.Load();

        _hook = new HookService();
        _capture = new CaptureService();
        _diffDetector = new DiffDetector(_capture);
        _storage = new FileStorage(_config);
        _metadataLogger = new MetadataLogger(_config);
        _notifier = new Notifier(_config);
        _orchestrator = new TriggerOrchestrator(
            _config, _hook, _capture, _storage, _diffDetector, _metadataLogger, _notifier);
    }

    public void Initialize()
    {
        var icon = CreateDefaultIcon();

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "AutoScreenshot",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(),
        };

        _notifier.SetNotifyIcon(_notifyIcon, icon, null);
        _hook.Start();

        // 自動起動の確認・登録
        if (_config.Config.AutoStart && !AutoStartService.IsEnabled())
            AutoStartService.Enable();

        Log.Information("タスクトレイアイコン表示完了");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var pauseItem = new ToolStripMenuItem("一時停止");
        pauseItem.Click += (_, _) =>
        {
            _paused = !_paused;
            _orchestrator.SetPaused(_paused);
            pauseItem.Text = _paused ? "再開" : "一時停止";
            _notifier.SetPausedState(_paused, _notifyIcon!);
        };

        var openFolderItem = new ToolStripMenuItem("保存フォルダを開く");
        openFolderItem.Click += (_, _) =>
        {
            string folder = _config.Config.Storage.SaveFolder;
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start("explorer.exe", folder);
        };

        var settingsItem = new ToolStripMenuItem("設定");
        settingsItem.Click += (_, _) =>
        {
            var win = new SettingsWindow(_config);
            win.Show();
        };

        var captureNowItem = new ToolStripMenuItem("今すぐ撮影");
        captureNowItem.Click += (_, _) => _orchestrator.CaptureNow();

        var versionItem = new ToolStripMenuItem("バージョン情報");
        versionItem.Click += (_, _) =>
            System.Windows.MessageBox.Show("AutoScreenshot v1.0\n© 2026", "バージョン情報",
                MessageBoxButton.OK, MessageBoxImage.Information);

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.AddRange([
            pauseItem, openFolderItem, new ToolStripSeparator(),
            settingsItem, captureNowItem, new ToolStripSeparator(),
            versionItem, new ToolStripSeparator(),
            exitItem
        ]);

        return menu;
    }

    private static Icon CreateDefaultIcon()
    {
        // ビルドイン暫定アイコン (カメラ形状のシンプルなビットマップ)
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(0, 120, 215));
        g.FillEllipse(Brushes.White, 3, 4, 10, 8);
        g.FillEllipse(new SolidBrush(Color.FromArgb(0, 120, 215)), 5, 6, 6, 5);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _hook.Stop();
        _orchestrator.Dispose();
        _diffDetector.Dispose();
        _notifyIcon?.Dispose();
    }
}
