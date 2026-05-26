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
);
