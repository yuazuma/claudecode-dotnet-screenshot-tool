using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace AutoScreenshot.Services;

/// <summary>画面差分検知（縮小画像比較）</summary>
public class DiffDetector : IDisposable
{
    private readonly CaptureService _captureService;
    private readonly Dictionary<int, Bitmap> _prevThumbs = [];

    public DiffDetector(CaptureService captureService)
    {
        _captureService = captureService;
    }

    /// <summary>
    /// 全モニタを走査し、差分率が閾値を超えたモニタのインデックスリストを返す
    /// </summary>
    public List<int> DetectChangedScreens(double thresholdPercent)
    {
        var changed = new List<int>();
        var screens = Screen.AllScreens;

        for (int i = 0; i < screens.Length; i++)
        {
            var thumb = _captureService.CaptureThumbnail(screens[i].Bounds);

            if (_prevThumbs.TryGetValue(i, out var prev))
            {
                double diff = CalcDiffRatio(prev, thumb);
                if (diff >= thresholdPercent / 100.0)
                {
                    changed.Add(i);
                    prev.Dispose();
                    _prevThumbs[i] = thumb;
                }
                else
                {
                    thumb.Dispose();
                }
            }
            else
            {
                _prevThumbs[i] = thumb;
            }
        }

        return changed;
    }

    private static unsafe double CalcDiffRatio(Bitmap a, Bitmap b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
            return 1.0;

        var dataA = a.LockBits(new Rectangle(0, 0, a.Width, a.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var dataB = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        long diffPixels = 0;
        long total = a.Width * a.Height;

        byte* pA = (byte*)dataA.Scan0;
        byte* pB = (byte*)dataB.Scan0;

        for (long p = 0; p < total; p++)
        {
            int dr = Math.Abs(pA[p * 4 + 2] - pB[p * 4 + 2]);
            int dg = Math.Abs(pA[p * 4 + 1] - pB[p * 4 + 1]);
            int db = Math.Abs(pA[p * 4 + 0] - pB[p * 4 + 0]);

            if (dr + dg + db > 30)
                diffPixels++;
        }

        a.UnlockBits(dataA);
        b.UnlockBits(dataB);

        return (double)diffPixels / total;
    }

    public void Dispose()
    {
        foreach (var bmp in _prevThumbs.Values)
            bmp.Dispose();
        _prevThumbs.Clear();
    }
}
