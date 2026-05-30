using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// H.264 MFT エンコーダーを直接使用して MP4 を生成するライター。
/// IMFSinkWriter の MPEG-4 マルチプレクサーをバイパスするため、
/// Azure Windows Server 等で Finalize が失敗する問題を回避できる。
/// ISO BMFF コンテナは自前で書き出す。
/// </summary>
public sealed class H264Mp4Writer : IDisposable
{
    // ── P/Invoke ────────────────────────────────────────────────────────────────
    [DllImport("mfplat.dll",  ExactSpelling = true)] private static extern int MFStartup(uint version, uint dwFlags);
    [DllImport("mfplat.dll",  ExactSpelling = true)] private static extern int MFShutdown();
    [DllImport("mfplat.dll",  ExactSpelling = true)] private static extern int MFCreateMediaType(out IMFMediaType ppMFType);
    [DllImport("mfplat.dll",  ExactSpelling = true)] private static extern int MFCreateMemoryBuffer(uint cbMaxLength, out IMFMediaBuffer ppBuffer);
    [DllImport("mfplat.dll",  ExactSpelling = true)] private static extern int MFCreateSample(out IMFSample ppIMFSample);
    [DllImport("mfplat.dll",  ExactSpelling = true)]
    private static extern int MFTEnum(
        Guid guidCategory, uint flags,
        IntPtr pInputType, ref MftRegisterTypeInfo pOutputType,
        IntPtr pAttributes, out IntPtr ppclsidMFT, out uint pcMFTs);
    [DllImport("ole32.dll",   ExactSpelling = true)]
    private static extern int CoCreateInstance(
        ref Guid rclsid, IntPtr pUnkOuter, uint dwClsContext,
        ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    [DllImport("ole32.dll",   ExactSpelling = true)] private static extern void CoTaskMemFree(IntPtr pv);

    private const uint MF_VERSION    = 0x00020070;
    private const uint CLSCTX_INPROC = 1;
    private const uint MFT_ENUM_FLAG_SYNCMFT = 0x2;

    // MFT_MESSAGE_TYPE values
    private const uint MFT_MESSAGE_NOTIFY_BEGIN_STREAMING   = 0x10000001;
    private const uint MFT_MESSAGE_NOTIFY_END_OF_STREAM     = 0x10000003;
    private const uint MFT_MESSAGE_COMMAND_DRAIN            = 0x00000001;

    // H264 output needs more buffer for processing
    private const uint MFT_OUTPUT_STATUS_SAMPLE_READY       = 1;
    private const int  MF_E_TRANSFORM_NEED_MORE_INPUT       = unchecked((int)0xC00D6D72);
    private const int  MF_E_TRANSFORM_STREAM_CHANGE         = unchecked((int)0xC00D6D61);

    // GUIDs
    private static readonly Guid MFT_CATEGORY_VIDEO_ENCODER = new("F79EFB0E-879F-4C48-B7A9-F8A7DB3B9845");
    private static readonly Guid MFMediaType_Video          = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_H264         = new("34363248-0000-0010-8000-00AA00389B71");
    private static readonly Guid MFVideoFormat_RGB32        = new("e436eb7e-524f-11ce-9f53-0020af0ba770");
    private static readonly Guid MF_MT_MAJOR_TYPE           = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid MF_MT_SUBTYPE              = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid MF_MT_AVG_BITRATE          = new("20332624-fb0d-4d9e-bd0d-cbf6786c102e");
    private static readonly Guid MF_MT_INTERLACE_MODE       = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    private static readonly Guid MF_MT_FRAME_SIZE           = new("1652c33d-d6b2-4012-b834-72030849a37d");
    private static readonly Guid MF_MT_FRAME_RATE           = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");
    private static readonly Guid MF_MT_PIXEL_ASPECT_RATIO   = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    private static readonly Guid IID_IMFTransform           = new("BF94C121-5B05-4E6F-8000-BA598961414D");

    // ── フィールド ──────────────────────────────────────────────────────────────
    private readonly string _outputPath;
    private readonly int    _width;
    private readonly int    _height;
    private readonly int    _fps;
    private readonly int    _bitrateBps;
    private IMFTransform?   _encoder;
    private bool            _disposed;
    private bool            _streaming;
    private long            _inputTimestamp;
    private long            _outputTimestamp;

    // エンコード済みフレーム（AVCC 形式）
    private readonly List<byte[]> _encodedFrames   = [];
    private readonly List<long>   _durations        = [];  // 100ns 単位
    private byte[]?               _sps;
    private byte[]?               _pps;

    public H264Mp4Writer(string outputPath, int width, int height, int fps, int bitrateMbps)
    {
        _outputPath = outputPath;
        _width      = width  % 2 == 0 ? width  : width  - 1;
        _height     = height % 2 == 0 ? height : height - 1;
        _fps        = fps;
        _bitrateBps = bitrateMbps * 1_000_000;

        ThrowIfFailed(MFStartup(MF_VERSION, 0), "MFStartup");
        _encoder = CreateH264Encoder();
    }

    public void AddVideoFrame(Bitmap bmp, double durationSeconds)
    {
        if (_encoder == null) throw new ObjectDisposedException(nameof(H264Mp4Writer));

        if (!_streaming)
        {
            ThrowIfFailed(_encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, UIntPtr.Zero),
                "NotifyBeginStreaming");
            _streaming = true;
        }

        long dur = (long)(durationSeconds * 10_000_000);

        // RGB32 bitmap → IMFSample
        byte[] rgb32 = BitmapToRgb32(bmp, _width, _height);
        var sample = CreateSampleFromRaw(rgb32, _inputTimestamp, dur);
        _inputTimestamp += dur;

        // ProcessInput
        int hrIn = _encoder.ProcessInput(0, sample, 0);
        if (hrIn < 0 && hrIn != MF_E_TRANSFORM_NEED_MORE_INPUT)
            ThrowIfFailed(hrIn, "ProcessInput");

        // ProcessOutput をドレインして蓄積
        DrainOutput(dur);
    }

