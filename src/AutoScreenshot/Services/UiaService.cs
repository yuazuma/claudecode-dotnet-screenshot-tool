using System.Windows.Automation;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>Windows UI Automation でカーソル位置またはフォーカス要素の UI 情報を取得する</summary>
public class UiaService
{
    /// <summary>指定座標の UI 要素を取得する (マウスイベント用)。200ms タイムアウト付き。</summary>
    public async Task<(string? name, string? controlType)> GetElementAtAsync(System.Drawing.Point screenPoint)
    {
        var uiaTask = Task.Run(() => TryGetElementAt(screenPoint));
        if (await Task.WhenAny(uiaTask, Task.Delay(200)) == uiaTask)
            return uiaTask.IsCompletedSuccessfully ? uiaTask.Result : (null, null);

        Log.Debug("UIA GetElementAt タイムアウト ({X}, {Y})", screenPoint.X, screenPoint.Y);
        return (null, null);
    }

    /// <summary>現在フォーカスされている UI 要素を取得する (キーボードイベント用)。200ms タイムアウト付き。</summary>
    public async Task<(string? name, string? controlType)> GetFocusedElementAsync()
    {
        var uiaTask = Task.Run(TryGetFocusedElement);
        if (await Task.WhenAny(uiaTask, Task.Delay(200)) == uiaTask)
            return uiaTask.IsCompletedSuccessfully ? uiaTask.Result : (null, null);

        Log.Debug("UIA GetFocusedElement タイムアウト");
        return (null, null);
    }

    private static (string? name, string? controlType) TryGetElementAt(System.Drawing.Point pt)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(pt.X, pt.Y));
            return ExtractInfo(element);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "UIA FromPoint 例外 ({X}, {Y})", pt.X, pt.Y);
            return (null, null);
        }
    }

    private static (string? name, string? controlType) TryGetFocusedElement()
    {
        try
        {
            return ExtractInfo(AutomationElement.FocusedElement);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "UIA FocusedElement 例外");
            return (null, null);
        }
    }

    private static (string? name, string? controlType) ExtractInfo(AutomationElement? element)
    {
        if (element == null) return (null, null);

        string name = element.Current.Name?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return (null, null);

        string ctrlType = MapControlType(element.Current.ControlType);
        return (name, string.IsNullOrEmpty(ctrlType) ? null : ctrlType);
    }

    private static string MapControlType(ControlType ct)
    {
        if (ct == ControlType.Button)      return "Button";
        if (ct == ControlType.MenuItem)    return "MenuItem";
        if (ct == ControlType.CheckBox)    return "CheckBox";
        if (ct == ControlType.RadioButton) return "RadioButton";
        if (ct == ControlType.Edit)        return "Edit";
        if (ct == ControlType.ComboBox)    return "ComboBox";
        if (ct == ControlType.List)        return "List";
        if (ct == ControlType.ListItem)    return "ListItem";
        if (ct == ControlType.Hyperlink)   return "Hyperlink";
        if (ct == ControlType.Text)        return "Text";
        if (ct == ControlType.Tab)         return "Tab";
        if (ct == ControlType.TabItem)     return "TabItem";
        if (ct == ControlType.Tree)        return "Tree";
        if (ct == ControlType.TreeItem)    return "TreeItem";
        if (ct == ControlType.Menu)        return "Menu";
        if (ct == ControlType.MenuBar)     return "MenuBar";
        if (ct == ControlType.ToolBar)     return "ToolBar";
        if (ct == ControlType.Slider)      return "Slider";
        return ct?.LocalizedControlType ?? "";
    }
}
