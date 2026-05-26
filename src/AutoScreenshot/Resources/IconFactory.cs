using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AutoScreenshot.Resources;

/// <summary>アプリアイコンをコードで生成する。外部ICOファイル不要。</summary>
internal static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>通常状態のトレイアイコン (青 / カメラ型)</summary>
    public static Icon CreateNormalIcon(int size = 32) => CreateIcon(size, active: true);

    /// <summary>一時停止中のトレイアイコン (グレー / カメラ型)</summary>
    public static Icon CreatePausedIcon(int size = 32) => CreateIcon(size, active: false);

    private static Icon CreateIcon(int size, bool active)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // 背景円
        Color bg = active ? Color.FromArgb(0, 120, 215) : Color.FromArgb(128, 128, 128);
        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // カメラ本体
        int m = size / 8;
        int bodyX = m;
        int bodyY = size / 3;
        int bodyW = size - 2 * m;
        int bodyH = size - bodyY - m;

        using var bodyBrush = new SolidBrush(Color.White);
        using var bodyPen   = new Pen(Color.White, 1);
        g.FillRectangle(bodyBrush, bodyX, bodyY, bodyW, bodyH);

        // レンズ (青い円)
        int lensSize = bodyH - 4;
        int lensX    = bodyX + (bodyW - lensSize) / 2;
        int lensY    = bodyY + 2;
        using var lensBrush = new SolidBrush(bg);
        g.FillEllipse(lensBrush, lensX, lensY, lensSize, lensSize);

        // ビューファインダー突起
        int finderW = size / 4;
        int finderH = size / 10;
        int finderX = bodyX + size / 8;
        int finderY = bodyY - finderH;
        g.FillRectangle(bodyBrush, finderX, finderY, finderW, finderH);

        // HICON 取得 → Icon コピー → DestroyIcon で解放
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            return Icon.FromHandle(hIcon).Clone() as Icon
                ?? throw new InvalidOperationException("Icon の生成に失敗しました");
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
