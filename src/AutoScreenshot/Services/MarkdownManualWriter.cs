using System.Text;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>ManualSession を Markdown ファイルに書き出す</summary>
public class MarkdownManualWriter
{
    public async Task WriteAsync(ManualSession session, string outputPath,
        int chapterTimeGapMinutes = 5, string templatePath = "",
        IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string outputDir = Path.GetDirectoryName(outputPath)!;

        // MD ファイルと同階層に _images/ サブフォルダを作成して画像をコピーする。
        // これにより "../" を使わず "xxx_images/yyy.png" 形式の相対パスにでき、
        // VS Code・GitHub 等の Markdown ビューアで画像が表示される。
        string mdBase        = Path.GetFileNameWithoutExtension(outputPath);
        string imagesDirName = mdBase + "_images";
        string imagesDir     = Path.Combine(outputDir, imagesDirName);
        // ソースパス → 相対パスのキャッシュ（同一ファイルの重複コピーを防止）
        var imageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();

        // 表紙
        sb.AppendLine($"# {session.Title}");
        sb.AppendLine();
        sb.AppendLine($"- **開始日時**: {session.StartedAt:yyyy-MM-dd HH:mm:ss}");
        if (session.EndedAt.HasValue)
            sb.AppendLine($"- **終了日時**: {session.EndedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **OS / ユーザー**: {session.OsInfo}");
        sb.AppendLine($"- **作成ツール**: AutoScreenshot v1.0");
        sb.AppendLine($"- **セッションID**: {session.SessionId:N}");
        sb.AppendLine();

        // ダイジェスト (LLM が生成した要約 D-01, L-04)
        if (!string.IsNullOrWhiteSpace(session.Digest))
        {
            sb.AppendLine("## 操作内容サマリー");
            sb.AppendLine();
            sb.AppendLine(session.Digest);
            sb.AppendLine();
        }

        // 目次（チャプター一覧）
        var chapters = BuildChapters(session.Steps, chapterTimeGapMinutes);
        if (chapters.Count > 0)
        {
            sb.AppendLine("## 目次");
            sb.AppendLine();
            for (int i = 0; i < chapters.Count; i++)
            {
                string label = chapters[i].DisplayTitle;
                string anchor = ToAnchor($"{i + 1}-{label}");
                sb.AppendLine($"{i + 1}. [{i + 1}. {label}](#{anchor})");
            }
            sb.AppendLine();
        }

        // 本文
        int globalStep = 0;
        int totalSteps = session.Steps.Count;
        for (int ci = 0; ci < chapters.Count; ci++)
        {
            var chapter = chapters[ci];
            sb.AppendLine($"## {ci + 1}. {chapter.DisplayTitle}");
            sb.AppendLine();

            DateTime? lastStepTime = null;
            foreach (var step in chapter.Steps)
            {
                ct.ThrowIfCancellationRequested();

                // 時間ギャップで小見出し
                if (lastStepTime.HasValue &&
                    (step.Timestamp - lastStepTime.Value).TotalMinutes >= chapter.TimeGapMinutes)
                {
                    sb.AppendLine($"### {step.Timestamp:HH:mm}〜");
                    sb.AppendLine();
                }
                lastStepTime = step.Timestamp;

                globalStep++;
                progress?.Report(new ExportProgress("Markdown 手順書を生成中...", globalStep, totalSteps, outputPath));
                string desc = step.DescriptionLlm ?? step.DescriptionRuleBased;
                string reviewMark = step.NeedsReview ? " <!-- TODO: UI名を確認してください -->" : "";
                sb.AppendLine($"{globalStep}. {desc}{reviewMark}");

                string? beforeRel = CopyImage(step.BeforeImagePath, imagesDir, imagesDirName, imageCache);
                string? afterRel  = CopyImage(step.AfterImagePath,  imagesDir, imagesDirName, imageCache);

                if (beforeRel != null)
                {
                    sb.AppendLine();
                    sb.AppendLine($"   **操作前**");
                    sb.AppendLine();
                    sb.AppendLine($"   ![操作前 {globalStep}]({beforeRel})");
                }
                if (afterRel != null)
                {
                    sb.AppendLine();
                    if (beforeRel != null) sb.AppendLine($"   **操作後**");
                    sb.AppendLine();
                    sb.AppendLine($"   ![ステップ {globalStep}]({afterRel})");
                }
                sb.AppendLine();
            }
        }

        string generated = sb.ToString();

        // O-07: ユーザーテンプレートが指定されていれば適用
        string finalContent = ApplyMarkdownTemplate(templatePath, generated);

        await File.WriteAllTextAsync(outputPath, finalContent, Encoding.UTF8);
        Log.Information("手順書 Markdown 出力完了: {Path}", outputPath);
    }

    // ── 画像コピー ───────────────────────────────────────────────────────────────
    // 元画像を imagesDir にコピーし、MD から見た相対パス文字列を返す。
    // 横幅が MaxImageWidth を超える場合はアスペクト比を維持してリサイズする。
    // ファイル名衝突時は連番サフィックスを付与。
    // 元ファイルが存在しない場合は null を返す。
    private const int MaxImageWidth = 1200;

