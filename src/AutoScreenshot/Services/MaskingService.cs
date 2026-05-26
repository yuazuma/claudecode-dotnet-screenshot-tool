using System.Drawing;
using System.Windows.Automation;
using System.Windows.Forms;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>UIAutomation によるパスワード欄検知・マスキング処理</summary>
public class MaskingService
{
    /// <summary>
    /// アクティブウィンドウ内のパスワード入力欄を検知し、
    /// 指定の Bitmap 上の対応領域を黒塗りにする
    /// </summary>
    public void ApplyMasking(Bitmap bmp, Rectangle screenBounds)
    {
        try
        {
            var focused = AutomationElement.FocusedElement;
            if (focused == null) return;

            MaskPasswordFields(bmp, focused, screenBounds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MaskingService: パスワード欄検知中にエラー");
        }
    }

    private static void MaskPasswordFields(Bitmap bmp, AutomationElement root, Rectangle screenBounds)
    {
        var condition = new PropertyCondition(
            AutomationElement.ControlTypeProperty, ControlType.Edit);

        var elements = root.FindAll(TreeScope.Subtree, condition);

        using var g = Graphics.FromImage(bmp);
        g.FillRectangle(Brushes.Black, 0, 0, 0, 0); // 初期化

        foreach (AutomationElement elem in elements)
        {
            try
            {
                // IsPassword プロパティで判定
                if (!(bool)elem.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty))
                    continue;

                var rect = elem.Current.BoundingRectangle;
                if (rect.IsEmpty) continue;

                // スクリーン座標をビットマップローカル座標に変換
                int x = (int)(rect.Left - screenBounds.Left);
                int y = (int)(rect.Top - screenBounds.Top);
                int w = (int)rect.Width;
                int h = (int)rect.Height;

                if (w <= 0 || h <= 0) continue;

                x = Math.Max(0, Math.Min(x, bmp.Width - 1));
                y = Math.Max(0, Math.Min(y, bmp.Height - 1));
                w = Math.Min(w, bmp.Width - x);
                h = Math.Min(h, bmp.Height - y);

                g.FillRectangle(Brushes.Black, x, y, w, h);
            }
            catch
            {
                // 個々の要素でのエラーは無視して続行
            }
        }
    }
}
