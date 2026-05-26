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

    /// <summary>指定領域をキャプチャする</summary>
    public Bitmap CaptureScreen(Rectangle bounds)
    {
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
    public byte[] Encode(Bitmap bmp, Models.ImageFormat format, int jpegQuality = 85)
    {
        using var ms = new System.IO.MemoryStream();

        switch (format)
        {
            case Models.ImageFormat.Jpeg:
                var jpegParams = new EncoderParameters(1);
                jpegParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)jpegQuality);
                var jpegCodec = GetCodecInfo("image/jpeg");
                if (jpegCodec != null)
                    bmp.Save(ms, jpegCodec, jpegParams);
                else
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                break;

            case Models.ImageFormat.WebP:
            {
                using var pngBuf = new System.IO.MemoryStream();
                bmp.Save(pngBuf, System.Drawing.Imaging.ImageFormat.Png);
                pngBuf.Position = 0;
                using var img = ISImage.Load<Rgba32>(pngBuf);
                img.Save(ms, new WebpEncoder { Quality = jpegQuality });
                break;
            }

            default: // PNG
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                break;
        }

        return ms.ToArray();
    }

    private static ImageCodecInfo? GetCodecInfo(string mimeType) =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == mimeType);

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
