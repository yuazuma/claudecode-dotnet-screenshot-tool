namespace AutoScreenshot.Models;

public enum ScreenshotMode { All, WindowChange, None }
public enum KeyboardMode { RealText, KeyCode, Both }

public class ManualGenConfig
{
    public bool Enabled { get; set; } = true;

    // ---- 保存先フォルダ（FR-H3） ----

    /// <summary>手順書のベースフォルダ。</summary>
    public string ManualBaseFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AutoScreenshot");

    /// <summary>日付テンプレートフォルダ名。</summary>
    public string ManualFolderTemplate { get; set; } = "{date_time}";

    /// <summary>第2ベースフォルダ（空文字 = フォールバックなし）。</summary>
    public string ManualFallbackBaseFolder { get; set; } = "";

    // ---- 後方互換 JSON 移行シム ----

    /// <summary>v1.6.x 以前の OutputFolder を ManualBaseFolder へ移行する。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("outputFolder")]
    public string? LegacyOutputFolder
    {
        get => null;
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                ManualBaseFolder = value;
        }
    }

    public bool OutputMarkdown { get; set; } = true;
    public bool OutputDocx { get; set; } = false;   // Phase 3 で実装
    public ScreenshotMode ScreenshotMode { get; set; } = ScreenshotMode.WindowChange;
    public KeyboardMode KeyboardMode { get; set; } = KeyboardMode.Both;
    public int ChapterTimeGapMinutes { get; set; } = 5;
    public bool ShowTitleDialogOnStart { get; set; } = false;
    public string TemplateMarkdownPath { get; set; } = "";
    public string TemplateDotxPath { get; set; } = "";
    public bool LlmEnabled { get; set; } = false;
    public string LlmEndpoint { get; set; } = "";
    public string LlmApiKey { get; set; } = "";
    public string LlmDeploymentName { get; set; } = "claude-haiku-4-5";
}
