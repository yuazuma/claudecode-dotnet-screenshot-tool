using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>1フレーム分の Bitmap を生成するサービス。波紋・矩形枠・テロップを合成する。</summary>
public class FrameRenderer
{
    // トリガー種別ごとの強調色（CaptureService.DrawImageOverlay と統一）
    private static readonly Dictionary<TriggerType, Color> TriggerColors = new()
    {
        [TriggerType.MouseLeftClick]  = Color.FromArgb(255, 70,  0),
        [TriggerType.MouseRightClick] = Color.FromArgb(0,  120, 255),
        [TriggerType.MouseMiddleClick]= Color.FromArgb(0,  190,  80),
        [TriggerType.MouseDragDrop]   = Color.FromArgb(255, 165,  0),
        [TriggerType.MouseWheel]      = Color.FromArgb(150, 0,  200),
        [TriggerType.Keyboard]        = Color.FromArgb(0,  190,  80),
        [TriggerType.ActiveWindowChange] = Color.FromArgb(140, 0,  255),
        [TriggerType.ScreenDiff]      = Color.FromArgb(255, 165,  0),
        [TriggerType.ManualCapture]   = Color.FromArgb(0,   80, 200),
    };

    private readonly VideoGenConfig _cfg;

    public FrameRenderer(VideoGenConfig cfg) => _cfg = cfg;

    /// <summary>ステップ画像を読み込み、強調表示・テロップを合成した Bitmap のリストを返す。
    /// クリック系は波紋フレームを 3 枚追加する（先頭が元画像、以降が波紋）。</summary>
    public List<Bitmap> Render(ManualStep step, Size targetSize)
    {
        Bitmap? source = LoadImage(step.ImagePath);
        if (source == null)
        {
            // 画像なし → 黒フレームで代用
            source = new Bitmap(Math.Max(targetSize.Width, 1), Math.Max(targetSize.Height, 1));
            using var g = Graphics.FromImage(source);
            g.Clear(Color.Black);
        }

        Bitmap scaled = ApplyResolution(source, targetSize);
        if (!ReferenceEquals(source, scaled)) source.Dispose();

        var color  = TriggerColors.GetValueOrDefault(step.TriggerType, Color.White);
        var frames = new List<Bitmap>();

        // --- 基本フレーム（矩形枠 + テロップ）---
        Bitmap baseFrame = (Bitmap)scaled.Clone();
        using (var g = Graphics.FromImage(baseFrame))
        {
            DrawRectangle(g, step, scaled.Size, color);
            DrawTelop(g, step, scaled.Size);
        }
        frames.Add(baseFrame);

        // --- 波紋フレーム (FR-V04-1) ---
        bool isClick = step.TriggerType is TriggerType.MouseLeftClick
            or TriggerType.MouseRightClick or TriggerType.MouseMiddleClick
            or TriggerType.MouseDragDrop;
        if (_cfg.DrawRipple && isClick)
        {
            for (int ri = 1; ri <= 3; ri++)
            {
                Bitmap rippleFrame = (Bitmap)scaled.Clone();
                using var g = Graphics.FromImage(rippleFrame);
                DrawRipple(g, step, scaled.Size, color, ri);
                DrawRectangle(g, step, scaled.Size, color);
                DrawTelop(g, step, scaled.Size);
                frames.Add(rippleFrame);
            }
        }

        scaled.Dispose();
        return frames;
    }