    public void FinalizeFile()
    {
        if (_encoder == null) throw new ObjectDisposedException(nameof(H264Mp4Writer));

        if (!_streaming)
        {
            ThrowIfFailed(_encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, UIntPtr.Zero), "BeginStreaming");
            _streaming = true;
        }

        // ストリーム終了を通知してエンコーダーをフラッシュ
        _encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, UIntPtr.Zero);
        _encoder.ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, UIntPtr.Zero);

        // 残りフレームをドレイン
        DrainOutput(0, drainAll: true);

        if (_encodedFrames.Count == 0)
            throw new InvalidOperationException("エンコードされたフレームがありません。");
        if (_sps == null || _pps == null)
            throw new InvalidOperationException("SPS/PPS が取得できませんでした。");

        Log.Information("H264Mp4Writer: {Count} フレームを MP4 に書き出し中...", _encodedFrames.Count);
        WriteMp4();
        Log.Information("H264Mp4Writer: MP4 書き出し完了 → {Path}", _outputPath);
    }

    // ── H.264 エンコーダー MFT の作成 ─────────────────────────────────────────

    private IMFTransform CreateH264Encoder()
    {
        // H.264 エンコーダー MFT を列挙
        var outputTypeInfo = new MftRegisterTypeInfo
        {
            guidMajorType = MFMediaType_Video,
            guidSubtype   = MFVideoFormat_H264,
        };

        int hr = MFTEnum(MFT_CATEGORY_VIDEO_ENCODER, MFT_ENUM_FLAG_SYNCMFT,
            IntPtr.Zero, ref outputTypeInfo, IntPtr.Zero,
            out IntPtr pClsids, out uint count);
        if (hr < 0 || count == 0)
            throw new COMException("H.264 エンコーダー MFT が見つかりません。", hr);

        // 先頭 CLSID を使用
        Guid clsid = Marshal.PtrToStructure<Guid>(pClsids);
        CoTaskMemFree(pClsids);

        Log.Debug("H264Mp4Writer: エンコーダー CLSID = {Clsid}", clsid);

        var iidMfTransform = IID_IMFTransform;
        hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC,
            ref iidMfTransform, out object obj);
        if (hr < 0) ThrowIfFailed(hr, "CoCreateInstance(H264 MFT)");

        var encoder = (IMFTransform)obj;

        // 出力型を設定（H.264）
        MFCreateMediaType(out var outMt);
        SetG(outMt, MF_MT_MAJOR_TYPE,         MFMediaType_Video);
        SetG(outMt, MF_MT_SUBTYPE,            MFVideoFormat_H264);
        SetU(outMt, MF_MT_AVG_BITRATE,        (uint)_bitrateBps);
        SetU(outMt, MF_MT_INTERLACE_MODE,     2u);
        SetQ(outMt, MF_MT_FRAME_SIZE,         _width,  _height);
        SetQ(outMt, MF_MT_FRAME_RATE,         _fps,    1);
        SetQ(outMt, MF_MT_PIXEL_ASPECT_RATIO, 1,       1);
        ThrowIfFailed(encoder.SetOutputType(0, outMt, 0), "SetOutputType(H264)");
        Marshal.ReleaseComObject(outMt);

        // 入力型を設定（RGB32）
        MFCreateMediaType(out var inMt);
        SetG(inMt, MF_MT_MAJOR_TYPE,         MFMediaType_Video);
        SetG(inMt, MF_MT_SUBTYPE,            MFVideoFormat_RGB32);
        SetQ(inMt, MF_MT_FRAME_SIZE,         _width, _height);
        SetQ(inMt, MF_MT_FRAME_RATE,         _fps,   1);
        SetQ(inMt, MF_MT_PIXEL_ASPECT_RATIO, 1,      1);
        ThrowIfFailed(encoder.SetInputType(0, inMt, 0), "SetInputType(RGB32)");
        Marshal.ReleaseComObject(inMt);

        return encoder;
    }

    // ── ProcessOutput ループ ────────────────────────────────────────────────────

    private void DrainOutput(long defaultDur, bool drainAll = false)
    {
        const int maxAttempts = 1000;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var buf = new MftOutputDataBuffer { dwStreamID = 0, pSample = IntPtr.Zero };
            int hr = _encoder!.ProcessOutput(0, 1, ref buf, out _);

            if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT && !drainAll) break;
            if (hr == MF_E_TRANSFORM_STREAM_CHANGE) continue;
            if (hr < 0 && hr != MF_E_TRANSFORM_NEED_MORE_INPUT) break;

            if (buf.pSample == IntPtr.Zero)
            {
                if (!drainAll) break;
                if (hr == MF_E_TRANSFORM_NEED_MORE_INPUT) break;
                continue;
            }

            try
            {
                var sample = (IMFSample)Marshal.GetObjectForIUnknown(buf.pSample);
                ProcessOutputSample(sample, defaultDur);
                Marshal.ReleaseComObject(sample);
            }
            finally
            {
                Marshal.Release(buf.pSample);
            }

            if (!drainAll) break;
        }
    }

    private void ProcessOutputSample(IMFSample sample, long defaultDur)
    {
        sample.GetSampleDuration(out long dur);
        if (dur <= 0) dur = defaultDur;

        sample.GetBufferByIndex(0, out IMFMediaBuffer buf);
        buf.Lock(out IntPtr ptr, out _, out uint curLen);
        var data = new byte[curLen];
        Marshal.Copy(ptr, data, 0, (int)curLen);
        buf.Unlock();
        Marshal.ReleaseComObject(buf);

        // Annex-B ストリームを解析して AVCC に変換
        var nalUnits = ParseAnnexB(data);
        var avccFrame = new MemoryStream();
        bool hasVideo = false;

        foreach (var nal in nalUnits)
        {
            if (nal.Length == 0) continue;
            int nalType = nal[0] & 0x1F;

            if (nalType == 7) { _sps = nal; continue; }  // SPS
            if (nalType == 8) { _pps = nal; continue; }  // PPS

            // 映像 NAL → AVCC（4 バイト長プレフィックス + NAL）
            uint nalLen = (uint)nal.Length;
            avccFrame.WriteByte((byte)(nalLen >> 24));
            avccFrame.WriteByte((byte)(nalLen >> 16));
            avccFrame.WriteByte((byte)(nalLen >> 8));
            avccFrame.WriteByte((byte)nalLen);
            avccFrame.Write(nal);
            hasVideo = true;
        }

        if (hasVideo)
        {
            _encodedFrames.Add(avccFrame.ToArray());
            _durations.Add(dur);
        }
    }

    // Annex-B → NAL ユニット配列に分割
    private static List<byte[]> ParseAnnexB(byte[] data)
    {
        var nals = new List<byte[]>();
        int pos = 0;

        while (pos < data.Length)
        {
            int start = FindStartCode(data, pos);
            if (start < 0) break;

            int startLen = (start + 3 < data.Length && data[start + 2] == 1) ? 3 : 4;
            int nalStart = start + startLen;
            int nextStart = FindStartCode(data, nalStart);
            int nalEnd   = nextStart >= 0 ? nextStart : data.Length;

            if (nalEnd > nalStart)
                nals.Add(data[nalStart..nalEnd]);

            pos = nalEnd;
        }
        return nals;
    }

    private static int FindStartCode(byte[] data, int from)
    {
        for (int i = from; i < data.Length - 2; i++)
        {
            if (data[i] == 0 && data[i + 1] == 0)
            {
                if (data[i + 2] == 1) return i;
                if (i + 3 < data.Length && data[i + 2] == 0 && data[i + 3] == 1) return i;
            }
        }
        return -1;
    }

    // ── MP4 コンテナ書き出し（ISO BMFF + AVCC H.264） ──────────────────────────

    private void WriteMp4()
    {
        using var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs);

        uint timescale  = (uint)_fps;
        uint totalDur   = (uint)_encodedFrames.Count;  // 各フレーム 1 tick

        // ftyp
        WriteBox(bw, "ftyp", b =>
        {
            b.Write(Ascii4("avc1"));
            b.Write(BE32(0));
            b.Write(Ascii4("isom"));
            b.Write(Ascii4("avc1"));
        });

        // mdat: AVCC フレームを順番に書き出しオフセットを記録
        var chunkOffsets = new List<long>(_encodedFrames.Count);
        var mdatStart    = fs.Position;
        bw.Write(BE32(0));  // size placeholder
        bw.Write(Ascii4("mdat"));

        foreach (var frame in _encodedFrames)
        {
            chunkOffsets.Add(fs.Position);
            bw.Write(frame);
        }

        long mdatEnd  = fs.Position;
        uint mdatSize = (uint)(mdatEnd - mdatStart);
        fs.Position   = mdatStart;
        bw.Write(BE32(mdatSize));
        fs.Position   = mdatEnd;

        // moov
        WriteBox(bw, "moov", moov =>
        {
            // mvhd
            WriteBox(moov, "mvhd", b =>
            {
                b.Write(new byte[4]);
                b.Write(BE32(0)); b.Write(BE32(0));
                b.Write(BE32(1000));            // timescale = 1000ms
                uint durationMs = (uint)(_durations.Sum(d => d) / 10_000);
                b.Write(BE32(durationMs));
                b.Write(BE32(0x00010000));      // rate 1.0
                b.Write(BE16(0x0100));          // volume
                b.Write(new byte[10]);
                WriteMatrix(b);
                b.Write(new byte[24]);
                b.Write(BE32(2));              // next track ID
            });

            // trak
            WriteBox(moov, "trak", trak =>
            {
                WriteBox(trak, "tkhd", b =>
                {
                    b.Write((byte)0); b.Write(new byte[2]); b.Write((byte)3);
                    b.Write(BE32(0)); b.Write(BE32(0));
                    b.Write(BE32(1));           // track ID
                    b.Write(BE32(0));
                    uint tDur = (uint)(_durations.Sum(d => d) / 10_000);
                    b.Write(BE32(tDur));
                    b.Write(new byte[8]);
                    b.Write(BE16(0)); b.Write(BE16(0));
                    b.Write(BE16(0)); b.Write(BE16(0));
                    WriteMatrix(b);
                    b.Write(BE32((uint)(_width  << 16)));
                    b.Write(BE32((uint)(_height << 16)));
                });

                WriteBox(trak, "mdia", mdia =>
                {
                    WriteBox(mdia, "mdhd", b =>
                    {
                        b.Write(new byte[4]);
                        b.Write(BE32(0)); b.Write(BE32(0));
                        b.Write(BE32(timescale));
                        b.Write(BE32(totalDur));
                        b.Write(BE16(0x55C4));  // language: und
                        b.Write(BE16(0));
                    });

                    WriteBox(mdia, "hdlr", b =>
                    {
                        b.Write(new byte[8]);
                        b.Write(Ascii4("vide"));
                        b.Write(new byte[12]);
                        b.Write(Encoding.UTF8.GetBytes("VideoHandler\0"));
                    });

                    WriteBox(mdia, "minf", minf =>
                    {
                        WriteBox(minf, "vmhd", b =>
                        {
                            b.Write((byte)0); b.Write(new byte[2]); b.Write((byte)1);
                            b.Write(new byte[8]);
                        });

                        WriteBox(minf, "dinf", di =>
                        {
                            WriteBox(di, "dref", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(1));
                                WriteBox(b, "url ", u =>
                                {
                                    u.Write((byte)0); u.Write(new byte[2]); u.Write((byte)1);
                                });
                            });
                        });

                        WriteBox(minf, "stbl", stbl =>
                        {
                            // stsd → avc1 → avcC
                            WriteBox(stbl, "stsd", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(1));
                                WriteBox(b, "avc1", av =>
                                {
                                    av.Write(new byte[6]);
                                    av.Write(BE16(1));  // data ref index
                                    av.Write(new byte[16]);
                                    av.Write(BE16((ushort)_width));
                                    av.Write(BE16((ushort)_height));
                                    av.Write(BE32(0x00480000));  // horiz 72dpi
                                    av.Write(BE32(0x00480000));  // vert 72dpi
                                    av.Write(BE32(0));
                                    av.Write(BE16(1));
                                    av.Write(new byte[32]);
                                    av.Write(BE16(0x18));
                                    av.Write(BE16(0xFFFF));
                                    // avcC
                                    WriteBox(av, "avcC", ac =>
                                    {
                                        ac.Write((byte)1);         // configurationVersion
                                        ac.Write(_sps![1]);        // profile
                                        ac.Write(_sps![2]);        // compatibility
                                        ac.Write(_sps![3]);        // level
                                        ac.Write((byte)0xFF);      // lengthSizeMinusOne = 3
                                        ac.Write((byte)0xE1);      // 1 SPS
                                        ac.Write(BE16((ushort)_sps!.Length));
                                        ac.Write(_sps!);
                                        ac.Write((byte)1);         // 1 PPS
                                        ac.Write(BE16((ushort)_pps!.Length));
                                        ac.Write(_pps!);
                                    });
                                });
                            });

                            // stts: 1 tick per frame
                            WriteBox(stbl, "stts", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(1));
                                b.Write(BE32((uint)_encodedFrames.Count));
                                b.Write(BE32(1u));
                            });

                            // stsc: 1 sample per chunk
                            WriteBox(stbl, "stsc", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(1));
                                b.Write(BE32(1)); b.Write(BE32(1)); b.Write(BE32(1));
                            });

                            // stsz: variable sample sizes
                            WriteBox(stbl, "stsz", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(0));
                                b.Write(BE32((uint)_encodedFrames.Count));
                                foreach (var f in _encodedFrames) b.Write(BE32((uint)f.Length));
                            });

                            // stco: chunk offsets
                            WriteBox(stbl, "stco", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32((uint)chunkOffsets.Count));
                                foreach (long off in chunkOffsets) b.Write(BE32((uint)off));
                            });

                            // stss: all frames are sync (IDR or P-frame with key)
                            WriteBox(stbl, "stss", b =>
                            {
                                b.Write(new byte[4]);
                                b.Write(BE32(1));
                                b.Write(BE32(1u));  // first frame is IDR
                            });
                        });
                    });
                });
            });
        });
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────────

    private static byte[] BitmapToRgb32(Bitmap src, int w, int h)
    {
        using var bmp = src.Width == w && src.Height == h
            ? new Bitmap(src) : new Bitmap(src, w, h);
        var bd     = bmp.LockBits(new Rectangle(0, 0, w, h),
            ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        int stride = Math.Abs(bd.Stride);
        var buf    = new byte[stride * h];
        var flipped = new byte[stride * h];
        Marshal.Copy(bd.Scan0, buf, 0, buf.Length);
        bmp.UnlockBits(bd);
        for (int y = 0; y < h; y++)
            Buffer.BlockCopy(buf, y * stride, flipped, (h - 1 - y) * stride, stride);
        return flipped;
    }

    private IMFSample CreateSampleFromRaw(byte[] rgb32, long timestamp, long duration)
    {
        MFCreateMemoryBuffer((uint)rgb32.Length, out IMFMediaBuffer buf);
        buf.Lock(out IntPtr ptr, out _, out _);
        Marshal.Copy(rgb32, 0, ptr, rgb32.Length);
        buf.Unlock();
        buf.SetCurrentLength((uint)rgb32.Length);

        MFCreateSample(out IMFSample sample);
        sample.AddBuffer(buf);
        sample.SetSampleTime(timestamp);
        sample.SetSampleDuration(duration);
        Marshal.ReleaseComObject(buf);
        return sample;
    }

    private static void WriteBox(BinaryWriter w, string fourcc, Action<BinaryWriter> content)
    {
        long startPos = w.BaseStream.Position;
        w.Write(BE32(0));
        w.Write(Ascii4(fourcc.PadRight(4)[..4]));
        using var ms    = new MemoryStream();
        using var inner = new BinaryWriter(ms);
        content(inner);
        inner.Flush();
        w.Write(ms.ToArray());
        long endPos  = w.BaseStream.Position;
        uint boxSize = (uint)(endPos - startPos);
        w.BaseStream.Position = startPos;
        w.Write(BE32(boxSize));
        w.BaseStream.Position = endPos;
    }

    private static void WriteMatrix(BinaryWriter b)
    {
        b.Write(BE32(0x00010000)); b.Write(BE32(0)); b.Write(BE32(0));
        b.Write(BE32(0)); b.Write(BE32(0x00010000)); b.Write(BE32(0));
        b.Write(BE32(0)); b.Write(BE32(0)); b.Write(BE32(0x40000000));
    }

    private static void SetG(IMFMediaType t, Guid k, Guid v) { t.SetGUID(ref k, ref v); }
    private static void SetU(IMFMediaType t, Guid k, uint v) { t.SetUINT32(ref k, v); }
    private static void SetQ(IMFMediaType t, Guid k, int hi, int lo)
        => t.SetUINT64(ref k, ((ulong)(uint)hi << 32) | (uint)lo);

    private static byte[] BE32(uint v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];
    private static byte[] BE16(ushort v) => [(byte)(v >> 8), (byte)v];
    private static byte[] Ascii4(string s) => Encoding.ASCII.GetBytes(s.PadRight(4)[..4]);

    private static void ThrowIfFailed(int hr, string op)
    {
        if (hr < 0) throw new COMException($"H264Mp4Writer: {op} failed (0x{(uint)hr:X8})", hr);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_encoder != null) { Marshal.ReleaseComObject(_encoder); _encoder = null; }
        MFShutdown();
    }

    // ── P/Invoke 補助構造体 ────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct MftRegisterTypeInfo
    {
        public Guid guidMajorType;
        public Guid guidSubtype;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MftOutputDataBuffer
    {
        public uint   dwStreamID;
        public IntPtr pSample;    // IMFSample* (raw to avoid marshaling issues)
        public uint   dwStatus;
        public IntPtr pEvents;
    }

    // ── COM インターフェース ────────────────────────────────────────────────────

    [ComImport, Guid("BF94C121-5B05-4E6F-8000-BA598961414D"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFTransform
    {
        [PreserveSig] int GetStreamLimits(out uint i1, out uint i2, out uint o1, out uint o2);
        [PreserveSig] int GetStreamCount(out uint pcIn, out uint pcOut);
        [PreserveSig] int GetStreamIDs(uint inSz, IntPtr pIn, uint outSz, IntPtr pOut);
        [PreserveSig] int GetInputStreamInfo(uint idx, IntPtr pInfo);
        [PreserveSig] int GetOutputStreamInfo(uint idx, IntPtr pInfo);
        [PreserveSig] int GetAttributes(IntPtr pp);
        [PreserveSig] int GetInputStreamAttributes(uint idx, IntPtr pp);
        [PreserveSig] int GetOutputStreamAttributes(uint idx, IntPtr pp);
        [PreserveSig] int DeleteInputStream(uint idx);
        [PreserveSig] int AddInputStreams(uint c, IntPtr ids);
        [PreserveSig] int GetInputAvailableType(uint idx, uint typeIdx, out IMFMediaType ppType);
        [PreserveSig] int GetOutputAvailableType(uint idx, uint typeIdx, out IMFMediaType ppType);
        [PreserveSig] int SetInputType(uint idx, IMFMediaType pType, uint flags);
        [PreserveSig] int SetOutputType(uint idx, IMFMediaType pType, uint flags);
        [PreserveSig] int GetInputCurrentType(uint idx, out IMFMediaType ppType);
        [PreserveSig] int GetOutputCurrentType(uint idx, out IMFMediaType ppType);
        [PreserveSig] int GetInputStatus(uint idx, out uint flags);
        [PreserveSig] int GetOutputStatus(out uint flags);
        [PreserveSig] int SetOutputBounds(long lo, long hi);
        [PreserveSig] int ProcessEvent(uint idx, IntPtr pEvent);
        [PreserveSig] int ProcessMessage(uint msg, UIntPtr param);
        [PreserveSig] int ProcessInput(uint idx, IMFSample pSample, uint flags);
        [PreserveSig] int ProcessOutput(uint flags, uint count, ref MftOutputDataBuffer pBuffers, out uint pdwStatus);
    }

    // IMFMediaType, IMFSample, IMFMediaBuffer は MfVideoWriter.cs で定義済み
    [ComImport, Guid("44ae0fa8-ea31-4109-8d2e-4cae4997c555"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType
    {
        [PreserveSig] int GetItem(ref Guid k, IntPtr v); [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.IUnknown)] object p, int m, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v); [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v); [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out int l);
        [PreserveSig] int GetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder sb, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] out string s, out int l);
        [PreserveSig] int GetBlobSize(ref Guid k, out int s); [PreserveSig] int GetBlob(ref Guid k, [Out] byte[] b, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out int s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid r, out IntPtr p);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v); [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems(); [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v); [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, [In] byte[] b, int n);
        [PreserveSig] int SetUnknown(ref Guid k, [MarshalAs(UnmanagedType.IUnknown)] object o);
        [PreserveSig] int LockStore(); [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out int n); [PreserveSig] int GetItemByIndex(int i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems([MarshalAs(UnmanagedType.IUnknown)] object d);
        [PreserveSig] int GetMajorType(out Guid g);
        [PreserveSig] int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool b);
        [PreserveSig] int IsEqual(IMFMediaType p, out uint f); [PreserveSig] int GetRepresentation(Guid g, out IntPtr p);
        [PreserveSig] int FreeRepresentation(Guid g, IntPtr p);
    }

    [ComImport, Guid("c40a00f2-b93a-4d80-ae8c-5a1c634f58e4"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        [PreserveSig] int GetItem(ref Guid k, IntPtr v); [PreserveSig] int GetItemType(ref Guid k, out int t);
        [PreserveSig] int CompareItem(ref Guid k, IntPtr v, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int Compare([MarshalAs(UnmanagedType.IUnknown)] object p, int m, [MarshalAs(UnmanagedType.Bool)] out bool r);
        [PreserveSig] int GetUINT32(ref Guid k, out uint v); [PreserveSig] int GetUINT64(ref Guid k, out ulong v);
        [PreserveSig] int GetDouble(ref Guid k, out double v); [PreserveSig] int GetGUID(ref Guid k, out Guid v);
        [PreserveSig] int GetStringLength(ref Guid k, out int l);
        [PreserveSig] int GetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder sb, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] out string s, out int l);
        [PreserveSig] int GetBlobSize(ref Guid k, out int s); [PreserveSig] int GetBlob(ref Guid k, [Out] byte[] b, int n, IntPtr pl);
        [PreserveSig] int GetAllocatedBlob(ref Guid k, out IntPtr b, out int s);
        [PreserveSig] int GetUnknown(ref Guid k, ref Guid r, out IntPtr p);
        [PreserveSig] int SetItem(ref Guid k, IntPtr v); [PreserveSig] int DeleteItem(ref Guid k);
        [PreserveSig] int DeleteAllItems(); [PreserveSig] int SetUINT32(ref Guid k, uint v);
        [PreserveSig] int SetUINT64(ref Guid k, ulong v); [PreserveSig] int SetDouble(ref Guid k, double v);
        [PreserveSig] int SetGUID(ref Guid k, ref Guid v);
        [PreserveSig] int SetString(ref Guid k, [MarshalAs(UnmanagedType.LPWStr)] string v);
        [PreserveSig] int SetBlob(ref Guid k, [In] byte[] b, int n);
        [PreserveSig] int SetUnknown(ref Guid k, [MarshalAs(UnmanagedType.IUnknown)] object o);
        [PreserveSig] int LockStore(); [PreserveSig] int UnlockStore();
        [PreserveSig] int GetCount(out int n); [PreserveSig] int GetItemByIndex(int i, out Guid k, IntPtr v);
        [PreserveSig] int CopyAllItems([MarshalAs(UnmanagedType.IUnknown)] object d);
        [PreserveSig] int GetSampleFlags(out uint f); [PreserveSig] int SetSampleFlags(uint f);
        [PreserveSig] int GetSampleTime(out long t); [PreserveSig] int SetSampleTime(long t);
        [PreserveSig] int GetSampleDuration(out long d); [PreserveSig] int SetSampleDuration(long d);
        [PreserveSig] int GetBufferCount(out int n); [PreserveSig] int GetBufferByIndex(int i, out IMFMediaBuffer b);
        [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer b);
        [PreserveSig] int AddBuffer(IMFMediaBuffer b); [PreserveSig] int RemoveBufferByIndex(int i);
        [PreserveSig] int RemoveAllBuffers(); [PreserveSig] int GetTotalLength(out uint l);
        [PreserveSig] int CopyToBuffer(IMFMediaBuffer b);
    }

    [ComImport, Guid("045FA593-8799-42B8-BC8D-8968C6453507"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig] int Lock(out IntPtr pp, out uint maxLen, out uint curLen);
        [PreserveSig] int Unlock();
        [PreserveSig] int GetCurrentLength(out uint l);
        [PreserveSig] int SetCurrentLength(uint l);
        [PreserveSig] int GetMaxLength(out uint l);
    }
}
