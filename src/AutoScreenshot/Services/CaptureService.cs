using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AutoScreenshot.Models;
using Serilog;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using ISImage = SixLabors.ImageSharp.Image;

namespace AutoScreenshot.Services;

/// <summary>スクリーンキャプチャ処理（全モニタ対応）</summary>
public class CaptureService
{
    /// <summary>接続中の全モニタを撮影し、Bitmap のリストを返す</summary>
    public List<(Bitmap Image, int MonitorIndex, Rectangle Bounds)> CaptureAllScreens()
    {
        var results = new List<(Bitmap, int, Rectangle)>();
        var screens = Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var bmp = CaptureScreen(screen.Bounds);
            results.Add((bmp, i + 1, screen.Bounds));
        }

        return results;
    }

    /// <summary>指定した 0 始まりインデックスのモニタのみ撮影する</summary>
    public List<(Bitmap Image, int MonitorIndex, Rectangle Bounds)> CaptureScreensByIndex(IReadOnlyList<int> zeroBasedIndices)
    {
        var results = new List<(Bitmap, int, Rectangle)>();
        var screens = Screen.AllScreens;

        foreach (int i in zeroBasedIndices)
        {
            if (i < 0 || i >= screens.Length) continue;
            var screen = screens[i];
            var bmp = CaptureScreen(screen.Bounds);
            results.Add((bmp, i + 1, screen.Bounds));
        }

        return results;
    }

    // RDP セッション検出結果をキャッシュ（プロセス起動後は変化しない）
    private static readonly bool _isRdpSession =
        System.Windows.Forms.SystemInformation.TerminalServerSession;
    private static readonly bool _wgcSupported = WgcCapture.IsSupported();

    /// <summary>指定領域をキャプチャする</summary>
    public Bitmap CaptureScreen(Rectangle bounds)
    {
        // RDP セッションでは GDI CopyFromScreen がデスクトップ壁紙色しか返さないため
        // Windows.Graphics.Capture API (DWM コンポジター経由) を使用する。
        if (_isRdpSession && _wgcSupported)
        {
            var wgcBmp = WgcCapture.Capture(bounds);
            if (wgcBmp != null) return wgcBmp;
            Serilog.Log.Warning("WGC フォールバック: GDI CopyFromScreen を使用");
        }

        var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
        return bmp;
    }

    /// <summary>差分検知用の縮小サムネイルを取得する</summary>
    public Bitmap CaptureThumbnail(Rectangle bounds, int thumbWidth = 320, int thumbHeight = 180)
    {
        using var full = CaptureScreen(bounds);
        var thumb = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(thumb);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
        g.DrawImage(full, 0, 0, thumbWidth, thumbHeight);
        return thumb;
    }

    /// <summary>画像をファイル形式に応じてエンコードし、バイト配列で返す</summary>
    /// <param name="lossless">
    /// true の場合: JPEG は quality=100、WebP は lossless モードで保存する（証跡用途）。
    /// false の場合: jpegQuality パラメーターを使用する（通常のサムネイル等）。
    /// </param>
    public byte[] Encode(Bitmap bmp, Models.ImageFormat format, int jpegQuality = 85, bool lossless = false)
    {
        using var ms = new System.IO.MemoryStream();

        switch (format)
        {
            case Models.ImageFormat.Jpeg:
            {
                int quality = lossless ? 100 : jpegQuality;
                var jpegParams = new EncoderParameters(1);
                jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                var jpegCodec = GetCodecInfo("image/jpeg");
                if (jpegCodec != null)
                    bmp.Save(ms, jpegCodec, jpegParams);
                else
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                break;
            }

            case Models.ImageFormat.WebP:
            {
                using var pngBuf = new System.IO.MemoryStream();
                bmp.Save(pngBuf, System.Drawing.Imaging.ImageFormat.Png);
                pngBuf.Position = 0;
                using var img = ISImage.Load<Rgba32>(pngBuf);
                if (lossless)
                    img.Save(ms, new WebpEncoder { FileFormat = SixLabors.ImageSharp.Formats.Webp.WebpFileFormatType.Lossless });
                else
                    img.Save(ms, new WebpEncoder { Quality = jpegQuality });
                break;
            }

            default: // PNG (常にロスレス)
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                break;
        }

        return ms.ToArray();
    }

    private static ImageCodecInfo? GetCodecInfo(string mimeType) =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == mimeType);

    /// <summary>カーソル位置にトリガー種別に応じた色付きマーカーを描画する (F-06-08/09)</summary>
    public void DrawImageOverlay(Bitmap bmp, System.Drawing.Point cursor,
        Rectangle monitorBounds, Models.TriggerType trigger)
    {
        int localX = cursor.X - monitorBounds.X;
        int localY = cursor.Y - monitorBounds.Y;
        if (localX < 0 || localY < 0 || localX >= bmp.Width || localY >= bmp.Height) return;

        Color markerColor = trigger switch
        {
            Models.TriggerType.MouseLeftClick     => Color.Red,
            Models.TriggerType.MouseRightClick    => Color.Blue,
            Models.TriggerType.MouseMiddleClick   => Color.LimeGreen,
            Models.TriggerType.MouseDragDrop      => Color.Orange,
            Models.TriggerType.MouseWheel         => Color.MediumPurple,
            Models.TriggerType.Keyboard           => Color.Yellow,
            Models.TriggerType.ActiveWindowChange => Color.Cyan,
            Models.TriggerType.ScreenDiff         => Color.DeepPink,
            _                                     => Color.White,
        };

        const int r = 20;
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 半透明の塗りつぶし円 (F-06-08)
        using var fill = new SolidBrush(Color.FromArgb(70, markerColor));
        g.FillEllipse(fill, localX - r, localY - r, r * 2, r * 2);

        // 色付き枠線 (F-06-09)
        using var border = new Pen(markerColor, 3f);
        g.DrawEllipse(border, localX - r, localY - r, r * 2, r * 2);

        // クロスヘア
        using var cross = new Pen(markerColor, 2f);
        g.DrawLine(cross, localX - r - 6, localY, localX - 6, localY);
        g.DrawLine(cross, localX + 6, localY, localX + r + 6, localY);
        g.DrawLine(cross, localX, localY - r - 6, localX, localY - 6);
        g.DrawLine(cross, localX, localY + 6, localX, localY + r + 6);
    }

    /// <summary>画像の左下にタイムスタンプを焼き込む</summary>
    public void BurnTimestamp(Bitmap bmp, DateTime timestamp)
    {
        using var g = Graphics.FromImage(bmp);
        using var font = new Font("Consolas", 10, FontStyle.Regular);
        string text = timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        var size = g.MeasureString(text, font);
        float x = 8f;
        float y = bmp.Height - size.Height - 8f;
        using var bg = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        g.FillRectangle(bg, x - 2, y - 2, size.Width + 4, size.Height + 4);
        g.DrawString(text, font, Brushes.White, x, y);
    }
}
