using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// Windows.Graphics.Capture API を使用したスクリーンキャプチャ。
/// GDI CopyFromScreen が機能しない RDP セッション (Azure Windows Server 等) で使用する。
/// DWM コンポジターの出力を直接読み取るため RDP でも実際の画面内容を取得できる。
/// </summary>
internal static class WgcCapture
{
    // ── COM インターフェイス ─────────────────────────────────────────────────────

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow([In] IntPtr window,   [In] ref Guid iid);
        IntPtr CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid);
    }

    /// <summary>
    /// Windows 11 (Build 22000+) で追加された IsBorderRequired プロパティを持つ
    /// GraphicsCaptureSession の COM インターフェイス。
    /// TFM 17763 では型定義に存在しないため COM インターフェイス経由で直接設定する。
    /// </summary>
    [ComImport, Guid("BFEE7A93-F3BD-4A41-A011-2F5A28E62735")]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    private interface IGraphicsCaptureSession3
    {
        [return: MarshalAs(UnmanagedType.Bool)] bool get_IsBorderRequired();
        void put_IsBorderRequired([MarshalAs(UnmanagedType.Bool)] bool value);
    }

    // ── P/Invoke ────────────────────────────────────────────────────────────────

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        IntPtr activatableClassId,   // HSTRING
        ref Guid iid,
        out IntPtr factory);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        int length,
        out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter, int driverType, IntPtr Software, uint Flags,
        IntPtr pFeatureLevels, uint FeatureLevels, uint SDKVersion,
        out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct POINT(int X, int Y);

    // ── 定数 / GUID ──────────────────────────────────────────────────────────────

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int  D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int  D3D_DRIVER_TYPE_WARP     = 5;

    private static readonly Guid IID_IDXGIDevice         = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    private const string CLSID_GraphicsCaptureItem =
        "Windows.Graphics.Capture.GraphicsCaptureItem";

    // ── 公開 API ─────────────────────────────────────────────────────────────────

    public static bool IsSupported()
    {
        try { return GraphicsCaptureSession.IsSupported(); }
        catch { return false; }
    }

    /// <summary>
    /// 指定した画面領域を WGC でキャプチャして Bitmap を返す。
    /// 失敗した場合は null を返す（呼び出し元で GDI 等にフォールバックする）。
    /// </summary>
    public static Bitmap? Capture(Rectangle monitorBounds)
    {
        try
        {
            return CaptureAsync(monitorBounds).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "WGC キャプチャ失敗 ({W}x{H})", monitorBounds.Width, monitorBounds.Height);
            return null;
        }
    }

    // ── 内部実装 ─────────────────────────────────────────────────────────────────

    private static async Task<Bitmap?> CaptureAsync(Rectangle bounds)
    {
        // 1. HMONITOR
        var hMon = MonitorFromPoint(
            new POINT(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2),
            MONITOR_DEFAULTTONEAREST);
        if (hMon == IntPtr.Zero) return null;

        // 2. GraphicsCaptureItem (HMONITOR から)
        var item = CreateCaptureItemForMonitor(hMon);
        var size = new SizeInt32(bounds.Width, bounds.Height);

        // 3. D3D11 デバイス (ハードウェア → WARP フォールバック)
        if (!TryCreateD3D11Device(out var d3dPtr) || d3dPtr == IntPtr.Zero) return null;

        // 4. WinRT IDirect3DDevice ラッパー
        var winRtDevice = CreateWinRtDevice(d3dPtr);

        // 5. フレームプール (CreateFreeThreaded: スレッドプール / 非ディスパッチャーから安全)
        using var pool    = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winRtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);

        // 6. フレームを 1 枚取得 (最大 3 秒)
        var tcs = new TaskCompletionSource<Direct3D11CaptureFrame?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var session = pool.CreateCaptureSession(item);
        TrySuppressBorder(session);  // Windows 11+ の黄色いキャプチャ通知ボーダーを非表示
        pool.FrameArrived += (s, _) => tcs.TrySetResult(s.TryGetNextFrame());
        session.StartCapture();

        using var cts = new CancellationTokenSource(3000);
        cts.Token.Register(() => tcs.TrySetResult(null));
        var frame = await tcs.Task.ConfigureAwait(false);
        session.Dispose(); // IClosable::Close() の .NET マッピング

        if (frame == null) return null;
        using (frame)
        {
            // 7. IDirect3DSurface → SoftwareBitmap (D3D11 COM 不要)
            var soft = await SoftwareBitmap.CreateCopyFromSurfaceAsync(
                frame.Surface, BitmapAlphaMode.Premultiplied).AsTask().ConfigureAwait(false);
            using (soft)
                return await SoftBitmapToGdiAsync(soft).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Windows 11 (Build 22000+) で表示されるキャプチャ通知の黄色ボーダーを抑制する。
    /// IGraphicsCaptureSession3 を COM QI で取得して put_IsBorderRequired(false) を呼ぶ。
    /// TFM 17763 では型定義が存在しないため COM 直接呼び出し。非対応 OS では何もしない。
    /// </summary>
    private static void TrySuppressBorder(GraphicsCaptureSession session)
    {
        try
        {
            // (object) 経由でコンパイル時型チェックを回避し実行時 QI（Windows 11+ 対応時のみ成功）
            if ((object)session is IGraphicsCaptureSession3 s3)
            {
                s3.put_IsBorderRequired(false);
                Log.Debug("WGC: IsBorderRequired=false に設定");
                return;
            }
        }
        catch { /* QI 失敗: 非対応 OS は無視 */ }

        // フォールバック: IWinRTObject 経由で native ポインターを取得して vtable 直接呼び出し
        try
        {
            if (session is not WinRT.IWinRTObject winRtObj) return;
            var nativePtr = winRtObj.NativeObject.ThisPtr;
            var iid3 = new Guid("BFEE7A93-F3BD-4A41-A011-2F5A28E62735");
            if (Marshal.QueryInterface(nativePtr, ref iid3, out var pSession3) < 0 || pSession3 == IntPtr.Zero)
                return;
            try
            {
                unsafe
                {
                    // WinRT vtable: [0-2]=IUnknown, [3-5]=IInspectable, [6]=get, [7]=put
                    var vtblPtr = *(IntPtr*)(void*)pSession3;
                    var vtbl    = (void**)(void*)vtblPtr;
                    ((delegate* unmanaged[Stdcall]<IntPtr, bool, int>)vtbl[7])(pSession3, false);
                }
                Log.Debug("WGC: IsBorderRequired=false (vtable経由) に設定");
            }
            finally { Marshal.Release(pSession3); }
        }
        catch { /* 非対応 OS では無視 */ }
    }

    private static GraphicsCaptureItem CreateCaptureItemForMonitor(IntPtr hMon)
    {
        var iid = typeof(IGraphicsCaptureItemInterop).GUID;
        WindowsCreateString(CLSID_GraphicsCaptureItem, CLSID_GraphicsCaptureItem.Length, out var hs);
        int hr;
        IntPtr factoryPtr;
        try   { hr = RoGetActivationFactory(hs, ref iid, out factoryPtr); }
        finally { WindowsDeleteString(hs); }
        if (hr < 0 || factoryPtr == IntPtr.Zero)
            throw new COMException("IGraphicsCaptureItemInterop の取得に失敗", hr);

        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
        Marshal.Release(factoryPtr);

        var guid    = IID_IGraphicsCaptureItem;
        var itemPtr = interop.CreateForMonitor(hMon, ref guid);
        if (itemPtr == IntPtr.Zero)
            throw new InvalidOperationException("CreateForMonitor が IntPtr.Zero を返しました");

        // CsWinRT の ABI → マネージド型変換
        var item = WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPtr);
        Marshal.Release(itemPtr);
        return item;
    }

    private static bool TryCreateD3D11Device(out IntPtr devicePtr)
    {
        // ハードウェア優先、失敗したら WARP (ソフトウェアレンダラー)
        if (D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero,
            0, IntPtr.Zero, 0, 7, out devicePtr, out _, out _) == 0)
            return true;
        return D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_WARP, IntPtr.Zero,
            0, IntPtr.Zero, 0, 7, out devicePtr, out _, out _) == 0;
    }

    private static IDirect3DDevice? CreateWinRtDevice(IntPtr d3dPtr)
    {
        var dxgiGuid = IID_IDXGIDevice;
        if (Marshal.QueryInterface(d3dPtr, ref dxgiGuid, out var dxgiPtr) < 0)
        { Marshal.Release(d3dPtr); return null; }

        try
        {
            if (CreateDirect3D11DeviceFromDXGIDevice(dxgiPtr, out var rtPtr) < 0 || rtPtr == IntPtr.Zero)
                return null;
            var dev = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(rtPtr);
            Marshal.Release(rtPtr);
            return dev;
        }
        finally
        {
            Marshal.Release(dxgiPtr);
            Marshal.Release(d3dPtr);
        }
    }

    /// <summary>
    /// SoftwareBitmap → BMP エンコード → System.Drawing.Bitmap
    /// IMemoryBufferByteAccess QI の CsWinRT 問題を回避するため BitmapEncoder を使用。
    /// </summary>
    private static async Task<Bitmap> SoftBitmapToGdiAsync(SoftwareBitmap soft)
    {
        using var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();

        // BMP 形式でエンコード（PNG より高速・可逆）
        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
            Windows.Graphics.Imaging.BitmapEncoder.BmpEncoderId, ras)
            .AsTask().ConfigureAwait(false);
        encoder.SetSoftwareBitmap(soft);
        await encoder.FlushAsync().AsTask().ConfigureAwait(false);

        // ストリームをバイト配列に読み出し
        var size   = (uint)ras.Size;
        var reader = new Windows.Storage.Streams.DataReader(ras.GetInputStreamAt(0));
        await reader.LoadAsync(size).AsTask().ConfigureAwait(false);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);

        // GDI Bitmap として解釈（MemoryStream に依存しない独立コピーを返す）
        using var ms  = new System.IO.MemoryStream(bytes);
        using var tmp = new Bitmap(ms);
        return new Bitmap(tmp); // stream への依存を除くためコピーを作成
    }
}
