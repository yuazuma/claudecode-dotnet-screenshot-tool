using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>Windows.Media.Ocr でスクリーンショット上の近傍テキストを認識する</summary>
public class OcrService
{
    private readonly OcrEngine? _engine;

    public OcrService()
    {
        try
        {
            _engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (_engine == null)
                Log.Warning("OcrService: 言語パックが見つかりません。OCR フォールバックは使用できません。");
            else
                Log.Debug("OcrService 初期化完了 (言語: {Lang})", _engine.RecognizerLanguage.DisplayName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OcrService 初期化失敗");
        }
    }

    /// <summary>
    /// 保存済みスクリーンショットのカーソル周辺 ±40px を OCR して近傍テキストを返す。
    /// 失敗時は null を返す。
    /// </summary>
    public async Task<string?> RecognizeNearbyTextAsync(
        string imagePath,
        System.Drawing.Point cursorPos,
        System.Drawing.Rectangle monitorBounds)
    {
        if (_engine == null) return null;
        if (!File.Exists(imagePath)) return null;

        try
        {
            // スクリーン座標 → ビットマップローカル座標
            int localX = cursorPos.X - monitorBounds.X;
            int localY = cursorPos.Y - monitorBounds.Y;

            const int margin = 40;

            using var original = new System.Drawing.Bitmap(imagePath);
            int imgW = original.Width;
            int imgH = original.Height;

            int cropX = Math.Max(0, localX - margin);
            int cropY = Math.Max(0, localY - margin);
            int cropW = Math.Min(margin * 2, imgW - cropX);
            int cropH = Math.Min(margin * 2, imgH - cropY);

            if (cropW <= 0 || cropH <= 0) return null;
            if (localX < 0 || localX >= imgW || localY < 0 || localY >= imgH) return null;

            var cropRect = new System.Drawing.Rectangle(cropX, cropY, cropW, cropH);
            using var cropped = original.Clone(cropRect, original.PixelFormat);

            // Bitmap → byte[] (PNG) → InMemoryRandomAccessStream → SoftwareBitmap
            using var ms = new MemoryStream();
            cropped.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            byte[] pngBytes = ms.ToArray();

            using var ras = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(ras.GetOutputStreamAt(0));
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync();
            ras.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(ras);
            var softBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            var result = await _engine.RecognizeAsync(softBitmap);
            string text = result.Text.Trim();

            Log.Debug("OCR 結果 ({X},{Y}): \"{Text}\"", localX, localY, text);

            if (string.IsNullOrWhiteSpace(text)) return null;

            // 最初の非空行を UI 名として使用（最大 60 文字）
            string? line = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
                ?.Trim();

            return line?.Length > 60 ? line[..60] : line;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "OCR 失敗: {Path}", imagePath);
            return null;
        }
    }
}
