using System.Drawing;
using System.Windows.Forms;
using AutoScreenshot.Resources;
using AutoScreenshot.Views;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>タスクトレイアイコンの管理とメニュー制御</summary>
public class NotifyIconWrapper : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private Icon? _normalIcon;
    private Icon? _pausedIcon;

    private readonly ConfigStore _config;
    private readonly HookService _hook;
    private readonly CaptureService _capture;
    private readonly DiffDetector _diffDetector;
    private readonly FileStorage _storage;
    private readonly MetadataLogger _metadataLogger;
    private readonly Notifier _notifier;
    private readonly MaskingService _masking;
    private readonly HotkeyService _hotkeyService;
    private readonly ManualSessionRecorder _manualRecorder;
    private readonly TriggerOrchestrator _orchestrator;

    private bool _paused;
    private ToolStripMenuItem? _pauseItem;

    public NotifyIconWrapper()
    {
        _config = new ConfigStore();
        _config.Load();

        _hook = new HookService(() => _config.Config.Triggers);
        _capture = new CaptureService();
        _diffDetector = new DiffDetector(_capture);
        _storage = new FileStorage(_config);
        _metadataLogger = new MetadataLogger(_config);
        _notifier = new Notifier(_config);
        _masking = new MaskingService();
        _hotkeyService = new HotkeyService();
        _hotkeyService.HotkeyPressed += (_, _) => OnPauseClick(null, EventArgs.Empty);
        _manualRecorder = new ManualSessionRecorder(_config, new UiaService(), new OcrService());
        _orchestrator = new TriggerOrchestrator(
            _config, _hook, _capture, _storage, _diffDetector, _metadataLogger, _notifier, _masking,
            _manualRecorder);
    }

    public void Initialize()
    {
        _normalIcon = IconFactory.CreateNormalIcon(32);
        _pausedIcon = IconFactory.CreatePausedIcon(32);

        _notifyIcon = new NotifyIcon
        {
            Icon = _normalIcon,
            Text = "AutoScreenshot",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(),
        };

        _notifier.SetNotifyIcon(_notifyIcon, _normalIcon, _pausedIcon);
        _storage.SetNotifier(_notifier);
        _storage.OnLowDiskSpaceDetected = OnLowDiskSpace;
        _hook.Start();

        // 手順書セッション開始 (S-01)
        if (_config.Config.ManualGen.Enabled)
            _manualRecorder.StartSession();

        // グローバルホットキー登録
        _hotkeyService.Register(_config.Config.HotkeyPause);
        _config.ConfigChanged += OnHotkeyConfigChanged;

        // 自動起動の確認・登録
        if (_config.Config.AutoStart && !AutoStartService.IsEnabled())
            AutoStartService.Enable();

        Log.Information("タスクトレイアイコン表示完了");
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        _pauseItem = new ToolStripMenuItem("一時停止");
        _pauseItem.Click += OnPauseClick;

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

        var historyItem = new ToolStripMenuItem("キャプチャ履歴");
        menu.Opening += (_, _) =>
        {
            historyItem.DropDownItems.Clear();
            var paths = _storage.GetRecentPaths();
            if (paths.Count == 0)
            {
                historyItem.DropDownItems.Add(
                    new ToolStripMenuItem("(まだ撮影されていません)") { Enabled = false });
            }
            else
            {
                foreach (var p in paths)
                {
                    string name = Path.GetFileName(p);
                    var item = new ToolStripMenuItem(name);
                    string capturePath = p;
                    item.Click += (_, _) =>
                    {
                        if (File.Exists(capturePath))
                            System.Diagnostics.Process.Start(
                                "explorer.exe", $"/select,\"{capturePath}\"");
                    };
                    historyItem.DropDownItems.Add(item);
                }
            }
        };

        var sessionSplitItem = new ToolStripMenuItem("手順書セッション区切り");
        sessionSplitItem.Click += (_, _) => _manualRecorder.SplitSession();

        var generateNowItem = new ToolStripMenuItem("手順書を今すぐ生成");
        generateNowItem.Click += (_, _) => _manualRecorder.GenerateNow();

        var versionItem = new ToolStripMenuItem("バージョン情報");
        versionItem.Click += (_, _) =>
            System.Windows.MessageBox.Show(
                "AutoScreenshot v1.0\n\nタスクトレイ常駐型 自動スクリーンショット撮影ツール",
                "バージョン情報",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.AddRange([
            _pauseItem, openFolderItem, new ToolStripSeparator(),
            settingsItem, captureNowItem, historyItem, new ToolStripSeparator(),
            sessionSplitItem, generateNowItem, new ToolStripSeparator(),
            versionItem, new ToolStripSeparator(),
            exitItem
        ]);

        return menu;
    }

    private void OnHotkeyConfigChanged(object? sender, EventArgs e)
    {
        _hotkeyService.Register(_config.Config.HotkeyPause);
    }

    private void OnLowDiskSpace()
    {
        if (_paused) return;
        Log.Warning("ディスク容量不足のため撮影を自動一時停止");
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _paused = true;
            _orchestrator.SetPaused(true);
            if (_pauseItem != null) _pauseItem.Text = "再開";
            if (_notifyIcon != null)
            {
                _notifyIcon.Icon = _pausedIcon;
                _notifier.SetPausedState(true, _notifyIcon);
            }
        });
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        _paused = !_paused;
        _orchestrator.SetPaused(_paused);

        if (_pauseItem != null)
            _pauseItem.Text = _paused ? "再開" : "一時停止";

        if (_notifyIcon != null)
        {
            _notifyIcon.Icon = _paused ? _pausedIcon : _normalIcon;
            _notifier.SetPausedState(_paused, _notifyIcon);
        }
    }

    public void Dispose()
    {
        _config.ConfigChanged -= OnHotkeyConfigChanged;
        _hook.Stop();
        _hotkeyService.Dispose();
        _orchestrator.Dispose();
        _diffDetector.Dispose();

        // 手順書セッション終了 (S-02) — 同期的に待機
        _manualRecorder.StopSessionAsync().GetAwaiter().GetResult();

        _notifyIcon?.Dispose();
        _normalIcon?.Dispose();
        _pausedIcon?.Dispose();
    }
}
