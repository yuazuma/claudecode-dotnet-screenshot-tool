using System.Drawing;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// ManualSession を受け取り、APNG / MP4 を生成する統括サービス。
/// バックグラウンドスレッドで実行する (NF-V02)。
/// </summary>
public class VideoGenerator
{
    private readonly ConfigStore _config;
    private readonly Notifier?   _notifier;

    public VideoGenerator(ConfigStore config, Notifier? notifier = null)
    {
        _config   = config;
        _notifier = notifier;
    }

    /// <summary>セッションから動画を非同期生成する。</summary>
    public Task GenerateAsync(ManualSession session,
        IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Generate(session, progress, ct), ct);

    private void Generate(ManualSession session,
        IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
    {
        var cfg = _config.Config.VideoGen;
        if (!cfg.OutputApng && !cfg.OutputMp4)
        {
            Log.Debug("VideoGenerator: 出力フォーマットが無効のためスキップ");
            return;
        }

        var steps = session.Steps.ToList();

        if (steps.Count == 0)
        {
            Log.Information("VideoGenerator: ステップ0件のためスキップ");
            return;
        }

        _notifier?.ShowBalloon("動画生成を開始しました", $"{session.Title} ({steps.Count} ステップ)");

        try
        {
            if (cfg.VideoUnit == VideoUnit.Session)
            {
                GenerateForSteps(cfg, session.Title, session.StartedAt, steps, 0, progress, ct);
            }
            else
            {
                // チャプター分割: ウィンドウタイトルが変わるたびに分割
                var chapters = GroupByChapter(steps);
                for (int i = 0; i < chapters.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var (winTitle, chapterSteps) = chapters[i];
                    string title = $"{session.Title}_{(i + 1):D2}_{MakeSlug(winTitle)}";
                    GenerateForSteps(cfg, title, chapterSteps[0].Timestamp, chapterSteps, i + 1, progress, ct);
                }
            }

            _notifier?.ShowBalloon("動画生成完了", session.Title);

            if (cfg.OpenFolderOnComplete)
            {
                string folder = ResolveOutputFolder(cfg, session);
                if (Directory.Exists(folder))
                    System.Diagnostics.Process.Start("explorer.exe", folder);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "動画生成に失敗しました: {Title}", session.Title);
            _notifier?.ShowBalloon("動画生成エラー", ex.Message);
        }
    }

    private void GenerateForSteps(VideoGenConfig cfg, string title, DateTime startedAt,
        List<ManualStep> steps, int chapterIndex,
        IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
    {
        // title/startedAt を使って簡易 ManualSession を組む（フォルダ解決用）
        var sessionMock = new ManualSession { Title = title };
        string folder   = ResolveOutputFolder(cfg, sessionMock);
        Directory.CreateDirectory(folder);
        string slug     = MakeSlug(title);
        string ts       = startedAt.ToString("yyyyMMdd_HHmmss");
        string baseName = $"{slug}_{ts}";

        Size targetSize = ResolveTargetSize(cfg, steps);

        // TTS 音声を全ステップ分一括生成
        byte[]?[]? wavSamples = null;
        if (cfg.TtsEnabled && cfg.OutputMp4)
        {
            using var tts = new TtsService(cfg);
            wavSamples = tts.SynthesizeAll(steps);
        }

        var renderer = new FrameRenderer(cfg);

        // ── APNG 生成 ──────────────────────────────────────────────────────
        if (cfg.OutputApng)
        {
            string apngPath = Path.Combine(folder, baseName + ".png");
            var apng = new ApngWriter(apngPath);
            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(new ExportProgress("APNG を生成中...", i + 1, steps.Count, apngPath));
                    double dur = CalcFrameDuration(cfg, steps, i, wavSamples?[i]);
                    var frames = renderer.Render(steps[i], targetSize);
                    try
                    {
                        // 波紋フレームがある場合、各フレームを短い時間で表示
                        if (frames.Count > 1)
                        {
                            double rippleDur = 0.15;
                            double mainDur   = Math.Max(dur - rippleDur * (frames.Count - 1), 0.5);
                            apng.AddFrame(frames[0], mainDur);
                            for (int fi = 1; fi < frames.Count; fi++)
                                apng.AddFrame(frames[fi], rippleDur);
                        }
                        else
                        {
                            apng.AddFrame(frames[0], dur);
                        }
                    }
                    finally { foreach (var f in frames) f.Dispose(); }
                }
                apng.Finalize(apngPath);
            }
            catch (Exception ex) { Log.Error(ex, "APNG 生成失敗: {Path}", apngPath); }
        }

        // ── MP4 生成 ───────────────────────────────────────────────────────
        if (cfg.OutputMp4)
        {
            string mp4Path = Path.Combine(folder, baseName + ".mp4");

            // フレームをキャッシュしておき H.264 失敗時に MJPEG でリトライする
            var cachedFrames = new List<(List<System.Drawing.Bitmap> bitmaps, double dur)>();

            // Step 1: フレームをレンダリングしてキャッシュ
            for (int i = 0; i < steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                double dur = CalcFrameDuration(cfg, steps, i, wavSamples?[i]);
                var bitmaps = renderer.Render(steps[i], targetSize);
                cachedFrames.Add((bitmaps, dur));
            }

            // Step 2: H.264 でエンコード（失敗時は MJPEG にフォールバック）
            bool mp4Success = false;

            // Step 2a: まず Media Foundation (H.264) で試みる
            try
            {
                using var mp4 = new MfVideoWriter(mp4Path, targetSize.Width, targetSize.Height,
                    fps: 1, bitrateMbps: cfg.Mp4VideoBitrateMbps);

                byte[]? firstWav = wavSamples?.FirstOrDefault(w => w != null);
                if (firstWav != null) mp4.EnableAudio(firstWav);

                for (int i = 0; i < cachedFrames.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report(new ExportProgress("MP4 を生成中...", i + 1, cachedFrames.Count, mp4Path));
                    var (bitmaps, dur) = cachedFrames[i];
                    if (bitmaps.Count > 1)
                    {
                        double rippleDur = 0.15;
                        double mainDur   = Math.Max(dur - rippleDur * (bitmaps.Count - 1), 0.5);
                        mp4.AddVideoFrame(bitmaps[0], mainDur);
                        for (int fi = 1; fi < bitmaps.Count; fi++)
                            mp4.AddVideoFrame(bitmaps[fi], rippleDur);
                    }
                    else
                    {
                        mp4.AddVideoFrame(bitmaps[0], dur);
                    }
                    mp4.AddAudioSample(wavSamples?[i]);
                }
                mp4.FinalizeFile();
                Log.Information("MP4 (H.264) 書き出し完了: {Path}", mp4Path);
                mp4Success = true;
            }
            catch (Exception exH264)
            {
                Log.Warning(exH264, "MP4 (H.264) 失敗。純 .NET MJPEG-in-MP4 にフォールバックします: {Path}", mp4Path);
                try { if (File.Exists(mp4Path)) File.Delete(mp4Path); } catch { }
            }

            // Step 2b: IMFSinkWriter H.264 が失敗した場合は H264Mp4Writer（直接 MFT + 自前 MP4 コンテナ）で再試行
            if (!mp4Success)
            {
                try
                {
                    using var h264 = new H264Mp4Writer(mp4Path, targetSize.Width, targetSize.Height,
                        fps: 1, bitrateMbps: cfg.Mp4VideoBitrateMbps);

                    for (int i = 0; i < cachedFrames.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Report(new ExportProgress("MP4 (H.264直接) を生成中...", i + 1, cachedFrames.Count, mp4Path));
                        var (bitmaps, dur) = cachedFrames[i];  // dur は秒単位
                        if (bitmaps.Count > 1)
                        {
                            double rippleDur = 0.15;
                            double mainDur   = Math.Max(dur - rippleDur * (bitmaps.Count - 1), 0.5);
                            h264.AddVideoFrame(bitmaps[0], mainDur);
                            for (int fi = 1; fi < bitmaps.Count; fi++)
                                h264.AddVideoFrame(bitmaps[fi], rippleDur);
                        }
                        else
                        {
                            h264.AddVideoFrame(bitmaps[0], dur);
                        }
                    }
                    h264.FinalizeFile();
                    Log.Information("MP4 (H.264直接 MFT) 書き出し完了: {Path}", mp4Path);
                    mp4Success = true;
                }
                catch (Exception exH264Direct)
                {
                    Log.Error(exH264Direct, "MP4 (H.264直接 MFT) も失敗: {Path}", mp4Path);
                    try { if (File.Exists(mp4Path)) File.Delete(mp4Path); } catch { }
                }
            }

            // Step 2c: FFmpeg 経由で H.264 MP4（H.264 MFT が存在しない Azure Server 等向け）
            if (!mp4Success)
            {
                try
                {
                    using var ffmpeg = new FfmpegMp4Writer(mp4Path, targetSize.Width, targetSize.Height);

                    for (int i = 0; i < cachedFrames.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Report(new ExportProgress("MP4 (FFmpeg) を生成中...", i + 1, cachedFrames.Count, mp4Path));
                        var (bitmaps, dur) = cachedFrames[i];  // dur は秒単位
                        if (bitmaps.Count > 1)
                        {
                            double rippleDur = 0.15;
                            double mainDur   = Math.Max(dur - rippleDur * (bitmaps.Count - 1), 0.5);
                            ffmpeg.AddFrame(bitmaps[0], mainDur);
                            for (int fi = 1; fi < bitmaps.Count; fi++)
                                ffmpeg.AddFrame(bitmaps[fi], rippleDur);
                        }
                        else
                        {
                            ffmpeg.AddFrame(bitmaps[0], dur);
                        }
                    }
                    ffmpeg.FinalizeFile();
                    Log.Information("MP4 (FFmpeg H.264) 書き出し完了: {Path}", mp4Path);
                    mp4Success = true;
                }
                catch (Exception exFfmpeg)
                {
                    Log.Error(exFfmpeg, "MP4 (FFmpeg) も失敗: {Path}", mp4Path);
                    try { if (File.Exists(mp4Path)) File.Delete(mp4Path); } catch { }
                }
            }

            if (!mp4Success)
            {
                Log.Error(
                    "MP4 の生成に失敗しました。H.264 エンコーダーが必要です。" +
                    "ffmpeg.exe をアプリフォルダまたは PATH に配置するか、" +
                    "管理者権限で DISM /Online /Add-Capability /CapabilityName:Media.MediaFeaturePack~~~~0.0.1.0 を実行してください。");
            }

            // キャッシュしたビットマップを解放
            foreach (var (bitmaps, _) in cachedFrames)
                foreach (var b in bitmaps) b.Dispose();
        }
    }

