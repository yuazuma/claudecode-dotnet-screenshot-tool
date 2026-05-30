using System.Drawing;
using System.Drawing.Imaging;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// MJPEG-in-AVI ライター。
/// AVI 形式は MJPEG を事実上全プレイヤー（Windows Media Player、VLC 等）がネイティブ対応する。
/// System.Drawing.Imaging の JPEG エンコーダーのみ使用し、外部コーデック不要。
/// H.264 が利用できない環境（Azure Windows Server 等）での動画出力フォールバック。
/// </summary>
public sealed class MjpegAviWriter : IDisposable
{
    private readonly string _outputPath;
    private readonly int    _fps;
    private readonly long   _jpegQuality;
    private readonly int    _width;
    private readonly int    _height;

    private readonly List<byte[]> _frames = [];
    private bool _disposed;

    public MjpegAviWriter(string outputPath, int width, int height, int fps, int jpegQuality = 85)
    {
        _outputPath  = outputPath;
        _fps         = fps;
        _jpegQuality = jpegQuality;
        _width       = width;
        _height      = height;
    }

    /// <summary>フレームを JPEG にエンコードして蓄積する。</summary>
    public void AddFrame(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        var ep = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(Encoder.Quality, _jpegQuality);
        var codec = ImageCodecInfo.GetImageEncoders()
            .FirstOrDefault(c => c.MimeType == "image/jpeg");
        if (codec != null)
            bmp.Save(ms, codec, ep);
        else
            bmp.Save(ms, ImageFormat.Jpeg);
        _frames.Add(ms.ToArray());
    }

    /// <summary>蓄積したフレームを RIFF AVI (MJPEG) ファイルとして書き出す。</summary>
    public void FinalizeFile()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("フレームが 0 件です。");

        using var fs = new FileStream(_outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs, System.Text.Encoding.ASCII, leaveOpen: true);

        uint microSecPerFrame = (uint)(1_000_000 / _fps);
        int  maxFrameSize     = _frames.Max(f => f.Length);
        int  totalFrames      = _frames.Count;

        // ── サイズ定数（修正済み）──────────────────────────────────────────────
        // avih chunk      = 4('avih') + 4(size) + 56(data)  = 64 bytes
        // strh chunk      = 4('strh') + 4(size) + 56(data)  = 64 bytes
        // strf chunk      = 4('strf') + 4(size) + 40(data)  = 48 bytes
        // strl LIST       = 4('LIST') + 4(size) + 4('strl') + 64(strh) + 48(strf) = 124 bytes
        //   strl size field = 4('strl') + 64 + 48 = 116
        // hdrl LIST       = 4('LIST') + 4(size) + 4('hdrl') + 64(avih) + 124(strl) = 200 bytes
        //   hdrl size field = 4('hdrl') + 64 + 124 = 192

        const uint avihChunkTotal = 64u;   // avih chunk (header + data)
        const uint strhChunkTotal = 64u;   // strh chunk
        const uint strfChunkTotal = 48u;   // strf chunk
        const uint strlListTotal  = 4u + 4u + 4u + strhChunkTotal + strfChunkTotal; // 124
        const uint strlSizeField  = 4u + strhChunkTotal + strfChunkTotal;           // 116
        const uint hdrlListTotal  = 4u + 4u + 4u + avihChunkTotal + strlListTotal;  // 200
        const uint hdrlSizeField  = 4u + avihChunkTotal + strlListTotal;            // 192

        // ── movi フレームのオフセット計算 ──────────────────────────────────────
        // idx1.dwOffset は 'movi' FourCC の直後を起点とする相対位置。
        var frameOffsets = new List<uint>(_frames.Count);
        uint moviDataOffset = 0;
        foreach (var frame in _frames)
        {
            frameOffsets.Add(moviDataOffset);
            uint chunkSize = (uint)(8 + frame.Length + (frame.Length % 2)); // word-aligned
            moviDataOffset += chunkSize;
        }
        uint moviDataSize = moviDataOffset;
        uint moviSizeField = 4u + moviDataSize;   // 'movi' tag + data

        uint idx1DataSize = (uint)(16 * totalFrames);

        // RIFF ファイル全体のサイズ（RIFF header の size フィールド）
        uint riffData = hdrlListTotal + (4u + 4u + moviSizeField) + (4u + 4u + idx1DataSize);

        // ── RIFF AVI ヘッダー ──────────────────────────────────────────────────
        bw.Write(Ascii4("RIFF"));
        bw.Write(LE32(riffData));
        bw.Write(Ascii4("AVI "));

