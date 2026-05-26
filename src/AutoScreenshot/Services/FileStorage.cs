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

    public FileStorage(ConfigStore config)
    {
        _config = config;
        _sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    public void SetNotifier(Notifier notifier) => _notifier = notifier;

    public async Task<string> SaveAsync(byte[] imageData, ImageFormat format, TriggerEvent evt)
    {
        string extension = format switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.WebP => "webp",
            _ => "png",
        };

        string folder = BuildFolderPath(evt);
        Directory.CreateDirectory(folder);

        string fileName = BuildFileName(evt, extension);
        string path = Path.Combine(folder, fileName);

        await File.WriteAllBytesAsync(path, imageData);
        Log.Information("保存完了: {Path}", path);

        CheckDiskSpace(folder);
        return path;
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

    private static string BuildFileName(TriggerEvent evt, string ext)
    {
        string ts = evt.Timestamp.ToString("yyyyMMdd_HHmmss_fff");
        string trigger = evt.Type.ToString().ToLowerInvariant();
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

                // トースト通知は 10 分に 1 回まで
                if (_notifier != null && (DateTime.Now - _lastDiskWarning).TotalMinutes >= 10)
                {
                    _lastDiskWarning = DateTime.Now;
                    _notifier.ShowDiskWarning(freeMb);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "ディスク容量チェック失敗");
        }
    }
}
