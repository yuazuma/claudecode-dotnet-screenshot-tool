namespace AutoScreenshot.Models;

public enum TriggerType
{
    ManualCapture,
    MouseLeftClick,
    MouseRightClick,
    MouseMiddleClick,
    MouseDragDrop,
    MouseWheel,
    Keyboard,
    ActiveWindowChange,
    ScreenDiff,
}

public record TriggerEvent(
    TriggerType Type,
    DateTime Timestamp,
    System.Drawing.Point CursorPosition,
    string ActiveWindowTitle,
    string ActiveProcessName,
    int MonitorIndex
)
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    // E-04: キーボードイベント時のみ使用
    public string? InputText { get; init; }
    public string? KeyCodes  { get; init; }
}
