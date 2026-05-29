using AutoScreenshot.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Serilog;
using WColor = DocumentFormat.OpenXml.Wordprocessing.Color;
using A   = DocumentFormat.OpenXml.Drawing;
using DW  = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace AutoScreenshot.Services;

/// <summary>ManualSession を Word (.docx) ファイルに書き出す</summary>
public class DocxManualWriter
{
    public async Task WriteAsync(ManualSession session, string outputPath,
        int chapterTimeGapMinutes = 5, string templateDotxPath = "")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await Task.Run(() => new Generator(session, outputPath, chapterTimeGapMinutes, templateDotxPath).Run());
        Log.Information("手順書 docx 出力完了: {Path}", outputPath);
    }

    // ── 内部実装クラス ──────────────────────────────────────────────────────────
    private sealed class Generator
    {
        private readonly ManualSession _session;
        private readonly string _outputPath;
        private readonly int _timeGapMin;
        private readonly string _templateDotxPath;
        private uint _drawId;

        public Generator(ManualSession session, string outputPath, int timeGapMin, string templateDotxPath = "")
        {
            _session         = session;
            _outputPath      = outputPath;
            _timeGapMin      = timeGapMin;
            _templateDotxPath = templateDotxPath;
        }

        public void Run()
        {
            bool useTemplate = !string.IsNullOrWhiteSpace(_templateDotxPath)
                               && File.Exists(_templateDotxPath);

            WordprocessingDocument doc;
            MainDocumentPart mainPart;
            Body body;

            if (useTemplate)
            {
                // O-07: .dotx をコピーして .docx に変換し、ボディを空にして再利用
                try
                {
                    File.Copy(_templateDotxPath, _outputPath, overwrite: true);
                    doc = WordprocessingDocument.Open(_outputPath, true);
                    doc.ChangeDocumentType(WordprocessingDocumentType.Document);
                    mainPart = doc.MainDocumentPart!;
                    mainPart.Document ??= new Document();
                    body = mainPart.Document.Body ?? mainPart.Document.AppendChild(new Body());
                    // ボディを空にする（SectionProperties は保持）
                    var sectPr = body.Elements<SectionProperties>().FirstOrDefault();
                    body.RemoveAllChildren();
                    if (sectPr != null) body.Append(sectPr);
                    // テンプレート固有のスタイルを保持するため DefineStyles は呼ばない
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "dotx テンプレート読み込み失敗: {Path}. 通常作成に切り替えます。", _templateDotxPath);
                    doc = WordprocessingDocument.Create(_outputPath, WordprocessingDocumentType.Document);
                    mainPart = doc.AddMainDocumentPart();
                    mainPart.Document = new Document();
                    body = mainPart.Document.AppendChild(new Body());
                    DefineStyles(mainPart);
                }
            }
            else
            {
                doc      = WordprocessingDocument.Create(_outputPath, WordprocessingDocumentType.Document);
                mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                body     = mainPart.Document.AppendChild(new Body());
                DefineStyles(mainPart);
            }

            using (doc)
            {
                var (hId, fId) = AddHeaderFooter(mainPart);

                // 表紙 → 改ページ → 目次 → 改ページ → 本文
                AppendCover(body);
                body.Append(PageBreak());
                AppendToc(body);
                body.Append(PageBreak());
                AppendContent(body, mainPart);

                // セクションプロパティ（A4、ヘッダー/フッター参照）
                body.Append(new SectionProperties(
                    new HeaderReference { Type = HeaderFooterValues.Default, Id = hId },
                    new FooterReference { Type = HeaderFooterValues.Default, Id = fId },
                    new PageSize   { Width = 11906, Height = 16838 },
                    new PageMargin { Top = 1134, Bottom = 1134, Left = 1701, Right = 1701 }));

                mainPart.Document.Save();
            }
        }

        // ── スタイル定義 ────────────────────────────────────────────────────────

        private static void DefineStyles(MainDocumentPart mainPart)
        {
            var sp = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles(
                MkStyle("Normal",   "Normal",    21, bold: false, color: null,     outline: null),
                MkStyle("Title",    "Title",     48, bold: true,  color: "1F3864", outline: null, centered: true),
                MkStyle("Heading1", "Heading 1", 32, bold: true,  color: "2E74B5", outline: 0),
                MkStyle("Heading2", "Heading 2", 26, bold: true,  color: "2E74B5", outline: 1),
                MkStyle("Heading3", "Heading 3", 24, bold: true,  color: "404040", outline: 2));
            sp.Styles = styles;
            styles.Save();
        }

        private static Style MkStyle(string id, string name, int halfPt,
            bool bold, string? color, int? outline, bool centered = false)
        {
            var style = new Style { Type = StyleValues.Paragraph, StyleId = id };
            style.Append(new StyleName { Val = name });
            if (id != "Normal") style.Append(new BasedOn { Val = "Normal" });

            // 段落プロパティ（見出しレベル・センタリング）
            var ppr = new StyleParagraphProperties();
            if (outline.HasValue) ppr.Append(new OutlineLevel  { Val = outline.Value });
            if (centered)         ppr.Append(new Justification { Val = JustificationValues.Center });
            ppr.Append(new SpacingBetweenLines { Before = "120", After = "120" });
            style.Append(ppr);

            // 文字プロパティ
            var rpr = new StyleRunProperties();
            rpr.Append(new FontSize              { Val = halfPt.ToString() });
            rpr.Append(new FontSizeComplexScript { Val = halfPt.ToString() });
            if (bold)         rpr.Append(new Bold());
            if (color != null) rpr.Append(new WColor { Val = color });
            style.Append(rpr);

            return style;
        }

        // ── ヘッダー / フッター ────────────────────────────────────────────────

        private (string hId, string fId) AddHeaderFooter(MainDocumentPart mainPart)
        {
            // ヘッダー：右寄せでタイトル
            var hp = mainPart.AddNewPart<HeaderPart>();
            var h  = new Header(
                new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Right }),
                    new Run(
                        new RunProperties(new FontSize { Val = "18" }, new WColor { Val = "808080" }),
                        new Text(_session.Title))));
            hp.Header = h;
            h.Save();

            // フッター：中央にページ番号フィールド
            var fp   = mainPart.AddNewPart<FooterPart>();
            var fpPa = new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
            fpPa.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }));
            fpPa.Append(new Run(new FieldCode(" PAGE \\* MERGEFORMAT ")
                { Space = SpaceProcessingModeValues.Preserve }));
            fpPa.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }));
            fpPa.Append(new Run(new Text("1")));
            fpPa.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
            var f = new Footer(fpPa);
            fp.Footer = f;
            f.Save();

            return (mainPart.GetIdOfPart(hp), mainPart.GetIdOfPart(fp));
        }

        // ── 表紙 ─────────────────────────────────────────────────────────────

        private void AppendCover(Body body)
        {
            body.Append(Para("", "Normal"));
            body.Append(Para("", "Normal"));
            body.Append(Para(_session.Title, "Title"));
            body.Append(Para("", "Normal"));
            body.Append(Para($"開始日時: {_session.StartedAt:yyyy-MM-dd HH:mm:ss}", "Normal"));
            if (_session.EndedAt.HasValue)
                body.Append(Para($"終了日時: {_session.EndedAt:yyyy-MM-dd HH:mm:ss}", "Normal"));
            body.Append(Para($"OS / ユーザー: {_session.OsInfo}", "Normal"));
            body.Append(Para("作成ツール: AutoScreenshot v1.0", "Normal"));

            // ダイジェスト (D-01, L-04)
            if (!string.IsNullOrWhiteSpace(_session.Digest))
            {
                body.Append(Para("", "Normal"));
                body.Append(Para("操作内容サマリー", "Heading2"));
                foreach (var line in _session.Digest.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    body.Append(Para(line.Trim(), "Normal"));
            }
        }

        // ── 目次（Word がファイルを開いた時に更新）──────────────────────────

        private static void AppendToc(Body body)
        {
            body.Append(Para("目次", "Heading1"));

            var tp = new Paragraph();
            tp.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }));
            tp.Append(new Run(
                new FieldCode(@" TOC \o ""1-3"" \h \z \u ")
                { Space = SpaceProcessingModeValues.Preserve }));
            tp.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }));
            tp.Append(new Run(new Text("（Word でファイルを開いてフィールドを更新すると目次が表示されます）")));
            tp.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
            body.Append(tp);
        }

        // ── 本文（チャプター・ステップ・画像）──────────────────────────────

        private void AppendContent(Body body, MainDocumentPart mainPart)
        {
            var chapters  = BuildChapters(_session.Steps);
            int stepCount = 0;

            for (int ci = 0; ci < chapters.Count; ci++)
            {
                var (title, steps) = chapters[ci];
                body.Append(Para($"{ci + 1}. {title}", "Heading1"));

                DateTime? lastTs = null;
                foreach (var step in steps)
                {
                    if (lastTs.HasValue &&
                        (step.Timestamp - lastTs.Value).TotalMinutes >= _timeGapMin)
                        body.Append(Para($"{step.Timestamp:HH:mm}〜", "Heading3"));
                    lastTs = step.Timestamp;

                    stepCount++;
                    string desc   = step.DescriptionLlm ?? step.DescriptionRuleBased;
                    string review = step.NeedsReview ? "　※要確認" : "";
                    body.Append(Para($"{stepCount}. {desc}{review}", "Normal"));

                    bool hasBefore = !string.IsNullOrEmpty(step.BeforeImagePath) && File.Exists(step.BeforeImagePath);
                    bool hasAfter  = !string.IsNullOrEmpty(step.AfterImagePath)  && File.Exists(step.AfterImagePath);
                    if (hasBefore)
                    {
                        body.Append(Para("操作前", "Heading3"));
                        TryAppendImage(body, mainPart, step.BeforeImagePath!, stepCount);
                    }
                    if (hasAfter)
                    {
                        if (hasBefore) body.Append(Para("操作後", "Heading3"));
                        TryAppendImage(body, mainPart, step.AfterImagePath!, stepCount);
                    }
                }
            }
        }

        // ── 画像埋め込み ───────────────────────────────────────────────────

        private void TryAppendImage(Body body, MainDocumentPart mainPart, string path, int stepNum)
        {
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".webp") return; // WebP は Open XML 非対応のためスキップ

                var partType = ext is ".jpg" or ".jpeg" ? ImagePartType.Jpeg : ImagePartType.Png;
                var imgPart  = mainPart.AddImagePart(partType);
                using (var fs = File.OpenRead(path))
                    imgPart.FeedData(fs);
                string relId = mainPart.GetIdOfPart(imgPart);

                // 画像サイズ → EMU (1inch = 914400 EMU)
                long wEmu, hEmu;
                using (var img = System.Drawing.Image.FromFile(path))
                {
                    const long maxEmu = 5_400_000L; // 15cm（A4 コンテンツ幅）
                    long rawW = (long)(img.Width  * 914400L / img.HorizontalResolution);
                    long rawH = (long)(img.Height * 914400L / img.VerticalResolution);
                    double scale = rawW > maxEmu ? (double)maxEmu / rawW : 1.0;
                    wEmu = (long)(rawW * scale);
                    hEmu = (long)(rawH * scale);
                }

                uint id  = ++_drawId;
                string nm = $"img{id}_step{stepNum}";

                var drawing = new Drawing(
                    new DW.Inline(
                        new DW.Extent        { Cx = wEmu, Cy = hEmu },
                        new DW.EffectExtent  { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                        new DW.DocProperties { Id = id, Name = nm },
                        new DW.NonVisualGraphicFrameDrawingProperties(
                            new A.GraphicFrameLocks { NoChangeAspect = true }),
                        new A.Graphic(
                            new A.GraphicData(
                                new PIC.Picture(
                                    new PIC.NonVisualPictureProperties(
                                        new PIC.NonVisualDrawingProperties { Id = 0U, Name = nm },
                                        new PIC.NonVisualPictureDrawingProperties()),
                                    new PIC.BlipFill(
                                        new A.Blip { Embed = relId },
                                        new A.Stretch(new A.FillRectangle())),
                                    new PIC.ShapeProperties(
                                        new A.Transform2D(
                                            new A.Offset  { X = 0L, Y = 0L },
                                            new A.Extents { Cx = wEmu, Cy = hEmu }),
                                        new A.PresetGeometry(new A.AdjustValueList())
                                            { Preset = A.ShapeTypeValues.Rectangle })))
                            { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

                body.Append(new Paragraph(
                    new ParagraphProperties(new Justification { Val = JustificationValues.Left }),
                    new Run(drawing)));
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "docx 画像埋め込み失敗: {Path}", path);
            }
        }

        // ── ヘルパー ─────────────────────────────────────────────────────────

        private static Paragraph Para(string text, string styleId)
        {
            var p = new Paragraph(
                new ParagraphProperties(new ParagraphStyleId { Val = styleId }));
            if (!string.IsNullOrEmpty(text))
                p.Append(new Run(
                    new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
            return p;
        }

        private static Paragraph PageBreak() =>
            new(new Run(new Break { Type = BreakValues.Page }));

        private static List<(string Title, List<ManualStep> Steps)> BuildChapters(List<ManualStep> steps)
        {
            var result = new List<(string, List<ManualStep>)>();
            List<ManualStep>? cur = null;
            string curTitle = "";

            foreach (var step in steps)
            {
                if (cur == null || step.TriggerType == TriggerType.ActiveWindowChange)
                {
                    curTitle = step.WindowTitle;
                    cur      = [];
                    result.Add((curTitle, cur));
                }
                cur.Add(step);
            }
            return result;
        }
    }
}