    // ── 画像読み込み（WebP は ImageSharp でデコード）────────────────────────────
    private static Bitmap? LoadImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            if (path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                using var img = SixLabors.ImageSharp.Image.Load(path);
                using var ms  = new MemoryStream();
                img.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                ms.Position = 0;
                return new Bitmap(ms);
            }
            return new Bitmap(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "フレーム画像の読み込みに失敗: {Path}", path);
            return null;
        }
    }

    // ── 解像度変換（アスペクト比保持 + レターボックス）─────────────────────────
    private Bitmap ApplyResolution(Bitmap src, Size target)
    {
        if (target.Width <= 0 || target.Height <= 0) return src;
        if (src.Width == target.Width && src.Height == target.Height) return src;

        var dest = new Bitmap(target.Width, target.Height);
        using var g = Graphics.FromImage(dest);
        g.Clear(Color.Black);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        double ratioW = (double)target.Width  / src.Width;
        double ratioH = (double)target.Height / src.Height;
        double ratio  = Math.Min(ratioW, ratioH);
        int dw = (int)(src.Width  * ratio);
        int dh = (int)(src.Height * ratio);
        int dx = (target.Width  - dw) / 2;
        int dy = (target.Height - dh) / 2;
        g.DrawImage(src, dx, dy, dw, dh);
        return dest;
    }

    // ── 波紋アニメーション描画（FR-V04-1）────────────────────────────────────
    private static void DrawRipple(Graphics g, ManualStep step, Size size, Color color, int phase)
    {
        // phase 1〜3 → 半径 30/55/80 px、透明度 200/130/60
        int radius  = phase * 25 + 5;
        int alpha   = 220 - phase * 60;
        var pt      = ScaleCursorPosition(step.CursorPosition, step, size);

        using var pen = new Pen(Color.FromArgb(alpha, color), 3f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawEllipse(pen,
            pt.X - radius, pt.Y - radius,
            radius * 2,    radius * 2);
    }

    // ── 矩形枠描画（FR-V04-2）────────────────────────────────────────────────
    private void DrawRectangle(Graphics g, ManualStep step, Size frameSize, Color color)
    {
        if (!_cfg.DrawRectangle) return;
        // UIA 境界矩形は ManualStep に保持されていないため、カーソル周辺 ±30px の矩形を代用
        // （詳細設計フェーズで UIA Rect を ManualStep に追加した場合に差し替え可能）
        var pt = ScaleCursorPosition(step.CursorPosition, step, frameSize);
        int margin = 30;
        var rect   = new Rectangle(pt.X - margin, pt.Y - margin, margin * 2, margin * 2);
        rect.Intersect(new Rectangle(0, 0, frameSize.Width, frameSize.Height));
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using var pen = new Pen(Color.FromArgb(200, color), 2f) { DashStyle = DashStyle.Dash };
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawRectangle(pen, rect);
    }

    // ── テロップ（字幕帯）描画（FR-V05）──────────────────────────────────────
    private void DrawTelop(Graphics g, ManualStep step, Size frameSize)
    {
        if (!_cfg.ShowTelop) return;

        var lines = BuildTelopLines(step);
        if (lines.Count == 0) return;

        float fontSize = Math.Max(8, _cfg.TelopFontSize);
        using var font = new Font("Yu Gothic UI", fontSize, FontStyle.Regular, GraphicsUnit.Point);
        float lineH = font.GetHeight(g);
        float totalH = lineH * lines.Count + 16;
        float y0    = frameSize.Height - totalH;

        // 半透明背景帯
        using var bgBrush = new SolidBrush(Color.FromArgb(_cfg.TelopBgAlpha, 0, 0, 0));
        g.FillRectangle(bgBrush, 0, y0, frameSize.Width, totalH);

        using var textBrush = new SolidBrush(Color.White);
        using var outlinePen = new Pen(Color.Black, 1f);
        var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };

        for (int i = 0; i < lines.Count; i++)
        {
            float ty = y0 + 8 + i * lineH;
            var rect = new RectangleF(8, ty, frameSize.Width - 16, lineH);
            g.DrawString(lines[i], font, textBrush, rect, fmt);
        }
    }

    private List<string> BuildTelopLines(ManualStep step)
    {
        var lines = new List<string>();

        if (_cfg.TelopShowTimestamp)
            lines.Add(step.Timestamp.ToString("yyyy/MM/dd HH:mm:ss"));

        if (_cfg.TelopShowEventLabel)
        {
            string label = step.TriggerType switch
            {
                TriggerType.MouseLeftClick   => "クリック（左）",
                TriggerType.MouseRightClick  => "クリック（右）",
                TriggerType.MouseMiddleClick => "クリック（中）",
                TriggerType.MouseDragDrop    => "ドラッグ＆ドロップ",
                TriggerType.MouseWheel       => "ホイール",
                TriggerType.Keyboard         => "キー入力",
                TriggerType.ActiveWindowChange => "ウィンドウ切替",
                TriggerType.ScreenDiff       => "差分検知",
                TriggerType.ManualCapture    => "手動撮影",
                _ => step.TriggerType.ToString(),
            };
            if (!string.IsNullOrWhiteSpace(step.UiElementName))
                label += $"  [{step.UiElementName}]";
            lines.Add(label);
        }

        if (_cfg.TelopShowInputText && !string.IsNullOrWhiteSpace(step.InputText))
            lines.Add($"入力: {step.InputText}");
        else if (_cfg.TelopShowInputText && !string.IsNullOrWhiteSpace(step.KeyCodes))
            lines.Add($"キー: {step.KeyCodes}");

        if (_cfg.TelopShowDescription)
        {
            string desc = step.DescriptionLlm ?? step.DescriptionRuleBased;
            if (!string.IsNullOrWhiteSpace(desc))
                lines.Add(desc);
        }

        return lines;
    }

    // ── カーソル座標をフレームサイズにスケール変換 ──────────────────────────
    private static Point ScaleCursorPosition(Point cursor, ManualStep step, Size frameSize)
    {
        // ManualStep にはモニター解像度情報がないため、座標はそのまま使用
        // 解像度変換後にフレームからはみ出す可能性があるため Clamp する
        return new Point(
            Math.Clamp(cursor.X, 0, Math.Max(frameSize.Width  - 1, 0)),
            Math.Clamp(cursor.Y, 0, Math.Max(frameSize.Height - 1, 0)));
    }
}
