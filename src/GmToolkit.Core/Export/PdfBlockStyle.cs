namespace GmToolkit.Core.Export;

/// <summary>Visual weight for a <see cref="PdfBlock"/> -- drives the font size/weight
/// <see cref="SimplePdfWriter"/> renders it at. Deliberately just four flat levels (not a full CSS-
/// style stylesheet): every document <see cref="SimplePdfWriter"/> renders (a character sheet, a
/// campaign summary) is a flat list of title/heading/sub-heading/body text, never nested rich
/// formatting, so four levels is exactly what's needed and no more.</summary>
public enum PdfBlockStyle
{
    /// <summary>The document's own title -- used once, at the very top.</summary>
    Title,

    /// <summary>A major section break (e.g. "Player Characters", "Stats").</summary>
    Heading,

    /// <summary>A minor section break nested under a <see cref="Heading"/> (e.g. one NPC's name
    /// inside a campaign summary's "NPCs" section).</summary>
    SubHeading,

    /// <summary>Ordinary paragraph/label text.</summary>
    Body,
}