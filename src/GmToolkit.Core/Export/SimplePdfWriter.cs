using System.Globalization;
using System.Text;

namespace GmToolkit.Core.Export;

/// <summary>
/// Renders a flat list of <see cref="PdfBlock"/>s to a minimal, valid, multi-page PDF (issue #132's
/// character-sheet/campaign-summary PDF export) -- hand-written PDF byte structure, not a
/// third-party PDF library.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not built on a NuGet PDF package</b> (QuestPDF, iText, SelectPdf, ...). Every
/// realistic option is either a native/Skia-backed dependency (a real concern for the Raspberry Pi
/// 4 ARM64 target this app already treats as load-bearing, not a stretch goal -- see CONTRIBUTING.md)
/// or carries licensing terms (iText's AGPL/commercial split) that don't fit a small MIT-licensed
/// hobby project without more thought than one issue's scope warrants. What #132 actually needs --
/// a title, a few headed sections, and wrapped paragraphs of plain text, laid out across pages with
/// no images, tables, or embedded fonts -- is a small enough slice of the PDF 1.4 spec (one
/// <c>/Catalog</c>, one flat <c>/Pages</c> tree, the built-in Standard-14 Helvetica/Helvetica-Bold
/// fonts that every PDF reader already ships and therefore need no embedding, and a handful of
/// <c>BT</c>/<c>Tf</c>/<c>Td</c>/<c>Tj</c>/<c>ET</c> content-stream operators per line) that writing
/// it by hand, with zero new dependencies anywhere in the dependency graph, is the better trade for
/// this app -- see this class's own tests (which shell out to <c>pdfinfo</c>/<c>pdftotext</c> where
/// available to confirm a real PDF reader agrees the output is valid) rather than trusting a
/// hand-rolled writer's own idea of correctness.
/// </para>
/// <para>
/// <b>Word wrap is a character-count heuristic, not real glyph-width metrics.</b> <see cref="AverageCharWidthFactor"/>
/// approximates Helvetica's average advance width as a fraction of the font size (the same trick
/// typewriter-style layout estimators have always used) rather than loading the Adobe Font Metrics
/// tables for Helvetica/Helvetica-Bold -- good enough for a plain-text character sheet or campaign
/// summary to read cleanly without overflowing the page margin, not typeset-quality justification.
/// </para>
/// <para>
/// <b>Text outside Latin-1 (0-255) is replaced with <c>?</c>.</b> The Standard-14 fonts this writer
/// references are always encoded <c>/WinAnsiEncoding</c> (effectively Latin-1) with no embedded
/// font program of their own -- correctly supporting arbitrary Unicode would mean embedding a real
/// font file, which reintroduces exactly the dependency/size concern this class's own remarks above
/// are trying to avoid. Every domain string this writer is ever handed (character/NPC names, notes,
/// stat values -- see <see cref="PlayerCharacterPdfExporter"/>/<see cref="CampaignSummaryPdfExporter"/>)
/// is free text a GM typed, which in practice is overwhelmingly Latin-1-safe already.
/// </para>
/// </remarks>
public static class SimplePdfWriter
{
    private const double PageWidth = 612; // US Letter, points (72pt/inch).
    private const double PageHeight = 792;
    private const double MarginLeft = 54;
    private const double MarginRight = 54;
    private const double MarginTop = 54;
    private const double MarginBottom = 54;
    private const double ContentWidth = PageWidth - MarginLeft - MarginRight;

    /// <summary>See this class's remarks on why word wrap is a heuristic, not real glyph metrics.
    /// Helvetica-Bold is very slightly wider on average than Helvetica; both constants are close
    /// enough to Adobe's published Helvetica AFM average that neither over- nor under-wraps
    /// noticeably at this class's body/heading font sizes.</summary>
    private const double AverageCharWidthFactor = 0.5;

    private const double BoldAverageCharWidthFactor = 0.56;

