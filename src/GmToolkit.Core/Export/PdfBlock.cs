namespace GmToolkit.Core.Export;

/// <summary>One paragraph of text in a document handed to <see cref="SimplePdfWriter"/> -- see that
/// type's remarks for the overall design. <see cref="Text"/> may contain <c>\n</c>, in which case
/// each line wraps and paginates independently (a blank line in the source renders as a blank
/// line in the PDF, not collapsed).</summary>
public sealed record PdfBlock(string Text, PdfBlockStyle Style = PdfBlockStyle.Body);