        // ── LIST hdrl ───────────────────────────────────────────────────────────
        bw.Write(Ascii4("LIST"));
        bw.Write(LE32(hdrlSizeField));   // = 192
        bw.Write(Ascii4("hdrl"));

        // avih (AVIMAINHEADER, 56 bytes data)
        bw.Write(Ascii4("avih"));
        bw.Write(LE32(56));
        bw.Write(LE32(microSecPerFrame));
        bw.Write(LE32((uint)(maxFrameSize * _fps)));
        bw.Write(LE32(0));                           // dwPaddingGranularity
        bw.Write(LE32(0x10));                        // dwFlags: AVIF_HASINDEX
        bw.Write(LE32((uint)totalFrames));
        bw.Write(LE32(0));                           // dwInitialFrames
        bw.Write(LE32(1));                           // dwStreams
        bw.Write(LE32((uint)maxFrameSize));          // dwSuggestedBufferSize
        bw.Write(LE32((uint)_width));
        bw.Write(LE32((uint)_height));
        bw.Write(LE32(0)); bw.Write(LE32(0));
        bw.Write(LE32(0)); bw.Write(LE32(0));

        // LIST strl
        bw.Write(Ascii4("LIST"));
        bw.Write(LE32(strlSizeField));   // = 116
        bw.Write(Ascii4("strl"));

        // strh (AVISTREAMHEADER, 56 bytes data)
        bw.Write(Ascii4("strh"));
        bw.Write(LE32(56));
        bw.Write(Ascii4("vids"));
        bw.Write(Ascii4("MJPG"));
        bw.Write(LE32(0));
        bw.Write(LE16(0)); bw.Write(LE16(0));
        bw.Write(LE32(0));
        bw.Write(LE32(1));                           // dwScale
        bw.Write(LE32((uint)_fps));                  // dwRate
        bw.Write(LE32(0));
        bw.Write(LE32((uint)totalFrames));
        bw.Write(LE32((uint)maxFrameSize));
        bw.Write(unchecked((uint)-1));               // dwQuality
        bw.Write(LE32(0));
        bw.Write(LE16(0)); bw.Write(LE16(0));
        bw.Write(LE16((ushort)_width)); bw.Write(LE16((ushort)_height));

        // strf (BITMAPINFOHEADER, 40 bytes data)
        bw.Write(Ascii4("strf"));
        bw.Write(LE32(40));
        bw.Write(LE32(40));
        bw.Write(LE32((uint)_width));
        bw.Write(LE32((uint)_height));
        bw.Write(LE16(1));
        bw.Write(LE16(24));
        bw.Write(Ascii4("MJPG"));
        bw.Write(LE32((uint)maxFrameSize));
        bw.Write(LE32(0)); bw.Write(LE32(0));
        bw.Write(LE32(0)); bw.Write(LE32(0));

        // ── LIST movi ───────────────────────────────────────────────────────────
        bw.Write(Ascii4("LIST"));
        bw.Write(LE32(moviSizeField));
        bw.Write(Ascii4("movi"));

        foreach (var frame in _frames)
        {
            bw.Write(Ascii4("00dc"));
            bw.Write(LE32((uint)frame.Length));
            bw.Write(frame);
            if (frame.Length % 2 != 0) bw.Write((byte)0);
        }

        // ── idx1 ───────────────────────────────────────────────────────────────
        bw.Write(Ascii4("idx1"));
        bw.Write(LE32(idx1DataSize));
        for (int i = 0; i < _frames.Count; i++)
        {
            bw.Write(Ascii4("00dc"));
            bw.Write(LE32(0x10));                    // AVIIF_KEYFRAME
            bw.Write(LE32(frameOffsets[i]));         // 'movi' 直後からの相対オフセット（+4 不要）
            bw.Write(LE32((uint)_frames[i].Length));
        }

        Log.Information("MjpegAviWriter: {Count} フレームを AVI (MJPEG) に書き出しました: {Path}",
            _frames.Count, _outputPath);
    }

    // ── ヘルパー（AVI は Little-Endian）──────────────────────────────────────

    private static byte[] LE32(uint v) => BitConverter.GetBytes(v);
    private static byte[] LE16(ushort v) => BitConverter.GetBytes(v);
    private static byte[] Ascii4(string s) =>
        System.Text.Encoding.ASCII.GetBytes(s.PadRight(4)[..4]);

    public void Dispose() => _disposed = true;
}
