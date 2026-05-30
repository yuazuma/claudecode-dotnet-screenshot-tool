using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>画像ファイル保存・フォルダテンプレート・フォールバック管理（FR-H3/H5）</summary>
public class FileStorage
{
    private readonly ConfigStore _config;
    private string? _sessionId;
    private Notifier? _notifier;
    private DateTime _lastDiskWarning = DateTime.MinValue;
    private readonly Queue<string> _recentPaths = new();

    // FR-H3: セッション開始日時とタイトル（テンプレート評価用）
    private DateTime _sessionStart = DateTime.Now;
    private string? _sessionTitle;

    // FR-H5: フォールバック状態管理
    private bool _usingFallback;
    private string? _fallbackBaseFolder;

    // プロジェクト機能が有効な場合に設定される保存先フォルダ
    private string? _projectImagesFolder;

    public Action? OnLowDiskSpaceDetected { get; set; }

    public FileStorage(ConfigStore config)
    {
        _config = config;
        _sessionStart = DateTime.Now;
        _sessionId = _sessionStart.ToString("yyyyMMdd_HHmmss");
    }

    public void SetNotifier(Notifier notifier) => _notifier = notifier;

    /// <summary>セッション開始日時とタイトルを設定する（FR-H3 テンプレート評価用）。</summary>
    public void SetSessionInfo(DateTime sessionStart, string? title)
    {
        _sessionStart = sessionStart;
        _sessionTitle = title;
        _sessionId    = sessionStart.ToString("yyyyMMdd_HHmmss");
    }

    /// <summary>FR-H5: フォールバックベースフォルダを有効化する（以降の保存をフォールバック先へ）。</summary>
    public void ActivateFallback(string fallbackBaseFolder)
    {
        _usingFallback     = true;
        _fallbackBaseFolder = fallbackBaseFolder;
        Log.Warning("FileStorage: フォールバックフォルダへ切り替え: {Fallback}", fallbackBaseFolder);
    }

    /// <summary>FR-H5: 現在フォールバック状態かどうか。</summary>
    public bool IsUsingFallback => _usingFallback;

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
        string fileName = BuildFileName(evt, extension);
        string path = Path.Combine(folder, fileName);

        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(path, imageData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // FR-H5: 書き込み失敗 → フォールバックへ切り替え
            var cfg = _config.Config.Storage;
            if (!_usingFallback && !string.IsNullOrEmpty(cfg.ImageFallbackBaseFolder))
            {
                ActivateFallback(cfg.ImageFallbackBaseFolder);
                _notifier?.ShowFallbackActivated("画像", cfg.ImageBaseFolder, cfg.ImageFallbackBaseFolder);
                folder = BuildFolderPath(evt);
                path   = Path.Combine(folder, fileName);
                Directory.CreateDirectory(folder);
                await File.WriteAllBytesAsync(path, imageData);
            }
            else
            {
                Log.Error(ex, "画像保存失敗（フォールバックなし）: {Path}", path);
                throw;
            }
        }

        Log.Information("保存完了: {Path}", path);

        lock (_recentPaths)
        {
            _recentPaths.Enqueue(path);
            while (_recentPaths.Count > 10) _recentPaths.Dequeue();
        }

        CheckDiskSpace(folder);
        return path;
    }

    /// <summary>操作前スクリーンショットをプロジェクトの images/before/ に保存する（FR-H1: after と同フォーマット）。
    /// プロジェクトフォルダが未設定の場合は null を返す。</summary>
    public async Task<string?> SaveBeforeAsync(byte[] imageData, TriggerEvent evt)
    {
        if (_projectImagesFolder == null) return null;
        string beforeFolder = Path.Combine(_projectImagesFolder, "before");
        Directory.CreateDirectory(beforeFolder);
        string ext = _config.Config.Storage.ImageFormat switch
        {
            ImageFormat.Jpeg => "jpg",
            ImageFormat.WebP => "webp",
            _                => "png",
        };
        string fileName = BuildFileName(evt, ext);
        string path = Path.Combine(beforeFolder, fileName);
        await File.WriteAllBytesAsync(path, imageData);
        Log.Debug("before 画像保存: {Path}", path);
        return path;
    }

    public IReadOnlyList<string> GetRecentPaths()
    {
        lock (_recentPaths) { return [.. _recentPaths.Reverse()]; }
    }

    private string BuildFolderPath(TriggerEvent evt)
    {
        var cfg  = _config.Config.Storage;
        string baseFolder = _usingFallback && !string.IsNullOrEmpty(_fallbackBaseFolder)
            ? _fallbackBaseFolder
            : cfg.ImageBaseFolder;

        string subfolder = FolderTemplateService.Evaluate(
            cfg.ImageFolderTemplate, _sessionStart, _sessionTitle);

        return string.IsNullOrEmpty(subfolder)
            ? baseFolder
            : Path.Combine(baseFolder, subfolder);
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
