using AutoScreenshot.Models;

namespace AutoScreenshot.Services;

/// <summary>ルールベースでステップの操作目的テキストを生成する</summary>
public static class RuleBasedDescriber
{
    public static string Describe(ManualStep step)
    {
        string ui = step.UiElementName ?? $"座標 ({step.CursorPosition.X}, {step.CursorPosition.Y})";
        string ctrl = step.UiControlType ?? "";

        return step.TriggerType switch
        {
            TriggerType.MouseLeftClick => ctrl switch
            {
                "MenuItem"      => $"「{ui}」メニューを選択しました。",
                "Hyperlink"     => $"「{ui}」リンクをクリックしました。",
                "CheckBox"      => $"「{ui}」チェックボックスをクリックしました。",
                "RadioButton"   => $"「{ui}」ラジオボタンを選択しました。",
                _               => $"「{ui}」をクリックしました。",
            },
            TriggerType.MouseRightClick =>
                $"「{ui}」を右クリックしてコンテキストメニューを開きました。",
            TriggerType.MouseMiddleClick =>
                $"「{ui}」を中クリックしました。",
            TriggerType.MouseDragDrop =>
                $"「{ui}」をドラッグしました。",
            TriggerType.Keyboard => BuildKeyboardDescription(step, ui),
            TriggerType.ActiveWindowChange =>
                $"「{step.WindowTitle}」({step.ProcessName}) に切り替えました。",
            _ => $"操作が記録されました。({step.TriggerType})",
        };
    }

    private static string BuildKeyboardDescription(ManualStep step, string ui)
    {
        if (!string.IsNullOrEmpty(step.InputText) && !string.IsNullOrEmpty(step.KeyCodes))
            return $"「{ui}」に「{step.InputText}」と入力しました。（キー: {step.KeyCodes}）";
        if (!string.IsNullOrEmpty(step.InputText))
            return $"「{ui}」に「{step.InputText}」と入力しました。";
        if (!string.IsNullOrEmpty(step.KeyCodes))
        {
            string kc = step.KeyCodes;
            if (kc.Contains("Enter", StringComparison.OrdinalIgnoreCase))
                return $"「{ui}」で Enter キーを押しました。";
            return $"{kc} を実行しました。";
        }
        return $"「{ui}」にキー入力しました。";
    }
}