    // ── フレーム表示時間の計算（FR-V03）──────────────────────────────────────
    private static double CalcFrameDuration(VideoGenConfig cfg, List<ManualStep> steps, int i, byte[]? wav)
    {
        double dur;
        if (cfg.FrameTimingMode == FrameTimingMode.Fixed)
        {
            dur = cfg.FixedFrameSeconds;
            // TTS 読み上げ時間が設定値より長い場合は延長（FR-V06-6）
            if (wav != null)
                dur = Math.Max(dur, TtsService.GetWavDurationSeconds(wav));
        }
        else
        {
            // 実時間再現: 次のステップとの差分（秒）
            if (i + 1 < steps.Count)
            {
                double elapsed = (steps[i + 1].Timestamp - steps[i].Timestamp).TotalSeconds;
                dur = Math.Clamp(elapsed, cfg.MinFrameSeconds, cfg.MaxFrameSeconds);
            }
            else
            {
                dur = cfg.FixedFrameSeconds; // 最終フレームは固定値
            }
        }
        return Math.Max(dur, 0.1);
    }

    // ── 出力解像度の決定（FR-V09）────────────────────────────────────────────
    private static Size ResolveTargetSize(VideoGenConfig cfg, List<ManualStep> steps)
    {
        return cfg.OutputResolution switch
        {
            VideoResolution.Fhd => new Size(1920, 1080),
            VideoResolution.Hd  => new Size(1280, 720),
            _                   => GetSourceSize(steps), // Original
        };
    }

