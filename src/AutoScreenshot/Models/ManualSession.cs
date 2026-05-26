namespace AutoScreenshot.Models;

public class ManualSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public DateTime? EndedAt { get; set; }
    public string OsInfo { get; init; } =
        $"{Environment.OSVersion} / {Environment.UserName}";
    public List<ManualStep> Steps { get; } = [];
    /// <summary>LLM が生成した操作内容の要約（3〜5 行）。LLM 無効時は null。</summary>
    public string? Digest { get; set; }
}

public class ManualStep
{
    public int StepNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public TriggerType TriggerType { get; set; }
    public string? UiElementName { get; set; }
    public string? UiControlType { get; set; }
    public System.Drawing.Point CursorPosition { get; set; }
    public string WindowTitle { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? InputText { get; set; }
    public string? KeyCodes { get; set; }
    public string? ImagePath { get; set; }
    public string DescriptionRuleBased { get; set; } = "";
    public string? DescriptionLlm { get; set; }
    public bool NeedsReview { get; set; }
}
