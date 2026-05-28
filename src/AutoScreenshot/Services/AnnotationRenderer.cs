using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// AnnotationItem リストを System.Drawing で画像に焼き込む（FR-C）。
/// 元ファイルは変更しない。常にメモリ上の新しい Bitmap を返す。
/// </summary>
public static class AnnotationRenderer
{
    private const int BadgeRadius = 14;
    private const float ArrowHeadLength = 14f;
    private const float ArrowHeadAngle = 25f;
    private const int CalloutPadding = 6;

    /// <summary>
    /// アノテーション済みの Bitmap を返す。
    /// アノテーションが null または空の場合は元画像のコピーを返す。
    /// </summary>
    public static Bitmap? Render(string imagePath, IReadOnlyList<AnnotationItem>? annotations)
    {
        if (!File.Exists(imagePath)) return null;

        Bitmap src;
        try { src = new Bitmap(imagePath); }
        catch (Exception ex)
        {
            Log.Warning(ex, "アノテーション: 画像読み込み失敗 {Path}", imagePath);
            return null;
        }

        if (annotations == null || annotations.Count == 0) return src;

        var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            g.DrawImage(src, 0, 0);

            int badgeIndex = 1;
            foreach (var a in annotations)
            {
                Color color = ParseColor(a.Color);
                switch (a.Type)
                {
                    case "Number":
                        DrawBadge(g, a.X, a.Y, a.Label ?? (badgeIndex++).ToString(), color);
                        break;
                    case "Arrow":
                        DrawArrow(g, a.X, a.Y, a.X2, a.Y2, color);
                        break;
                    case "Rect":
                        DrawRect(g, a.X, a.Y, a.X2, a.Y2, color);
                        break;
                    case "Callout":
                        DrawCallout(g, a.X, a.Y, a.Label ?? "", color);
                        break;
                }
            }
        }
        src.Dispose();
        return bmp;
    }

    private static void DrawBadge(Graphics g, int cx, int cy, string label, Color color)
    {
        var rect = new RectangleF(cx - BadgeRadius, cy - BadgeRadius, BadgeRadius * 2, BadgeRadius * 2);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, rect);

        using var pen = new Pen(Color.White, 2);
        g.DrawEllipse(pen, rect);

        using var font = new Font("Arial", BadgeRadius * 0.85f, FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(label, font, Brushes.White, new RectangleF(cx - BadgeRadius, cy - BadgeRadius, BadgeRadius * 2, BadgeRadius * 2), sf);
    }

    private static void DrawArrow(Graphics g, int x1, int y1, int x2, int y2, Color color)
    {
        using var pen = new Pen(color, 3) { EndCap = LineCap.ArrowAnchor };
        var arrow = new AdjustableArrowCap(6, 6) { Filled = true };
        pen.CustomEndCap = arrow;
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    private static void DrawRect(Graphics g, int x1, int y1, int x2, int y2, Color color)
    {
        int left = Math.Min(x1, x2), top = Math.Min(y1, y2);
        int w = Math.Abs(x2 - x1), h = Math.Abs(y2 - y1);
        if (w < 2 || h < 2) return;

        using var fillBrush = new SolidBrush(Color.FromArgb(40, color));
        g.FillRectangle(fillBrush, left, top, w, h);

        using var pen = new Pen(color, 2.5f);
        pen.DashStyle = DashStyle.Dash;
        g.DrawRectangle(pen, left, top, w, h);
    }

    private static void DrawCallout(Graphics g, int cx, int cy, string text, Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        using var font = new Font("Yu Gothic UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
        SizeF textSize = g.MeasureString(text, font);
        float bw = textSize.Width + CalloutPadding * 2;
        float bh = textSize.Height + CalloutPadding * 2;

        // 吹き出し本体を右上に配置
        float bx = cx + 10, by = cy - bh - 10;

        using var bgBrush = new SolidBrush(Color.FromArgb(220, color));
        g.FillRectangle(bgBrush, bx, by, bw, bh);

        // テキスト
        g.DrawString(text, font, Brushes.White, new PointF(bx + CalloutPadding, by + CalloutPadding));

        // 吹き出し三角
        var triangle = new PointF[] { new(bx, by + bh), new(bx + 14, by + bh), new(cx, cy) };
        using var tribrush = new SolidBrush(Color.FromArgb(220, color));
        g.FillPolygon(tribrush, triangle);
    }

    private static Color ParseColor(string hex)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return Color.Red; }
    }
}
