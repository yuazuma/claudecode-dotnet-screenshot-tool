using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// Windows MediaFoundation (IMFSinkWriter) を使って H.264+AAC の MP4 ファイルを生成するライター。
/// 追加バイナリ不要。OS 内蔵の H.264/AAC MFT を利用する (Windows 10 1809+)。
/// </summary>
public sealed class MfVideoWriter : IDisposable
{
    // ── P/Invoke ────────────────────────────────────────────────────────────────
    [DllImport("mfplat.dll",      ExactSpelling = true)] private static extern int MFStartup(uint version, uint dwFlags);
    [DllImport("mfplat.dll",      ExactSpelling = true)] private static extern int MFShutdown();
    [DllImport("mfreadwrite.dll", ExactSpelling = true)]
    private static extern int MFCreateSinkWriterFromURL(
        [MarshalAs(UnmanagedType.LPWStr)] string pwszOutputURL,
        IntPtr pByteStream, [MarshalAs(UnmanagedType.IUnknown)] object? pAttributes,
        out IMFSinkWriter ppSinkWriter);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateAttributes(out IMFAttributesMin ppMFAttributes, uint cInitialSize);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMediaType(out IMFMediaType ppMFType);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IMFMediaBuffer ppBuffer);
    [DllImport("mfplat.dll", ExactSpelling = true)] private static extern int MFCreateSample(out IMFSample ppIMFSample);

    private const uint MF_VERSION = 0x00020070;

    // ── GUID 定数 ─────────────────────────────────────────────────────────────
    // MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS: 0 = ソフトウェア MFT のみ使用（Azure Server 対応）
    private static readonly Guid MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS =
        new("A634A91C-822B-41B9-A494-4DE4643612B0");
    // MJPEG コーデック（H.264 が使えない環境でのフォールバック）
    private static readonly Guid MFVideoFormat_MJPG =
        new("47504A4D-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFMediaType_Video           = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFMediaType_Audio           = new("73647561-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_H264          = new("34363248-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFAudioFormat_AAC           = new("00001610-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_RGB32         = new("e436eb7e-524f-11ce-9f53-0020af0ba770");
    private static readonly Guid MFAudioFormat_PCM           = new("00000001-0000-0010-8000-00AA00389B71");
    private static readonly Guid MF_MT_MAJOR_TYPE            = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MF_MT_SUBTYPE               = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MF_MT_AVG_BITRATE           = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    private static readonly Guid MF_MT_INTERLACE_MODE        = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static readonly Guid MF_MT_FRAME_SIZE            = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid MF_MT_FRAME_RATE            = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid MF_MT_PIXEL_ASPECT_RATIO    = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    private static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND   = new("5faeeae7-0290-4c31-9e8a-c534f68d9ded");
    private static readonly Guid MF_MT_AUDIO_NUM_CHANNELS         = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    private static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE      = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    private static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    private static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT      = new("322de230-9eeb-43bd-ab7a-ff412251541d");

    // ── フィールド ──────────────────────────────────────────────────────────────
    private IMFSinkWriter? _writer;
    private int  _videoStreamIndex;
    private int  _audioStreamIndex = -1;
    private long _videoTimestamp;
    private long _audioTimestamp;
    private bool _hasAudio;
    private readonly int _width;
    private readonly int _height;
    private readonly int _fps;
    private readonly int _bitrateBps;
    private bool _disposed;
    private bool _begun;
    private int  _sampleRate  = 44100;
    private int  _channels    = 1;
    private int  _bitsPerSamp = 16;
    private readonly bool _useMjpeg;  // true = MJPEG フォールバックモード

    public MfVideoWriter(string outputPath, int width, int height, int fps, int bitrateMbps,
        bool useMjpeg = false)
    {
        _width      = width  % 2 == 0 ? width  : width  - 1;
        _height     = height % 2 == 0 ? height : height - 1;
        _fps        = fps;
        _bitrateBps = bitrateMbps * 1_000_000;
        _useMjpeg   = useMjpeg;

        ThrowIfFailed(MFStartup(MF_VERSION, 0), "MFStartup");

        // MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS = 0 を設定してソフトウェアエンコーダーを強制する。
        // この属性なしでは Azure / Windows Server 環境でハードウェア加速 (DXVA2/D3D11) が
        // 利用できず Finalize 時に E_UNEXPECTED が返る問題を回避する。
        ThrowIfFailed(MFCreateAttributes(out var attrs, 1), "MFCreateAttributes");
        var hwKey = MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS;
        attrs.SetUINT32(ref hwKey, 0u);
        int hr = MFCreateSinkWriterFromURL(outputPath, IntPtr.Zero, attrs, out _writer);
        Marshal.ReleaseComObject(attrs);
        ThrowIfFailed(hr, "MFCreateSinkWriterFromURL");

        _videoStreamIndex = AddVideoStream();
    }

    public void EnableAudio(byte[] firstWavSample)
    {
        if (_writer == null || _begun) return;
        try
        {
            if (firstWavSample.Length >= 44)
            {
                _sampleRate  = BitConverter.ToInt32(firstWavSample, 24);
                _channels    = BitConverter.ToInt16(firstWavSample, 22);
                _bitsPerSamp = BitConverter.ToInt16(firstWavSample, 34);
            }
            _audioStreamIndex = AddAudioStream();
            _hasAudio = true;
        }
        catch (Exception ex) { Log.Warning(ex, "音声ストリーム追加失敗。無音 MP4 を出力します。"); }
    }

    public void AddVideoFrame(Bitmap bmp, double durationSeconds)
    {
        if (_writer == null) return;
        EnsureBegun();
        long dur = (long)(durationSeconds * 10_000_000);
        WriteSample(_videoStreamIndex, BitmapToRgb32(bmp), _videoTimestamp, dur);
        _videoTimestamp += dur;
    }

    public void AddAudioSample(byte[]? wavBytes)
    {
        if (_writer == null || !_hasAudio || wavBytes == null || wavBytes.Length <= 44) return;
        EnsureBegun();
        int   dataLen    = wavBytes.Length - 44;
        var   pcm        = new byte[dataLen];
        Buffer.BlockCopy(wavBytes, 44, pcm, 0, dataLen);
        int   bps        = _sampleRate * _channels * (_bitsPerSamp / 8);
        long  dur        = bps > 0 ? (long)((double)dataLen / bps * 10_000_000) : 0;
        WriteSample(_audioStreamIndex, pcm, _audioTimestamp, dur);
        _audioTimestamp += dur;
    }

    public void FinalizeFile()
    {
        if (_writer == null) return;
        EnsureBegun();
        int hr = _writer.Finalize();
        if (hr < 0)
        {
            // 例外として投げることで呼び出し元（VideoGenerator）が失敗を検知できる。
            // 黙って飲み込むと 0 バイトの MP4 を「成功」と誤ログする問題を防ぐ。
            Log.Error("IMFSinkWriter.Finalize 失敗 HRESULT=0x{Hr:X8} — H.264 コーデックが利用できない可能性があります", (uint)hr);
            throw new System.Runtime.InteropServices.COMException(
                $"IMFSinkWriter.Finalize に失敗しました (HRESULT=0x{(uint)hr:X8})。" +
                " H.264 エンコーダーが利用できないか、フレームが書き込まれていない可能性があります。", hr);
        }
    }

    // ── 内部実装 ──────────────────────────────────────────────────────────────
    private int AddVideoStream()
    {
        MFCreateMediaType(out var outMt);
        MfSetG(outMt, MF_MT_MAJOR_TYPE,       MFMediaType_Video);
        MfSetG(outMt, MF_MT_SUBTYPE,           _useMjpeg ? MFVideoFormat_MJPG : MFVideoFormat_H264);
        if (!_useMjpeg) MfSetU(outMt, MF_MT_AVG_BITRATE, (uint)_bitrateBps); // MJPEG は品質ベースのため不要
        MfSetU(outMt, MF_MT_INTERLACE_MODE,    2u);
        MfSetQ(outMt, MF_MT_FRAME_SIZE,        _width,  _height);
        MfSetQ(outMt, MF_MT_FRAME_RATE,        _fps,    1);
        MfSetQ(outMt, MF_MT_PIXEL_ASPECT_RATIO, 1,      1);
        _writer!.AddStream(outMt, out int idx);
        Marshal.ReleaseComObject(outMt);

        MFCreateMediaType(out var inMt);
        MfSetG(inMt, MF_MT_MAJOR_TYPE, MFMediaType_Video);
        MfSetG(inMt, MF_MT_SUBTYPE,    MFVideoFormat_RGB32);
        MfSetQ(inMt, MF_MT_FRAME_SIZE, _width, _height);
        MfSetQ(inMt, MF_MT_FRAME_RATE, _fps,   1);
        MfSetQ(inMt, MF_MT_PIXEL_ASPECT_RATIO, 1, 1);
        _writer.SetInputMediaType(idx, inMt, IntPtr.Zero);
        Marshal.ReleaseComObject(inMt);
        return idx;
    }

    private int AddAudioStream()
    {
        MFCreateMediaType(out var outMt);
        MfSetG(outMt, MF_MT_MAJOR_TYPE,                MFMediaType_Audio);
        MfSetG(outMt, MF_MT_SUBTYPE,                   MFAudioFormat_AAC);
        MfSetU(outMt, MF_MT_AUDIO_SAMPLES_PER_SECOND,  (uint)_sampleRate);
        MfSetU(outMt, MF_MT_AUDIO_NUM_CHANNELS,         (uint)_channels);
        MfSetU(outMt, MF_MT_AUDIO_BITS_PER_SAMPLE,      16u);
        MfSetU(outMt, MF_MT_AUDIO_AVG_BYTES_PER_SECOND, 24000u);
        _writer!.AddStream(outMt, out int idx);
        Marshal.ReleaseComObject(outMt);

        int   ba = _channels * (_bitsPerSamp / 8);
        MFCreateMediaType(out var inMt);
        MfSetG(inMt, MF_MT_MAJOR_TYPE,                MFMediaType_Audio);
        MfSetG(inMt, MF_MT_SUBTYPE,                   MFAudioFormat_PCM);
        MfSetU(inMt, MF_MT_AUDIO_SAMPLES_PER_SECOND,  (uint)_sampleRate);
        MfSetU(inMt, MF_MT_AUDIO_NUM_CHANNELS,         (uint)_channels);
        MfSetU(inMt, MF_MT_AUDIO_BITS_PER_SAMPLE,      (uint)_bitsPerSamp);
        MfSetU(inMt, MF_MT_AUDIO_BLOCK_ALIGNMENT,       (uint)ba);
        MfSetU(inMt, MF_MT_AUDIO_AVG_BYTES_PER_SECOND,  (uint)(_sampleRate * ba));
        _writer.SetInputMediaType(idx, inMt, IntPtr.Zero);
        Marshal.ReleaseComObject(inMt);
        return idx;
    }

    private void EnsureBegun()
    {
        if (_begun) return;
        _writer!.BeginWriting();
        _begun = true;
    }

    private void WriteSample(int streamIdx, byte[] data, long timestamp, long duration)
    {
        MFCreateMemoryBuffer((uint)data.Length, out var buf);
        buf.Lock(out IntPtr ptr, out _, out _);
        Marshal.Copy(data, 0, ptr, data.Length);
        buf.Unlock();
        buf.SetCurrentLength((uint)data.Length);

        MFCreateSample(out var sample);
        sample.AddBuffer(buf);
        sample.SetSampleTime(timestamp);
        sample.SetSampleDuration(duration);
        _writer!.WriteSample(streamIdx, sample);
        Marshal.ReleaseComObject(sample);
        Marshal.ReleaseComObject(buf);
    }

    private byte[] BitmapToRgb32(Bitmap src)
    {
        using var bmp = src.Width == _width && src.Height == _height
            ? (Bitmap)src.Clone()
            : new Bitmap(src, _width, _height);
        var bd = bmp.LockBits(new Rectangle(0, 0, _width, _height),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        int stride = Math.Abs(bd.Stride);
        var buf    = new byte[stride * _height];
        var flipped = new byte[stride * _height];
        Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
        bmp.UnlockBits(bd);
        // MediaFoundation は RGB32 をボトムアップで受け取る
        for (int y = 0; y < _height; y++)
            Buffer.BlockCopy(buf, y * stride, flipped, (_height - 1 - y) * stride, stride);
        return flipped;
    }

    // ── IMFMediaType ヘルパー (ref Guid ラッパー) ─────────────────────────────
    private static void MfSetG(IMFMediaType t, Guid k, Guid v) { t.SetGUID(ref k, ref v); }
    private static void MfSetU(IMFMediaType t, Guid k, uint v) { t.SetUINT32(ref k, v); }
    private static void MfSetQ(IMFMediaType t, Guid k, int hi, int lo)
        { t.SetUINT64(ref k, ((ulong)(uint)hi << 32) | (uint)lo); }

    private static void ThrowIfFailed(int hr, string op)
    {
        if (hr < 0) throw new COMException($"MF {op} failed", hr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_writer != null) { Marshal.ReleaseComObject(_writer); _writer = null; }
        MFShutdown();
    }

    // ── COM インターフェース定義 ──────────────────────────────────────────────

    /// <summary>
    /// IMFAttributes の最小定義。MFCreateAttributes で取得した属性オブジェクトへの
    /// SetUINT32 のみを使用する。vtable 順序は Windows SDK 仕様に準拠。
    /// </summary>
    [ComImport, Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFAttributesMin
    {
        [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.IUnknown)] object pTheirs, int MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        [PreserveSig] int GetUINT32(ref Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
        [PreserveSig] int GetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, int cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
        [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
        [PreserveSig] int GetBlob(ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
        [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(ref Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid guidKey, uint unValue);
        // 以降のメソッドは使用しないが vtable 上に存在することに注意
    }

    [ComImport, Guid("3137f1cd-fe5e-4805-a5d8-fb477448cb3d"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSinkWriter
    {
        [PreserveSig] int AddStream(IMFMediaType pTargetMediaType, out int pdwStreamIndex);
        [PreserveSig] int SetInputMediaType(int dwStreamIndex, IMFMediaType pInputMediaType, IntPtr pEncParams);
        [PreserveSig] int BeginWriting();
        [PreserveSig] int WriteSample(int dwStreamIndex, IMFSample pSample);
        [PreserveSig] int SendStreamTick(int dwStreamIndex, long llTimestamp);
        [PreserveSig] int PlaceMarker(int dwStreamIndex, int eMarkerType, IntPtr pvarMarker, IntPtr pvarCtx);
        [PreserveSig] int NotifyEndOfSegment(int dwStreamIndex);
        [PreserveSig] int Flush(int dwStreamIndex);
        [PreserveSig] int Finalize();
        [PreserveSig] int GetServiceForStream(int dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int GetStatistics(int dwStreamIndex, out long pStats);
    }

    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType
    {
        // IMFAttributes (最小限のみ定義)
        [PreserveSig] int GetItem(ref Guid guidKey, IntPtr pValue);
        [PreserveSig] int GetItemType(ref Guid guidKey, out int pType);
        [PreserveSig] int CompareItem(ref Guid guidKey, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.IUnknown)] object pTheirs, int MatchType, [MarshalAs(UnmanagedType.Bool)] out bool pbResult);
        [PreserveSig] int GetUINT32(ref Guid guidKey, out uint punValue);
        [PreserveSig] int GetUINT64(ref Guid guidKey, out ulong punValue);
        [PreserveSig] int GetDouble(ref Guid guidKey, out double pfValue);
        [PreserveSig] int GetGUID(ref Guid guidKey, out Guid pguidValue);
        [PreserveSig] int GetStringLength(ref Guid guidKey, out int pcchLength);
        [PreserveSig] int GetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, int cchBufSize, IntPtr pcchLength);
        [PreserveSig] int GetAllocatedString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
        [PreserveSig] int GetBlobSize(ref Guid guidKey, out int pcbBlobSize);
        [PreserveSig] int GetBlob(ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, IntPtr pcbBlobSize);
        [PreserveSig] int GetAllocatedBlob(ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
        [PreserveSig] int GetUnknown(ref Guid guidKey, ref Guid riid, out IntPtr ppv);
        [PreserveSig] int SetItem(ref Guid guidKey, IntPtr value);
        [PreserveSig] int DeleteItem(ref Guid guidKey);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid guidKey, uint unValue);
        [PreserveSig] int SetUINT64(ref Guid guidKey, ulong unValue);
        [PreserveSig] int SetDouble(ref Guid guidKey, double fValue);
        [PreserveSig] int SetGUID(ref Guid guidKey, ref Guid guidValue);
        [PreserveSig] int SetString(ref Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
        [PreserveSig] int SetBlob(ref Guid guidKey, [In] byte[] pBuf, int cbBufSize);
        [PreserveSig] int SetUnknown(ref Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out int pcItems);
        [PreserveSig] int GetItemByIndex(int unIndex, out Guid pguidKey, IntPtr pValue);
        [PreserveSig] int CopyAllItems([MarshalAs(UnmanagedType.IUnknown)] object pDest);
        // IMFMediaType 固有
        [PreserveSig] int GetMajorType(out Guid pguidMajorType);
        [PreserveSig] int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool pfCompressed);
        [PreserveSig] int IsEqual(IMFMediaType pIMediaType, out uint pdwFlags);
        [PreserveSig] int GetRepresentation(Guid guidRepresentation, out IntPtr ppvRepresentation);
        [PreserveSig] int FreeRepresentation(Guid guidRepresentation, IntPtr pvRepresentation);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        // IMFAttributes (最小限)
        [PreserveSig] int GetItem(ref Guid k, IntPtr v);
        [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.IUnknown)] object p, int m, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v);
        [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v);
        [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out int l);
        [PreserveSig] int GetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder sb, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] out string s, out int l);
        [PreserveSig] int GetBlobSize(ref Guid k, out int s);
        [PreserveSig] int GetBlob(ref Guid k, [Out] byte[] b, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out int s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid r, out IntPtr p);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v);
        [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems();
        [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v);
        [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, [In] byte[] b, int n);
        [PreserveSig] int SetUnknown(ref Guid k, [MarshalAs(UnmanagedType.IUnknown)] object o);
        [PreserveSig] int LockStore();
        [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetItemByIndex(int i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems([MarshalAs(UnmanagedType.IUnknown)] object d);
        // IMFSample 固有
        [PreserveSig] int GetSampleFlags(out uint pdwSampleFlags);
        [PreserveSig] int SetSampleFlags(uint dwSampleFlags);
        [PreserveSig] int GetSampleTime(out long phnsSampleTime);
        [PreserveSig] int SetSampleTime(long hnsSampleTime);
        [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
        [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
        [PreserveSig] int GetBufferCount(out int pdwBufferCount);
        [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
        [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
        [PreserveSig] int RemoveBufferByIndex(int dwIndex);
        [PreserveSig] int RemoveAllBuffers();
        [PreserveSig] int GetTotalLength(out uint pcbTotalLength);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
    }

    [ComImport, Guid("045FA593-8799-42B8-BC8D-8968C6453507"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr ppbBuffer, out uint pcbMaxLength, out uint pcbCurrentLength);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint pcbCurrentLength);
        [PreserveSig] int SetCurrentLength(uint cbCurrentLength);
        [PreserveSig] int GetMaxLength(out uint pcbMaxLength);
    }
}
