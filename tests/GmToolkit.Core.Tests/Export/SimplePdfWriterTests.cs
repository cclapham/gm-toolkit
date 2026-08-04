using System.Text;
using System.Text.RegularExpressions;

using GmToolkit.Core.Export;

namespace GmToolkit.Core.Tests.Export;

/// <remarks>
/// These tests inspect the raw PDF bytes directly (via <see cref="Encoding.Latin1"/>, matching
/// <see cref="SimplePdfWriter"/>'s own encoding -- see its remarks) rather than shelling out to a
/// real PDF reader (e.g. poppler's <c>pdfinfo</c>/<c>pdftotext</c>), so this suite stays hermetic on
/// any CI runner regardless of what's installed -- <c>pdfinfo</c>/<c>pdftotext</c> were used
/// manually while building this class to confirm a real reader agrees the output is valid (parses
/// cleanly, reports the right page count, extracts the expected text), but that's not something
/// this repeatable test suite can depend on being present.
/// </remarks>
public class SimplePdfWriterTests
{
    [Fact]
    public void Write_produces_bytes_starting_with_the_pdf_header_and_ending_with_eof()
    {
        var bytes = SimplePdfWriter.Write("Title", [new PdfBlock("Hello", PdfBlockStyle.Body)]);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.StartsWith("%PDF-1.4\n", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_embeds_the_document_title_in_the_info_dictionary()
    {
        var bytes = SimplePdfWriter.Write("My Campaign - Summary", []);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/Title (My Campaign - Summary)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_with_no_blocks_still_produces_exactly_one_page()
    {
        var bytes = SimplePdfWriter.Write("Empty", []);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("/Type /Pages /Kids [6 0 R] /Count 1", text, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(text, "/Type /Page[^s]"));
    }

    [Fact]
    public void Write_renders_body_text_readably_in_the_content_stream()
    {
        var bytes = SimplePdfWriter.Write("Title", [new PdfBlock("Brannigan the Bold", PdfBlockStyle.Body)]);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains("(Brannigan the Bold) Tj", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("(parens)", "\\(parens\\)")]
    public void Write_escapes_backslashes_and_parentheses_in_content_strings(string input, string expectedEscaped)
    {
        var bytes = SimplePdfWriter.Write("Title", [new PdfBlock(input, PdfBlockStyle.Body)]);
        var text = Encoding.Latin1.GetString(bytes);

        Assert.Contains($"({expectedEscaped}) Tj", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_wraps_long_paragraphs_across_multiple_lines()
    {
        var longText = string.Join(' ', Enumerable.Repeat("word", 200));
        var bytes = SimplePdfWriter.Write("Title", [new PdfBlock(longText, PdfBlockStyle.Body)]);
        var text = Encoding.Latin1.GetString(bytes);

        // 200 four-letter words joined with spaces can't possibly fit on one ~500pt-wide line at
        // 10pt body text -- if wrapping ran at all, there must be more than one "Tj" text-show
        // operator emitted for this single block.
        Assert.True(Regex.Matches(text, Regex.Escape(") Tj")).Count > 1);
    }

    [Fact]
    public void Write_paginates_once_content_exceeds_one_page()
    {
        var blocks = Enumerable.Range(0, 80)
            .Select(i => new PdfBlock($"Line {i}: some body text for this row.", PdfBlockStyle.Body))
            .ToList();

        var bytes = SimplePdfWriter.Write("Title", blocks);
        var text = Encoding.Latin1.GetString(bytes);

        var pageCount = Regex.Matches(text, "/Type /Page[^s]").Count;
        Assert.True(pageCount > 1, $"Expected more than one page, got {pageCount}.");

        var kidsMatch = Regex.Match(text, @"/Kids \[([^\]]+)\] /Count (\d+)");
        Assert.True(kidsMatch.Success);
        Assert.Equal(pageCount, int.Parse(kidsMatch.Groups[2].Value));
    }

    [Fact]
    public void Write_produces_an_xref_table_whose_offsets_actually_point_at_each_object()
    {
        var blocks = Enumerable.Range(0, 80)
            .Select(i => new PdfBlock($"Line {i}: some body text for this row.", PdfBlockStyle.Body))
            .ToList();

        var bytes = SimplePdfWriter.Write("Title", blocks);
        var text = Encoding.Latin1.GetString(bytes);

        var startXrefMatch = Regex.Match(text, @"startxref\n(\d+)\n%%EOF$");
        Assert.True(startXrefMatch.Success);
        var xrefOffset = int.Parse(startXrefMatch.Groups[1].Value);
        Assert.StartsWith("xref\n", text[xrefOffset..], StringComparison.Ordinal);

        // Every "n" (in-use) entry's 10-digit byte offset must point at that object's own
        // "<id> 0 obj" header -- proves BeginObject's offset bookkeeping is actually correct, not
        // just that the surrounding syntax happens to look right.
        foreach (Match entry in Regex.Matches(text, @"(\d{10}) 00000 n \n"))
        {
            var offset = int.Parse(entry.Groups[1].Value);
            Assert.Matches(@"^\d+ 0 obj\n", text[offset..(offset + 20)]);
        }
    }

    [Fact]
    public void Write_throws_for_null_blocks()
    {
        Assert.Throws<ArgumentNullException>(() => SimplePdfWriter.Write("Title", null!));
    }
}