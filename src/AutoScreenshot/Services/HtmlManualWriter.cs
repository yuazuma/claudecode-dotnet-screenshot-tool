using System.Text;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// プロジェクトの非削除ステップから自己完結型 HTML 手順書を生成する（FR-A）。
/// スクリーンショットは Base64 インラインで埋め込むため外部ファイル依存なし。
/// </summary>
public class HtmlManualWriter
{
    public async Task WriteAsync(ProjectInfo project, string outputPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var steps = project.Steps.Where(s => !s.IsDeleted).ToList();
        var sb = new StringBuilder(512 * 1024);

        string title = System.Net.WebUtility.HtmlEncode(project.Title);
        string createdAt = project.CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
        string digest = project.Digest != null
            ? $"<div class=\"digest\"><p>{System.Net.WebUtility.HtmlEncode(project.Digest)}</p></div>"
            : "";

        sb.AppendLine($"""
            <!DOCTYPE html>
            <html lang="ja">
            <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{title}</title>
            <style>
            {BuildCss()}
            </style>
            </head>
            <body>
            <header>
              <h1>{title}</h1>
              <p class="meta">{createdAt} — {steps.Count} ステップ</p>
            </header>
            {digest}
            """);

        // 目次
        sb.AppendLine("<nav id=\"toc\"><h2>目次</h2><ol>");
        foreach (var step in steps)
        {
            string preview = System.Net.WebUtility.HtmlEncode(
                Truncate(step.EffectiveDescription, 40));
            sb.AppendLine($"  <li><a href=\"#step-{step.StepNumber}\">ステップ {step.StepNumber}: {preview}</a></li>");
        }
        sb.AppendLine("</ol></nav>");
        sb.AppendLine("<main>");

        // ステップカード
        foreach (var step in steps)
        {
            string desc = System.Net.WebUtility.HtmlEncode(step.EffectiveDescription);
            string trigger = System.Net.WebUtility.HtmlEncode(step.TriggerType);
            string window = System.Net.WebUtility.HtmlEncode(step.WindowTitle);
            string ts = step.Timestamp.LocalDateTime.ToString("HH:mm:ss");

            string imagesSection = BuildImagesSection(project.ProjectFolder, step);

            sb.AppendLine($"""
                <section id="step-{step.StepNumber}" class="step">
                  <h2>ステップ {step.StepNumber}</h2>
                  <p class="step-meta">{trigger} &mdash; {ts} &mdash; {window}</p>
                  {imagesSection}
                  <p class="step-desc">{desc}</p>
                </section>
                """);
        }

        sb.AppendLine("</main>");
        sb.AppendLine($"<footer><p>AutoScreenshot で生成 — {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p></footer>");
        sb.AppendLine("</body></html>");

        await File.WriteAllTextAsync(outputPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Log.Information("HTML 手順書を生成: {Path}", outputPath);
    }

    private static string BuildImagesSection(string projectFolder, ProjectStep step)
    {
        string beforeTag = BuildBeforeImageTag(projectFolder, step);
        string afterTag  = BuildAfterImageTag(projectFolder, step);

        if (string.IsNullOrEmpty(beforeTag))
            return afterTag;

        return $"<div class=\"step-images\">" +
               $"<figure class=\"before\"><figcaption>操作前</figcaption>{beforeTag}</figure>" +
               $"<figure class=\"after\"><figcaption>操作後</figcaption>{afterTag}</figure>" +
               $"</div>";
    }

    private static string BuildBeforeImageTag(string projectFolder, ProjectStep step)
    {
        // before が未取得の場合は素の after 画像（アノテーション適用前）をフォールバックとして使用する
        string? relPath = step.BeforeImagePath ?? step.AfterImagePath;
        if (relPath == null) return string.Empty;

        string fullPath = Path.Combine(projectFolder, relPath.Replace('/', '\\'));
        if (!File.Exists(fullPath)) return string.Empty;
        try
        {
            string base64 = Convert.ToBase64String(File.ReadAllBytes(fullPath));
            string mime = relPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png" : GetMime(fullPath);
            return $"<img src=\"data:{mime};base64,{base64}\" alt=\"操作前\" loading=\"lazy\">";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HTML 用 before 画像読み込み失敗: {Path}", fullPath);
            return string.Empty;
        }
    }

    private static string BuildAfterImageTag(string projectFolder, ProjectStep step)
    {
        if (step.AfterImagePath == null) return "<div class=\"no-image\">(画像なし)</div>";

        string fullPath = Path.Combine(projectFolder, step.AfterImagePath.Replace('/', '\\'));
        if (!File.Exists(fullPath)) return "<div class=\"no-image\">(画像ファイルが見つかりません)</div>";

        try
        {
            byte[] imageBytes;
            string mime;

            if (step.Annotations?.Count > 0)
            {
                using var bmp = AnnotationRenderer.Render(fullPath, step.Annotations);
                if (bmp != null)
                {
                    using var ms = new System.IO.MemoryStream();
                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    imageBytes = ms.ToArray();
                    mime = "image/png";
                }
                else
                {
                    imageBytes = File.ReadAllBytes(fullPath);
                    mime = GetMime(fullPath);
                }
            }
            else
            {
                imageBytes = File.ReadAllBytes(fullPath);
                mime = GetMime(fullPath);
            }

            string base64 = Convert.ToBase64String(imageBytes);
            return $"<img src=\"data:{mime};base64,{base64}\" alt=\"操作後\" loading=\"lazy\">";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "HTML 用 after 画像読み込み失敗: {Path}", fullPath);
            return "<div class=\"no-image\">(画像の読み込みに失敗しました)</div>";
        }
    }

