using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AutoScreenshot.Models;

namespace AutoScreenshot.Resources;

/// <summary>アプリアイコンをコードで生成する。外部ICOファイル不要。</summary>
internal static class IconFactory
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>通常録画中のトレイアイコン（青）</summary>
    public static Icon CreateRecordingIcon(int size = 32) => CreateIcon(size, IconState.Recording);

    /// <summary>一時停止中のトレイアイコン（グレー）</summary>
    public static Icon CreatePausedIcon(int size = 32) => CreateIcon(size, IconState.Paused);

    /// <summary>撮影成功フラッシュ用アイコン（緑）</summary>
    public static Icon CreateCapturedIcon(int size = 32) => CreateIcon(size, IconState.Captured);

    /// <summary>バックグラウンド処理中のトレイアイコン（オレンジ）</summary>
    public static Icon CreateProcessingIcon(int size = 32) => CreateIcon(size, IconState.Processing);

    /// <summary>エラー状態のトレイアイコン（赤）</summary>
    public static Icon CreateErrorIcon(int size = 32) => CreateIcon(size, IconState.Error);

    // 後方互換エイリアス（NotifyIconWrapper 移行前の呼び出しが残らないよう削除予定）
    public static Icon CreateNormalIcon(int size = 32) => CreateRecordingIcon(size);

    private static Color BgColor(IconState state) => state switch
    {
        IconState.Recording  => Color.FromArgb(0,   120, 215),  // 青
        IconState.Paused     => Color.FromArgb(128, 128, 128),  // グレー
        IconState.Captured   => Color.FromArgb(16,  124,  16),  // 緑
        IconState.Processing => Color.FromArgb(202,  80,  16),  // オレンジ
        IconState.Error      => Color.FromArgb(197,  15,  31),  // 赤
        _                    => Color.FromArgb(0,   120, 215),
    };

    private static Icon CreateIcon(int size, IconState state)
    {
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        Color bg = BgColor(state);

        // 背景円
        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, size - 2, size - 2);

        // カメラ本体
        int m     = size / 8;
        int bodyX = m;
        int bodyY = size / 3;
        int bodyW = size - 2 * m;
        int bodyH = size - bodyY - m;

        using var bodyBrush = new SolidBrush(Color.White);
        g.FillRectangle(bodyBrush, bodyX, bodyY, bodyW, bodyH);

        // レンズ（背景色の円）
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

        // Processing: 右下に小ドット（処理中を視覚的に示す）
        if (state == IconState.Processing)
        {
            int dotR  = Math.Max(3, size / 8);
            int dotX  = size - dotR - 1;
            int dotY  = size - dotR - 1;
            using var dotBrush = new SolidBrush(Color.White);
            using var dotPen   = new Pen(bg, 1f);
            g.FillEllipse(dotBrush, dotX - dotR, dotY - dotR, dotR * 2, dotR * 2);
            g.DrawEllipse(dotPen,   dotX - dotR, dotY - dotR, dotR * 2, dotR * 2);
        }

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