    private static Size GetSourceSize(List<ManualStep> steps)
    {
        // 最初に見つかった有効な画像のサイズを使用
        foreach (var s in steps)
        {
            if (string.IsNullOrWhiteSpace(s.AfterImagePath) || !File.Exists(s.AfterImagePath)) continue;
            try
            {
                using var bmp = System.Drawing.Image.FromFile(s.AfterImagePath);
                return new Size(bmp.Width % 2 == 0 ? bmp.Width : bmp.Width - 1,
                                bmp.Height % 2 == 0 ? bmp.Height : bmp.Height - 1);
            }
            catch { /* 読み込み失敗は次を試す */ }
        }
        return new Size(1920, 1080); // フォールバック
    }

    // ── チャプター分割（ウィンドウ単位）──────────────────────────────────────
    private static List<(string WindowTitle, List<ManualStep> Steps)> GroupByChapter(List<ManualStep> steps)
    {
        var chapters = new List<(string, List<ManualStep>)>();
        string? curWin = null;
        List<ManualStep>? cur = null;
        foreach (var s in steps)
        {
            if (curWin != s.WindowTitle || cur == null)
            {
                cur = [];
                chapters.Add((s.WindowTitle, cur));
                curWin = s.WindowTitle;
            }
            cur.Add(s);
        }
        return chapters;
    }

    // ── 出力フォルダパス解決（FR-H3: テンプレート評価） ──────────────────────
    private string ResolveOutputFolder(VideoGenConfig cfg, ManualSession session)
    {
        string baseFolder = !string.IsNullOrWhiteSpace(cfg.VideoBaseFolder)
            ? cfg.VideoBaseFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AutoScreenshot");
        string subFolder = FolderTemplateService.Evaluate(
            cfg.VideoFolderTemplate, session.StartedAt, session.Title);
        return string.IsNullOrEmpty(subFolder)
            ? baseFolder
            : Path.Combine(baseFolder, subFolder);
    }

    // ── スラッグ生成 ──────────────────────────────────────────────────────────
    private static string MakeSlug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else if (c == ' ' || c == '-')            sb.Append('_');
        }
        string s = sb.ToString().TrimStart('_');
        return s.Length > 40 ? s[..40] : (s.Length == 0 ? "video" : s);
    }
}
