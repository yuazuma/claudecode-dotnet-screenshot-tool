using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>画像ファイル保存・命名規則適用</summary>
public class FileStorage
{
    private readonly ConfigStore _config;
    private string? _sessionId;
    private Notifier? _notifier;
    private DateTime _lastDiskWarning = DateTime.MinValue;
    private readonly Queue<string> _recentPaths = new();

    // プロジェクト機能が有効な場合に設定される保存先フォルダ
    private string? _projectImagesFolder;

    public Action? OnLowDiskSpaceDetected { get; set; }

    public FileStorage(ConfigStore config)
    {
        _config = config;
        _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    public void SetNotifier(Notifier notifier) => _notifier = notifier;

    /// <summary>プロジェクト機能が有効な場合に呼ぶ。以降の SaveAsync はこのフォルダに保存する。</summary>
    public void SetProjectFolder(string projectFolder)
    {
        _projectImagesFolder = Path.Combine(projectFolder, "images");
        Directory.CreateDirectory(_projectImagesFolder);
    }

    /// <summary>プロジェクトフォルダをクリアする（プロジェクト機能無効時またはセッション区切り時）。</summary>
    public void ClearProjectFolder() => _projectImagesFolder = null;

    public async Task<string> SaveAsync(byte[] imageData, ImageFormat format, TriggerEvent evt)
    {
        string extension = format switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.WebP => "webp",
            _ => "png",
        };

        string folder = _projectImagesFolder ?? BuildFolderPath(evt);
        Directory.CreateDirectory(folder);

        string fileName = BuildFileName(evt, extension);
        string path = Path.Combine(folder, fileName);

        await File.WriteAllBytesAsync(path, imageData);
        Log.Information("保存完了: {Path}", path);

        lock (_recentPaths)
        {
            _recentPaths.Enqueue(path);
            while (_recentPaths.Count > 10) _recentPaths.Dequeue();
        }

        CheckDiskSpace(folder);
        return path;
    }

    public IReadOnlyList<string> GetRecentPaths()
    {
        lock (_recentPaths) { return [.. _recentPaths.Reverse()]; }
    }

    private string BuildFolderPath(TriggerEvent evt)
    {
        var cfg = _config.Config.Storage;
        string root = cfg.SaveFolder;
        string date = evt.Timestamp.ToString("yyyy-MM-dd");
        string hour = evt.Timestamp.ToString("HH");

        return cfg.FolderNaming switch
        {
            FolderNamingRule.DateWithTimestamp => Path.Combine(root, date),
            FolderNamingRule.DateHour => Path.Combine(root, date, hour),
            FolderNamingRule.Session => Path.Combine(root, _sessionId!),
            FolderNamingRule.Flat => root,
            _ => Path.Combine(root, date),
        };
    }

    private static string TriggerToken(TriggerType type) => type switch
    {
        TriggerType.ManualCapture      => "manual",
        TriggerType.MouseLeftClick     => "click",
        TriggerType.MouseRightClick    => "rightclick",
        TriggerType.MouseMiddleClick   => "middleclick",
        TriggerType.MouseDragDrop      => "drag",
        TriggerType.MouseWheel         => "scroll",
        TriggerType.Keyboard           => "keyboard",
        TriggerType.ActiveWindowChange => "windowchange",
        TriggerType.ScreenDiff         => "diff",
        _                              => type.ToString().ToLowerInvariant(),
    };

    private static string BuildFileName(TriggerEvent evt, string ext)
    {
        string ts = evt.Timestamp.ToString("yyyyMMdd_HHmmss_fff");
        string trigger = TriggerToken(evt.Type);
        return $"{ts}_{trigger}_monitor{evt.MonitorIndex}.{ext}";
    }

    private void CheckDiskSpace(string folder)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(folder)!);
            long freeMb = drive.AvailableFreeSpace / (1024 * 1024);
            long threshold = _config.Config.Storage.LowDiskSpaceThresholdMb;

            if (freeMb < threshold)
            {
                Log.Warning("ディスク空き容量が少なくなっています: {Free}MB (しきい値: {Threshold}MB)", freeMb, threshold);

                if (_notifier != null && (DateTime.Now - _lastDiskWarning).TotalMinutes >= 10)
                {
                    _lastDiskWarning = DateTime.Now;
                    _notifier.ShowDiskWarning(freeMb);
                }

                OnLowDiskSpaceDetected?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ディスク容量チェック失敗");
        }
    }
}
