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
    public Task GenerateAsync(ManualSession session)
        => Task.Run(() => Generate(session));

    private void Generate(ManualSession session)
    {
        var cfg = _config.Config.VideoGen;
        if (!cfg.OutputApng && !cfg.OutputMp4)
        {
            Log.Debug("VideoGenerator: 出力フォーマットが無効のためスキップ");
            return;
        }

        var steps = session.Steps
            .Where(s => s.ImagePath != null || true) // 画像なしステップも含む
            .ToList();

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
                GenerateForSteps(cfg, session.Title, session.StartedAt, steps, 0);
            }
            else
            {
                // チャプター分割: ウィンドウタイトルが変わるたびに分割
                var chapters = GroupByChapter(steps);
                for (int i = 0; i < chapters.Count; i++)
                {
                    var (winTitle, chapterSteps) = chapters[i];
                    string title = $"{session.Title}_{(i + 1):D2}_{MakeSlug(winTitle)}";
                    GenerateForSteps(cfg, title, chapterSteps[0].Timestamp, chapterSteps, i + 1);
                }
            }

            _notifier?.ShowBalloon("動画生成完了", session.Title);

            if (cfg.OpenFolderOnComplete)
            {
                string folder = ResolveOutputFolder(cfg);
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
        List<ManualStep> steps, int chapterIndex)
    {
        string folder   = ResolveOutputFolder(cfg);
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
            try
            {
                using var mp4 = new MfVideoWriter(mp4Path, targetSize.Width, targetSize.Height,
                    fps: 1, bitrateMbps: cfg.Mp4VideoBitrateMbps);

                // 最初の WAV から音声パラメーターを設定
                byte[]? firstWav = wavSamples?.FirstOrDefault(w => w != null);
                if (firstWav != null) mp4.EnableAudio(firstWav);

                for (int i = 0; i < steps.Count; i++)
                {
                    double dur = CalcFrameDuration(cfg, steps, i, wavSamples?[i]);
                    var frames = renderer.Render(steps[i], targetSize);
                    try
                    {
                        if (frames.Count > 1)
                        {
                            double rippleDur = 0.15;
                            double mainDur   = Math.Max(dur - rippleDur * (frames.Count - 1), 0.5);
                            mp4.AddVideoFrame(frames[0], mainDur);
                            for (int fi = 1; fi < frames.Count; fi++)
                                mp4.AddVideoFrame(frames[fi], rippleDur);
                        }
                        else
                        {
                            mp4.AddVideoFrame(frames[0], dur);
                        }
                    }
                    finally { foreach (var f in frames) f.Dispose(); }

                    mp4.AddAudioSample(wavSamples?[i]);
                }
                mp4.FinalizeFile();
                Log.Information("MP4 書き出し完了: {Path}", mp4Path);
            }
            catch (Exception ex) { Log.Error(ex, "MP4 生成失敗: {Path}", mp4Path); }
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
            if (string.IsNullOrWhiteSpace(s.ImagePath) || !File.Exists(s.ImagePath)) continue;
            try
            {
                using var bmp = System.Drawing.Image.FromFile(s.ImagePath);
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

    // ── 出力フォルダパス解決 ──────────────────────────────────────────────────
    private string ResolveOutputFolder(VideoGenConfig cfg)
        => string.IsNullOrWhiteSpace(cfg.VideoOutputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AutoScreenshot")
            : cfg.VideoOutputFolder;

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