    private static readonly IReadOnlyDictionary<PdfBlockStyle, StyleMetrics> Styles = new Dictionary<PdfBlockStyle, StyleMetrics>
    {
        [PdfBlockStyle.Title] = new StyleMetrics(FontSize: 20, Bold: true, SpaceBefore: 0, SpaceAfter: 14),
        [PdfBlockStyle.Heading] = new StyleMetrics(FontSize: 14, Bold: true, SpaceBefore: 12, SpaceAfter: 6),
        [PdfBlockStyle.SubHeading] = new StyleMetrics(FontSize: 12, Bold: true, SpaceBefore: 8, SpaceAfter: 4),
        [PdfBlockStyle.Body] = new StyleMetrics(FontSize: 10, Bold: false, SpaceBefore: 0, SpaceAfter: 4),
    };

    /// <summary>
    /// Renders <paramref name="blocks"/> (in order) to a complete PDF file's bytes, with
    /// <paramref name="documentTitle"/> set as the PDF's own <c>/Title</c> metadata (shown in a
    /// reader's tab/window title). Always produces at least one page, even for an empty
    /// <paramref name="blocks"/> list.
    /// </summary>
    public static byte[] Write(string documentTitle, IReadOnlyList<PdfBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var pages = LayoutPages(blocks);
        return Render(documentTitle ?? string.Empty, pages);
    }

    private static List<List<PositionedLine>> LayoutPages(IReadOnlyList<PdfBlock> blocks)
    {
        var pages = new List<List<PositionedLine>>();
        var currentPage = new List<PositionedLine>();
        var y = PageHeight - MarginTop;

        void StartNewPage()
        {
            pages.Add(currentPage);
            currentPage = [];
            y = PageHeight - MarginTop;
        }

        foreach (var block in blocks)
        {
            var style = Styles[block.Style];
            var lineHeight = style.FontSize * 1.3;

            y -= style.SpaceBefore;

            foreach (var line in WrapText(block.Text ?? string.Empty, style.FontSize, style.Bold))
            {
                if (y - lineHeight < MarginBottom)
                {
                    StartNewPage();
                }

                currentPage.Add(new PositionedLine(line, style.FontSize, style.Bold, y));
                y -= lineHeight;
            }

            y -= style.SpaceAfter;
        }

        pages.Add(currentPage);
        return pages;
    }

    /// <summary>Splits <paramref name="text"/> on its own line breaks first (a blank source line
    /// stays a blank rendered line), then greedily word-wraps each of those lines to fit
    /// <see cref="ContentWidth"/> at <paramref name="fontSize"/>/<paramref name="bold"/> per this
    /// class's character-count heuristic (see this class's remarks).</summary>
    private static IReadOnlyList<string> WrapText(string text, double fontSize, bool bold)
    {
        var averageCharWidth = fontSize * (bold ? BoldAverageCharWidthFactor : AverageCharWidthFactor);
        var maxChars = Math.Max(1, (int)(ContentWidth / averageCharWidth));

        var result = new List<string>();
        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (rawLine.Length == 0)
            {
                result.Add(string.Empty);
                continue;
            }

            var current = new StringBuilder();
            foreach (var word in rawLine.Split(' '))
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                    continue;
                }

                if (current.Length + 1 + word.Length <= maxChars)
                {
                    current.Append(' ').Append(word);
                    continue;
                }

