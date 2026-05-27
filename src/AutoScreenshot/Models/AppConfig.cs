namespace AutoScreenshot.Models;

/// <summary>アプリケーション設定 (config.json にシリアライズ)</summary>
public class AppConfig
{
    // --- 一般 ---
    public bool AutoStart { get; set; } = true;
    public string? HotkeyPause { get; set; } = null;

    // --- 撮影トリガー ---
    public TriggerConfig Triggers { get; set; } = new();

    // --- 保存 ---
    public StorageConfig Storage { get; set; } = new();

    // --- メタデータ ---
    public MetadataConfig Metadata { get; set; } = new();

    // --- プライバシー ---
    public PrivacyConfig Privacy { get; set; } = new();

    // --- 通知 ---
    public NotificationConfig Notification { get; set; } = new();

    // --- 手順書生成 ---
    public ManualGenConfig ManualGen { get; set; } = new();

    // --- 動画生成 ---
    public VideoGenConfig VideoGen { get; set; } = new();

    // --- プロジェクト ---
    public ProjectConfig Project { get; set; } = new();
}

public class TriggerConfig
{
    public bool MouseLeftClick { get; set; } = true;
    public bool MouseRightClick { get; set; } = true;
    public bool MouseMiddleClick { get; set; } = true;
    public bool MouseDragDrop { get; set; } = true;
    public bool MouseWheel { get; set; } = true;
    public bool Keyboard { get; set; } = true;
    public bool ActiveWindowChange { get; set; } = true;
    public bool ScreenDiff { get; set; } = true;

    // クールダウン (秒)
    public double CooldownMouseClick { get; set; } = 1.0;
    public double CooldownMouseDragDrop { get; set; } = 0.5;
    public double CooldownMouseWheel { get; set; } = 2.0;
    public double CooldownKeyboard { get; set; } = 2.0;
    public double CooldownActiveWindow { get; set; } = 1.0;
    public double CooldownScreenDiff { get; set; } = 3.0;

    // キーボードアイドル待機 (秒)
    public double KeyboardIdleSeconds { get; set; } = 2.0;

    // ドラッグ判定閾値 (ミリ秒): DOWN→UP がこれ以上離れていればドラッグとみなす
    public int DragThresholdMs { get; set; } = 200;

    // ホイールアイドル待機 (ミリ秒): 最終ホイールイベントからこの時間経過後に1枚撮影
    public int WheelIdleMs { get; set; } = 500;

    // 画面差分
    public int ScreenDiffIntervalSeconds { get; set; } = 3;
    public double ScreenDiffThresholdPercent { get; set; } = 30.0;
}

public class StorageConfig
{
    public string SaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AutoScreenshot");

    public ImageFormat ImageFormat { get; set; } = ImageFormat.Png;
    public int JpegQuality { get; set; } = 85;

    public FolderNamingRule FolderNaming { get; set; } = FolderNamingRule.DateWithTimestamp;

    // 空き容量しきい値 (MB)
    public long LowDiskSpaceThresholdMb { get; set; } = 500;
}

public class MetadataConfig
{
    public bool SidecarTextLog { get; set; } = true;
    public bool StructuredOutput { get; set; } = false;
    public StructuredFormat StructuredFormat { get; set; } = StructuredFormat.JsonLines;
    public bool BurnTimestamp { get; set; } = false;
    public bool ImageOverlay { get; set; } = false;
}

public class PrivacyConfig
{
    public bool MaskPasswordFields { get; set; } = true;
    public List<string> ExcludeApps { get; set; } = [];
}

public class NotificationConfig
{
    public bool IconFlash { get; set; } = true;
    public bool Toast { get; set; } = false;
    public bool ShowCounter { get; set; } = true;
}

public enum ImageFormat { Png, Jpeg, WebP }
public enum FolderNamingRule { DateWithTimestamp, DateHour, Session, Flat }
public enum StructuredFormat { JsonLines, Csv }
