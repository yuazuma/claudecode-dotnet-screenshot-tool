using System.Drawing;
using System.Drawing.Imaging;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>スクリーンショット画像から JPEG サムネイルを非同期生成する</summary>
public static class ThumbnailService
{
    /// <summary>
    /// imagePath の画像からサムネイルを生成して thumbPath に保存する。
    /// 失敗しても例外を投げず false を返す（撮影継続を妨げない）。
    /// </summary>
    public static async Task<bool> GenerateAsync(string imagePath, string thumbPath, int maxWidth = 320)
    {
        try
        {
            byte[] data = await File.ReadAllBytesAsync(imagePath);
            await Task.Run(() =>
            {
                using var ms = new System.IO.MemoryStream(data);
                using var src = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);

                int w = src.Width;
                int h = src.Height;
                if (w > maxWidth)
                {
                    h = (int)((double)h * maxWidth / w);
                    w = maxWidth;
                }

                using var thumb = new Bitmap(w, h);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, w, h);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(thumbPath)!);

                var encoder = GetJpegEncoder();
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
                thumb.Save(thumbPath, encoder, encoderParams);
            });
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "サムネイル生成失敗: {Path}", imagePath);
            return false;
        }
    }

    private static ImageCodecInfo GetJpegEncoder()
    {
        return ImageCodecInfo.GetImageEncoders()
            .First(c => c.MimeType == "image/jpeg");
    }
}