    private static string GetMime(string path) =>
        Path.GetExtension(path).TrimStart('.').ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "webp"          => "image/webp",
            _               => "image/png",
        };

    private static string BuildCss() => """
        :root {
          --accent: #0078d4;
          --border: #d0d7de;
          --bg-step: #ffffff;
          --bg-page: #f6f8fa;
          --text: #1f2328;
          --text-muted: #656d76;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
          background: var(--bg-page);
          color: var(--text);
          line-height: 1.6;
        }
        header {
          max-width: 900px; margin: 0 auto; padding: 32px 24px 16px;
        }
        header h1 { font-size: 1.8rem; margin-bottom: 4px; }
        .meta { color: var(--text-muted); font-size: 0.9rem; }
        .digest {
          max-width: 900px; margin: 0 auto 16px; padding: 0 24px;
          border-left: 4px solid var(--accent); color: var(--text-muted);
        }
        nav#toc {
          max-width: 900px; margin: 0 auto 24px; padding: 16px 24px;
          background: var(--bg-step); border: 1px solid var(--border);
          border-radius: 8px;
        }
        nav#toc h2 { font-size: 1rem; margin-bottom: 8px; color: var(--text-muted); }
        nav#toc ol { padding-left: 20px; }
        nav#toc li { margin: 2px 0; }
        nav#toc a { color: var(--accent); text-decoration: none; font-size: 0.9rem; }
        nav#toc a:hover { text-decoration: underline; }
        main { max-width: 900px; margin: 0 auto; padding: 0 24px 48px; }
        .step {
          background: var(--bg-step);
          border: 1px solid var(--border);
          border-radius: 8px;
          margin-bottom: 24px;
          padding: 20px 24px;
          box-shadow: 0 1px 3px rgba(0,0,0,.06);
        }
        .step h2 {
          font-size: 1.1rem; font-weight: 600;
          color: var(--accent); margin-bottom: 4px;
        }
        .step-meta { font-size: 0.8rem; color: var(--text-muted); margin-bottom: 12px; }
        .step img {
          width: 100%; max-width: 800px;
          border: 1px solid var(--border); border-radius: 4px;
          margin-bottom: 4px; display: block;
        }
        .step-images {
          display: flex; gap: 12px; margin-bottom: 12px; flex-wrap: wrap;
        }
        .step-images figure {
          flex: 1 1 45%; min-width: 200px; margin: 0;
        }
        .step-images figcaption {
          font-size: 0.78rem; color: var(--text-muted); margin-bottom: 4px;
          font-weight: 600; text-transform: uppercase; letter-spacing: .04em;
        }
        .step-images img { margin-bottom: 0; }
        figure.before img { opacity: 0.85; }
        .step-desc { font-size: 0.95rem; }
        .no-image {
          background: #f0f0f0; color: var(--text-muted);
          text-align: center; padding: 40px; border-radius: 4px;
          margin-bottom: 12px; font-size: 0.85rem;
        }
        footer {
          max-width: 900px; margin: 0 auto; padding: 16px 24px;
          text-align: right; font-size: 0.75rem; color: var(--text-muted);
        }
        @media print {
          body { background: white; }
          nav#toc { display: none; }
          .step { break-inside: avoid; box-shadow: none; border: 1px solid #ccc; }
          .step img { max-width: 100%; }
        }
        """;

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
