using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Export;

/// <summary>
/// Builds a single-character PDF sheet (issue #132's "Per-character: Character → Export to PDF")
/// from a <see cref="PlayerCharacter"/> plus its owning <see cref="Campaign"/>, rendered via
/// <see cref="SimplePdfWriter"/>.
/// </summary>
/// <remarks>
/// <b>No hardcoded STR/DEX/CON/INT/WIS/CHA/HP/AC fields.</b> Same reasoning as
/// <see cref="CampaignCsvExporter"/>'s remarks: <see cref="PlayerCharacter.Stats"/> is a
/// system-agnostic bag, so this exporter never assumes any particular key exists. When
/// <paramref name="system"/> (the campaign's attached <see cref="CharacterSystem"/>, if any) is
/// supplied, stats render in that system's own <see cref="CharacterSystem.PcFields"/> order with
/// each field's human-readable <see cref="Systems.StatFieldDefinition.Label"/> -- a
/// <c>repeating-group</c> field (skills, spells, equipment, ...) expands to one line per row via
/// <see cref="RepeatingGroupCodec.Deserialize"/>, which is how issue #132's example template
/// ("Skills, Spells (if applicable), Equipment") is satisfied without this class knowing what
/// "Skills" or "Equipment" mean for any particular system. Without a resolved system (a freeform
/// campaign, or one whose system pack is no longer installed), stats fall back to every
/// <see cref="PlayerCharacter.Stats"/> entry sorted by key -- still readable, just unlabeled.
/// </remarks>
public static class PlayerCharacterPdfExporter
{
    public static byte[] Export(PlayerCharacter character, Campaign campaign, CharacterSystem? system = null)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(campaign);

        var blocks = new List<PdfBlock> { new(character.CharacterName, PdfBlockStyle.Title) };

        var summary = $"{campaign.Name}";
        if (!string.IsNullOrWhiteSpace(character.PlayerName))
        {
            summary += $"  |  Played by {character.PlayerName}";
        }

        blocks.Add(new PdfBlock(summary, PdfBlockStyle.Body));

        var classLine = $"Ancestry: {OrDash(character.Ancestry)}   Class: {OrDash(character.Class)}   Level: {character.Level}";
        blocks.Add(new PdfBlock(classLine, PdfBlockStyle.Body));

        blocks.Add(new PdfBlock("Stats", PdfBlockStyle.Heading));
        blocks.AddRange(StatBlockBuilder.Build(character.Stats, system?.PcFields));

        if (!string.IsNullOrWhiteSpace(character.Notes))
        {
            blocks.Add(new PdfBlock("Notes", PdfBlockStyle.Heading));
            blocks.Add(new PdfBlock(character.Notes, PdfBlockStyle.Body));
        }

        return SimplePdfWriter.Write($"{character.CharacterName} - Character Sheet", blocks);
    }

    private static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}