using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using AutoScreenshot.Models;
using Serilog;

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
                // WebP は別途ライブラリが必要 (Phase 4 で対応)
                // 暫定的に PNG で保存
                Log.Warning("WebP は未対応のため PNG で保存します");
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                break;

            default: // PNG
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                break;
        }

        return ms.ToArray();
    }

    private static ImageCodecInfo? GetCodecInfo(string mimeType) =>
        ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == mimeType);
}