                result.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }

            result.Add(current.ToString());
        }

        return result;
    }

    private static byte[] Render(string documentTitle, List<List<PositionedLine>> pages)
    {
        using var stream = new MemoryStream();
        var offsets = new List<long> { 0 }; // Index 0 is the free-list head; never a real object.

        void WriteRaw(string text)
        {
            var bytes = Encoding.Latin1.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        void BeginObject(int id)
        {
            while (offsets.Count <= id)
            {
                offsets.Add(0);
            }

            offsets[id] = stream.Position;
            WriteRaw($"{id} 0 obj\n");
        }

        const int catalogId = 1;
        const int pagesId = 2;
        const int fontRegularId = 3;
        const int fontBoldId = 4;
        const int infoId = 5;
        const int firstPageId = 6;

        var pageCount = pages.Count;
        var totalObjects = firstPageId + (pageCount * 2) - 1;

        // Leading comment with high-bit bytes is the conventional PDF-1.4 hint to tools that treat
        // this as a binary (not text) file -- has no effect on parsing, purely a courtesy.
        WriteRaw("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");

        BeginObject(catalogId);
        WriteRaw($"<< /Type /Catalog /Pages {pagesId} 0 R >>\nendobj\n");

        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{firstPageId + (i * 2)} 0 R"));
        BeginObject(pagesId);
        WriteRaw($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>\nendobj\n");

        BeginObject(fontRegularId);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(fontBoldId);
        WriteRaw("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>\nendobj\n");

        BeginObject(infoId);
        WriteRaw($"<< /Title ({EscapeContentString(documentTitle)}) /Producer (GM Toolkit) >>\nendobj\n");

        for (var i = 0; i < pageCount; i++)
        {
            var pageObjectId = firstPageId + (i * 2);
            var contentObjectId = pageObjectId + 1;

            BeginObject(pageObjectId);
            WriteRaw(
                $"<< /Type /Page /Parent {pagesId} 0 R /MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                $"/Resources << /Font << /F1 {fontRegularId} 0 R /F2 {fontBoldId} 0 R >> >> " +
                $"/Contents {contentObjectId} 0 R >>\nendobj\n");

            var content = BuildContentStream(pages[i]);
            var contentBytes = Encoding.Latin1.GetBytes(content);

            BeginObject(contentObjectId);
            WriteRaw($"<< /Length {contentBytes.Length} >>\nstream\n");
            stream.Write(contentBytes, 0, contentBytes.Length);
            WriteRaw("\nendstream\nendobj\n");
        }

        var xrefOffset = stream.Position;
        WriteRaw($"xref\n0 {totalObjects + 1}\n");
        WriteRaw("0000000000 65535 f \n");
        for (var id = 1; id <= totalObjects; id++)
        {
            WriteRaw($"{offsets[id]:D10} 00000 n \n");
        }

        WriteRaw(
            $"trailer\n<< /Size {totalObjects + 1} /Root {catalogId} 0 R /Info {infoId} 0 R >>\n" +
            $"startxref\n{xrefOffset}\n%%EOF");

        return stream.ToArray();
    }

    private static string BuildContentStream(List<PositionedLine> lines)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.Text.Length == 0)
            {
                continue;
            }

            var font = line.Bold ? "F2" : "F1";
            builder.Append("BT\n");
            builder.Append('/').Append(font).Append(' ').Append(Num(line.FontSize)).Append(" Tf\n");
            builder.Append(Num(MarginLeft)).Append(' ').Append(Num(line.Y)).Append(" Td\n");
            builder.Append('(').Append(EscapeContentString(line.Text)).Append(") Tj\n");
            builder.Append("ET\n");
        }

        return builder.ToString();
    }

    /// <summary>PDF literal-string escaping: backslash and both parentheses must be backslash-
    /// escaped (unbalanced/unescaped parentheses would otherwise desynchronize the reader's own
    /// string-literal parsing), control characters are flattened to a space (every string this
    /// writer emits is a single content-stream line already -- see <see cref="WrapText"/> -- so a
    /// literal newline inside one would only ever be stray input, not intentional layout), and any
    /// codepoint outside Latin-1 becomes <c>?</c> per this class's remarks on
    /// <c>/WinAnsiEncoding</c>.</summary>
    private static string EscapeContentString(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            var c = ch > 255 ? '?' : ch;
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '(':
                    builder.Append("\\(");
                    break;
                case ')':
                    builder.Append("\\)");
                    break;
                default:
                    builder.Append(c < 0x20 ? ' ' : c);
                    break;
            }
        }

        return builder.ToString();
    }

    private static string Num(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private sealed record StyleMetrics(double FontSize, bool Bold, double SpaceBefore, double SpaceAfter);

    private readonly record struct PositionedLine(string Text, double FontSize, bool Bold, double Y);
}