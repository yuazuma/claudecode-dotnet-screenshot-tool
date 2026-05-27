using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// APNG (Animated PNG) ファイルを逐次書き込むライター。
/// 仕様: https://wiki.mozilla.org/APNG_Spec
/// </summary>
public sealed class ApngWriter : IDisposable
{
    private readonly string _outputPath;
    private readonly int    _loopCount;   // 0 = 無限ループ
    private readonly List<(byte[] pngBytes, int delayNum, int delayDen)> _frames = [];

    public ApngWriter(string outputPath, int loopCount = 0)
    {
        _outputPath = outputPath;
        _loopCount  = loopCount;
    }

    /// <summary>フレームを追加する。durationSeconds は表示秒数。</summary>
    public void AddFrame(Bitmap bmp, double durationSeconds)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        byte[] pngBytes = ms.ToArray();

        // 分数表現: num/den = durationSeconds (den=1000 で ms 精度)
        int den = 1000;
        int num = (int)Math.Round(durationSeconds * den);
        num = Math.Max(num, 1);
        _frames.Add((pngBytes, num, den));
    }

    /// <summary>APNG ファイルを書き出して確定する。</summary>
    public void Finalize(string outputPath)
    {
        if (_frames.Count == 0)
        {
            Log.Warning("ApngWriter: フレーム0件のため書き出しをスキップします");
            return;
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var fs = File.Create(outputPath);
            WriteApng(fs);
            Log.Information("APNG 書き出し完了: {Path} ({Frames}フレーム)", outputPath, _frames.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "APNG 書き出し失敗: {Path}", outputPath);
            throw;
        }
    }

    // ────────────────────────────────────────────────────────────────────────────
    // APNG バイナリ生成
    // ────────────────────────────────────────────────────────────────────────────
    private void WriteApng(Stream output)
    {
        // 最初のフレームから IHDR とデータチャンクを取得
        (int width, int height, byte[] idat0) = ExtractPngCore(_frames[0].pngBytes);

        uint seqNum = 0;

        // PNG シグネチャ
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        // IHDR (最初フレームの IHDR をそのまま使用)
        WriteIhdrChunk(output, width, height);

        // acTL (Animation Control Chunk)
        WriteActl(output, _frames.Count, _loopCount);

        // フレームごとに fcTL + IDAT/fdAT を書く
        for (int i = 0; i < _frames.Count; i++)
        {
            (byte[] pngBytes, int delayNum, int delayDen) = _frames[i];
            (_, _, byte[] idat) = ExtractPngCore(pngBytes);

            // fcTL (Frame Control Chunk)
            WriteFctl(output, ref seqNum, width, height, 0, 0, (ushort)delayNum, (ushort)delayDen);

            if (i == 0)
            {
                // 先頭フレームは IDAT（非 APNG ビューアでも表示できる）
                WriteChunk(output, "IDAT", idat);
            }
            else
            {
                // 後続フレームは fdAT（シーケンス番号 + データ）
                WriteFdat(output, ref seqNum, idat);
            }
        }

        // IEND
        WriteChunk(output, "IEND", []);
    }

    private static void WriteIhdrChunk(Stream s, int width, int height)
    {
        var data = new byte[13];
        WriteInt32BE(data, 0, width);
        WriteInt32BE(data, 4, height);
        data[8] = 8;  // bit depth
        data[9] = 2;  // color type: RGB
        // compression/filter/interlace = 0
        WriteChunk(s, "IHDR", data);
    }

    private static void WriteActl(Stream s, int numFrames, int numPlays)
    {
        var data = new byte[8];
        WriteInt32BE(data, 0, numFrames);
        WriteInt32BE(data, 4, numPlays);
        WriteChunk(s, "acTL", data);
    }

    private static void WriteFctl(Stream s, ref uint seq, int w, int h, int x, int y,
        ushort delayNum, ushort delayDen)
    {
        var data = new byte[26];
        WriteInt32BE(data, 0, (int)seq++);
        WriteInt32BE(data, 4, w);
        WriteInt32BE(data, 8, h);
        WriteInt32BE(data, 12, x);
        WriteInt32BE(data, 16, y);
        WriteInt16BE(data, 20, delayNum);
        WriteInt16BE(data, 22, delayDen);
        data[24] = 0; // dispose op: none
        data[25] = 0; // blend op: source
        WriteChunk(s, "fcTL", data);
    }

    private static void WriteFdat(Stream s, ref uint seq, byte[] idat)
    {
        var data = new byte[4 + idat.Length];
        WriteInt32BE(data, 0, (int)seq++);
        Buffer.BlockCopy(idat, 0, data, 4, idat.Length);
        WriteChunk(s, "fdAT", data);
    }

    // チャンク書き込み: length(4) + type(4) + data + CRC(4)
    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        byte[] lenBytes = new byte[4];
        WriteInt32BE(lenBytes, 0, data.Length);
        s.Write(lenBytes);

        byte[] typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);

        // CRC32 over type + data
        uint crc = Crc32(typeBytes);
        crc = Crc32Continue(crc, data);
        byte[] crcBytes = new byte[4];
        WriteInt32BE(crcBytes, 0, (int)crc);
        s.Write(crcBytes);
    }

    // PNG ファイルから生の IDAT データを抽出する（複数 IDAT は連結）
    private static (int width, int height, byte[] idat) ExtractPngCore(byte[] png)
    {
        int width = 0, height = 0;
        var idatData = new List<byte[]>();
        int pos = 8; // シグネチャをスキップ

        while (pos + 12 <= png.Length)
        {
            int length = ReadInt32BE(png, pos);       pos += 4;
            string type = System.Text.Encoding.ASCII.GetString(png, pos, 4); pos += 4;

            if (type == "IHDR")
            {
                width  = ReadInt32BE(png, pos);
                height = ReadInt32BE(png, pos + 4);
            }
            else if (type == "IDAT")
            {
                var chunk = new byte[length];
                Buffer.BlockCopy(png, pos, chunk, 0, length);
                idatData.Add(chunk);
            }
            pos += length + 4; // data + CRC
        }

        // IDAT チャンクを連結（zlib ストリームは連続しているため単純連結でOK）
        int total = idatData.Sum(b => b.Length);
        var merged = new byte[total];
        int offset = 0;
        foreach (var b in idatData) { Buffer.BlockCopy(b, 0, merged, offset, b.Length); offset += b.Length; }

        return (width, height, merged);
    }

    // ── ユーティリティ ───────────────────────────────────────────────────────
    private static void WriteInt32BE(byte[] buf, int offset, int value)
    {
        buf[offset]     = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)(value);
    }

    private static void WriteInt16BE(byte[] buf, int offset, ushort value)
    {
        buf[offset]     = (byte)(value >> 8);
        buf[offset + 1] = (byte)(value);
    }

    private static int ReadInt32BE(byte[] buf, int offset)
        => (buf[offset] << 24) | (buf[offset + 1] << 16) | (buf[offset + 2] << 8) | buf[offset + 3];

    // CRC32 (ISO 3309)
    private static readonly uint[] CrcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }
    private static uint Crc32(byte[] data) => Crc32Continue(0xFFFFFFFFu, data) ^ 0xFFFFFFFFu;
    private static uint Crc32Continue(uint crc, byte[] data)
    {
        foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    public void Dispose() { }
}