    private static string? CopyImage(string? srcPath, string imagesDir, string imagesDirName,
        Dictionary<string, string> cache)
    {
        if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath))
            return null;

        // 同一ソースは既にコピー済みの相対パスを再利用（重複コピー防止）
        string srcFull = Path.GetFullPath(srcPath);
        if (cache.TryGetValue(srcFull, out string? cached))
            return cached;

        Directory.CreateDirectory(imagesDir);

        string ext      = Path.GetExtension(srcPath);
        string baseName = Path.GetFileNameWithoutExtension(srcPath);
        string destName = Path.GetFileName(srcPath);
        string destPath = Path.Combine(imagesDir, destName);

        // 別ソースが同名で既に存在する場合は連番サフィックスで回避
        int suffix = 1;
        while (File.Exists(destPath))
        {
            destName = $"{baseName}_{suffix++}{ext}";
            destPath = Path.Combine(imagesDir, destName);
        }

        WriteImage(srcPath, destPath);

        string relPath = $"{imagesDirName}/{destName}";
        cache[srcFull] = relPath;
        return relPath;
    }

    // 横幅が MaxImageWidth を超える場合はリサイズして保存、そうでなければそのままコピー。
    private static void WriteImage(string srcPath, string destPath)
    {
        try
        {
            using var src = System.Drawing.Image.FromFile(srcPath);
            if (src.Width <= MaxImageWidth)
            {
                // リサイズ不要 — バイト列をそのままコピー
                File.Copy(srcPath, destPath);
                return;
            }

            int newWidth  = MaxImageWidth;
            int newHeight = (int)Math.Round(src.Height * (double)MaxImageWidth / src.Width);
            if (newHeight < 1) newHeight = 1;

            using var resized = new System.Drawing.Bitmap(newWidth, newHeight);
            using var g = System.Drawing.Graphics.FromImage(resized);
            g.InterpolationMode  = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.SmoothingMode      = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.DrawImage(src, 0, 0, newWidth, newHeight);

            // 元の拡張子に合わせたフォーマットで保存
            var fmt = Path.GetExtension(destPath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                ".webp"           => System.Drawing.Imaging.ImageFormat.Png,  // WebP は GDI+ 非対応のため PNG で代替
                _                 => System.Drawing.Imaging.ImageFormat.Png,
            };
            resized.Save(destPath, fmt);

            Log.Debug("Markdown 用画像リサイズ: {W}px → {NW}px ({Src})",
                src.Width, newWidth, Path.GetFileName(srcPath));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Markdown 用画像リサイズに失敗 — そのままコピーします: {Src}", srcPath);
            File.Copy(srcPath, destPath, overwrite: true);
        }
    }


    // O-07: テンプレート適用。{{content}} プレースホルダーがあれば置換、なければ先頭に挿入。
    private static string ApplyMarkdownTemplate(string templatePath, string generated)
    {
        if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            return generated;

        try
        {
            string template = File.ReadAllText(templatePath, Encoding.UTF8);
            const string placeholder = "{{content}}";
            return template.Contains(placeholder, StringComparison.OrdinalIgnoreCase)
                ? template.Replace(placeholder, generated, StringComparison.OrdinalIgnoreCase)
                : template + Environment.NewLine + generated;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Markdown テンプレート読み込み失敗: {Path}", templatePath);
            return generated;
        }
    }

    private static List<ChapterGroup> BuildChapters(List<ManualStep> steps, int timeGapMinutes)
    {
        var result = new List<ChapterGroup>();
        ChapterGroup? current = null;

        foreach (var step in steps)
        {
            if (current == null || step.TriggerType == TriggerType.ActiveWindowChange)
            {
                current = new ChapterGroup
                {
                    WindowTitle   = step.WindowTitle,
                    ProcessName   = step.ProcessName,
                    TimeGapMinutes = timeGapMinutes
                };
                result.Add(current);
            }
            current.Steps.Add(step);
        }
        return result;
    }

    private static string ToAnchor(string heading)
    {
        // GitHub Markdown アンカー生成（簡易）
        var sb = new StringBuilder();
        foreach (char c in heading.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '-') sb.Append(c);
            else if (c == ' ') sb.Append('-');
        }
        return sb.ToString();
    }

    private class ChapterGroup
    {
        public string WindowTitle  { get; set; } = "";
        public string ProcessName  { get; set; } = "";
        public int TimeGapMinutes  { get; set; } = 5;
        public List<ManualStep> Steps { get; } = [];

        // D-02: タイトルが空の場合はプロセス名でフォールバック
        public string DisplayTitle => string.IsNullOrWhiteSpace(WindowTitle)
            ? (string.IsNullOrWhiteSpace(ProcessName) ? "(不明なウィンドウ)" : $"({ProcessName})")
            : WindowTitle;
    }
}
