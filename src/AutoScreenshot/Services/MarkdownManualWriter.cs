using System.Text;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>ManualSession を Markdown ファイルに書き出す</summary>
public class MarkdownManualWriter
{
    public async Task WriteAsync(ManualSession session, string outputPath,
        int chapterTimeGapMinutes = 5, string templatePath = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        string outputDir = Path.GetDirectoryName(outputPath)!;

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
        for (int ci = 0; ci < chapters.Count; ci++)
        {
            var chapter = chapters[ci];
            sb.AppendLine($"## {ci + 1}. {chapter.DisplayTitle}");
            sb.AppendLine();


            DateTime? lastStepTime = null;
            foreach (var step in chapter.Steps)
            {
                // 時間ギャップで小見出し
                if (lastStepTime.HasValue &&
                    (step.Timestamp - lastStepTime.Value).TotalMinutes >= chapter.TimeGapMinutes)
                {
                    sb.AppendLine($"### {step.Timestamp:HH:mm}〜");
                    sb.AppendLine();
                }
                lastStepTime = step.Timestamp;

                globalStep++;
                string desc = step.DescriptionLlm ?? step.DescriptionRuleBased;
                string reviewMark = step.NeedsReview ? " <!-- TODO: UI名を確認してください -->" : "";
                sb.AppendLine($"{globalStep}. {desc}{reviewMark}");

                if (!string.IsNullOrEmpty(step.ImagePath) && File.Exists(step.ImagePath))
                {
                    string rel = Path.GetRelativePath(outputDir, step.ImagePath)
                                     .Replace('\\', '/');
                    sb.AppendLine();
                    sb.AppendLine($"   ![ステップ {globalStep}]({rel})");
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
