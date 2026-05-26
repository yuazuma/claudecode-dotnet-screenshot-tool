namespace AutoScreenshot.Models;

public enum ScreenshotMode { All, WindowChange, None }
public enum KeyboardMode { RealText, KeyCode, Both }

public class ManualGenConfig
{
    public bool Enabled { get; set; } = true;
    public string OutputFolder { get; set; } = "";  // 空 = {SaveFolder}/manuals/
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
