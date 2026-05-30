using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// FFmpeg を使用して H.264 MP4 を生成するライター。
/// Media Foundation の H.264 エンコーダーが利用できない環境（Azure Windows Server 等）向けフォールバック。
/// 各フレームを JPEG として一時フォルダに書き出し、concat demuxer 経由で FFmpeg を呼び出す。
/// </summary>
public sealed class FfmpegMp4Writer : IDisposable
{
    private readonly string _outputPath;
    private readonly int    _width;
    private readonly int    _height;
    private readonly string _tempDir;
    private readonly List<(string Path, double Duration)> _frames = [];
    private bool _disposed;

    public FfmpegMp4Writer(string outputPath, int width, int height)
    {
        _outputPath = outputPath;
        _width      = width  % 2 == 0 ? width  : width  - 1;
        _height     = height % 2 == 0 ? height : height - 1;
        _tempDir    = Path.Combine(Path.GetTempPath(), $"asc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    /// <summary>フレームを JPEG として蓄積する。durationSeconds は表示秒数。</summary>
    public void AddFrame(Bitmap bmp, double durationSeconds)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FfmpegMp4Writer));

        string framePath = Path.Combine(_tempDir, $"{_frames.Count:D5}.jpg");

        // JPEG は alpha チャンネル非対応のため 24bppRgb に変換してから保存
        using var rgb = new Bitmap(_width, _height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(rgb))
        {
            g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.DrawImage(bmp, 0, 0, _width, _height);
        }

        var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
        var ep    = new EncoderParameters(1);
        ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);

        if (codec != null) rgb.Save(framePath, codec, ep);
        else               rgb.Save(framePath, ImageFormat.Jpeg);

        _frames.Add((framePath, Math.Max(durationSeconds, 0.04)));
    }

    /// <summary>FFmpeg を呼び出して MP4 を生成する。</summary>
    public void FinalizeFile()
    {
        if (_frames.Count == 0)
            throw new InvalidOperationException("フレームが 0 件です。");

        string? ffmpegPath = FindFfmpeg();
        if (ffmpegPath == null)
            throw new FileNotFoundException(
                "ffmpeg.exe が見つかりません。" +
                "FFmpeg を PATH の通った場所またはアプリと同じフォルダに配置するか、" +
                "管理者権限で次のコマンドを実行して Media Feature Pack をインストールしてください: " +
                "DISM /Online /Add-Capability /CapabilityName:Media.MediaFeaturePack~~~~0.0.1.0");

        // FFmpeg concat demuxer 形式のリストファイルを作成
        // 最後のフレームを2回書くことで最終フレームの duration が正しく反映される
        string concatPath = Path.Combine(_tempDir, "concat.txt");
        var sb = new StringBuilder();
        sb.AppendLine("ffconcat version 1.0");
        foreach (var (path, dur) in _frames)
        {
            sb.AppendLine($"file '{EscapeFfmpegPath(path)}'");
            sb.AppendLine($"duration {dur:F6}");
        }
        // 末尾に最終フレームを duration なしで再掲（FFmpeg の要件）
        sb.AppendLine($"file '{EscapeFfmpegPath(_frames[^1].Path)}'");
        File.WriteAllText(concatPath, sb.ToString(), new UTF8Encoding(false));

        string args = string.Join(" ",
            "-f concat",
            "-safe 0",
            $"-i \"{concatPath}\"",
            "-c:v libx264",
            "-pix_fmt yuv420p",
            "-movflags +faststart",
            $"-vf scale={_width}:{_height}",
            "-y",
            $"\"{_outputPath}\"");

        Log.Debug("FfmpegMp4Writer: {Ffmpeg} {Args}", ffmpegPath, args);

        var psi = new ProcessStartInfo
        {
            FileName               = ffmpegPath,
            Arguments              = args,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("FFmpeg プロセスを起動できませんでした。");
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            string tail = stderr.Length > 800 ? stderr[^800..] : stderr;
            throw new Exception($"FFmpeg 終了コード {proc.ExitCode}: {tail}");
        }

        Log.Information("FfmpegMp4Writer: MP4 書き出し完了 ({Frames} フレーム) → {Path}",
            _frames.Count, _outputPath);
    }

    // ── FFmpeg 検索 ─────────────────────────────────────────────────────────────

    private static string? FindFfmpeg()
    {
        // 1. アプリ直下 / tools/ サブフォルダ
        foreach (var rel in new[] { "ffmpeg.exe", @"tools\ffmpeg.exe" })
        {
            string p = Path.Combine(AppContext.BaseDirectory, rel);
            if (File.Exists(p)) return p;
        }

        // 2. よく使われるインストールパス
        foreach (var abs in new[]
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            @"C:\tools\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-7.1-full_build\bin\ffmpeg.exe"),
        })
        {
            if (File.Exists(abs)) return abs;
        }

        // 3. PATH 環境変数
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                string p = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(p)) return p;
            }
        }

        return null;
    }

    // FFmpeg concat ファイル内のパスエスケープ（シングルクォートとバックスラッシュ）
    private static string EscapeFfmpegPath(string path) =>
        path.Replace("\\", "/").Replace("'", "\\'");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* 一時ファイルは揮発性 */ }
    }
}
