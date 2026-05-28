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
    private Icon? _recordingIcon;
    private Icon? _pausedIcon;
    private Icon? _capturedIcon;
    private Icon? _processingIcon;
    private Icon? _errorIcon;

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
    private readonly VideoGenerator _videoGenerator;
    private readonly ProjectStore _projectStore;
    private readonly ExportService _exportService;

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
        _projectStore = new ProjectStore(_config);
        _manualRecorder = new ManualSessionRecorder(
            _config, new UiaService(), new OcrService(), _notifier, _projectStore);
        _videoGenerator = new VideoGenerator(_config, _notifier);
        _manualRecorder.SetVideoGenerator(_videoGenerator);
        _exportService = new ExportService(_config, _projectStore, _videoGenerator, _notifier);
        _orchestrator = new TriggerOrchestrator(
            _config, _hook, _capture, _storage, _diffDetector, _metadataLogger, _notifier, _masking,
            _manualRecorder);
    }

    public void Initialize()
    {
        _recordingIcon   = IconFactory.CreateRecordingIcon(32);
        _pausedIcon      = IconFactory.CreatePausedIcon(32);
        _capturedIcon    = IconFactory.CreateCapturedIcon(32);
        _processingIcon  = IconFactory.CreateProcessingIcon(32);
        _errorIcon       = IconFactory.CreateErrorIcon(32);

        _notifyIcon = new NotifyIcon
        {
            Icon = _recordingIcon,
            Text = "AutoScreenshot",
            Visible = true,
            ContextMenuStrip = BuildContextMenu(),
        };

        _notifier.SetNotifyIcon(_notifyIcon,
            _recordingIcon, _pausedIcon, _capturedIcon, _processingIcon, _errorIcon);
        _storage.SetNotifier(_notifier);
        _storage.OnLowDiskSpaceDetected = OnLowDiskSpace;
        _hook.Start();

        if (_config.Config.ManualGen.Enabled)
        {
            string title = GetSessionTitle();
            _manualRecorder.StartSession(title);

            _ = SetStorageProjectFolderAsync();
        }

        _hotkeyService.Register(_config.Config.HotkeyPause);
        _config.ConfigChanged += OnHotkeyConfigChanged;

        if (_config.Config.AutoStart && !AutoStartService.IsEnabled())
            AutoStartService.Enable();

        Log.Information("タスクトレイアイコン表示完了");
    }

    private async Task SetStorageProjectFolderAsync()
    {
        // プロジェクト作成完了を最大 5 秒待つ
        for (int i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            var proj = _manualRecorder.CurrentProject;
            if (proj != null)
            {
                _storage.SetProjectFolder(proj.ProjectFolder);
                return;
            }
        }
        Log.Warning("プロジェクトフォルダの設定タイムアウト");
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

        var projectNameItem = new ToolStripMenuItem("記録中: —") { Enabled = false };

        menu.Opening += (_, _) =>
        {
            var proj = _manualRecorder.CurrentProject;
            projectNameItem.Text = proj != null ? $"記録中: {proj.Title}" : "記録中: —";

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

        var newProjectItem = new ToolStripMenuItem("新しいプロジェクトを開始");
        newProjectItem.Click += (_, _) =>
        {
            _storage.ClearProjectFolder();
            _manualRecorder.SplitSession(GetSessionTitle());
            _ = SetStorageProjectFolderAsync();
        };

        var exportManualItem = new ToolStripMenuItem("Markdown で出力");
        exportManualItem.Click += (_, _) =>
        {
            var proj = _manualRecorder.CurrentProject;
            if (proj != null) _ = _exportService.ExportMarkdownAsync(proj);
        };

        var exportHtmlItem = new ToolStripMenuItem("HTML で出力");
        exportHtmlItem.Click += (_, _) =>
        {
            var proj = _manualRecorder.CurrentProject;
            if (proj != null) _ = _exportService.ExportHtmlAsync(proj);
        };

        var exportVideoItem = new ToolStripMenuItem("動画を生成");
        exportVideoItem.Click += (_, _) =>
        {
            var proj = _manualRecorder.CurrentProject;
            if (proj != null) _ = _exportService.ExportVideoAsync(proj);
        };

        var exportImagesItem = new ToolStripMenuItem("画像を書き出す");
        exportImagesItem.Click += (_, _) =>
        {
            var proj = _manualRecorder.CurrentProject;
            if (proj != null) _ = _exportService.ExportImagesAsync(proj);
        };

        var exportMenu = new ToolStripMenuItem("エクスポート");
        exportMenu.DropDownItems.AddRange([exportManualItem, exportHtmlItem, exportVideoItem, exportImagesItem]);

        var manageProjectItem = new ToolStripMenuItem("プロジェクトを管理...");
        manageProjectItem.Click += (_, _) =>
        {
            var win = new ProjectViewWindow(_config, _projectStore, _exportService);
            win.Show();
        };

        menu.Items.AddRange([
            projectNameItem, new ToolStripSeparator(),
            _pauseItem, newProjectItem, new ToolStripSeparator(),
            captureNowItem, historyItem, new ToolStripSeparator(),
            exportMenu, manageProjectItem, new ToolStripSeparator(),
            openFolderItem, settingsItem, new ToolStripSeparator(),
            BuildVersionItem(),
            BuildExitItem()
        ]);

        return menu;
    }

    private static ToolStripMenuItem BuildVersionItem()
    {
        var item = new ToolStripMenuItem("バージョン情報");
        item.Click += (_, _) =>
            System.Windows.MessageBox.Show(
                "AutoScreenshot v1.5.0\n\nタスクトレイ常駐型 自動スクリーンショット撮影・動画生成ツール",
                "バージョン情報",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        return item;
    }

    private static ToolStripMenuItem BuildExitItem()
    {
        var item = new ToolStripMenuItem("終了");
        item.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        return item;
    }

    private string GetSessionTitle()
    {
        if (!_config.Config.ManualGen.ShowTitleDialogOnStart) return "";
        var dialog = new AutoScreenshot.Views.ManualTitleDialog();
        return dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.EnteredTitle)
            ? dialog.EnteredTitle
            : "";
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
            _notifier.SetBaseState(true);
        });
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        _paused = !_paused;
        _orchestrator.SetPaused(_paused);

        if (_pauseItem != null)
            _pauseItem.Text = _paused ? "再開" : "一時停止";

        _notifier.SetBaseState(_paused);
    }

    public void Dispose()
    {
        _config.ConfigChanged -= OnHotkeyConfigChanged;
        _hook.Stop();
        _hotkeyService.Dispose();
        _orchestrator.Dispose();
        _diffDetector.Dispose();
        _storage.ClearProjectFolder();

        _manualRecorder.StopSessionAsync().GetAwaiter().GetResult();

        _notifyIcon?.Dispose();
        _recordingIcon?.Dispose();
        _pausedIcon?.Dispose();
        _capturedIcon?.Dispose();
        _processingIcon?.Dispose();
        _errorIcon?.Dispose();
    }
}
