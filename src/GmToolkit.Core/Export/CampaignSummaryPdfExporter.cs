using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Export;

/// <summary>
/// Builds a whole-campaign summary PDF (issue #132's "Per-campaign: Campaign → Export Campaign
/// Summary to PDF") from a <see cref="Campaign"/> and its loaded <see cref="Campaign.PlayerCharacters"/>/
/// <see cref="Campaign.Npcs"/>, rendered via <see cref="SimplePdfWriter"/>.
/// </summary>
/// <remarks>
/// <b>No quest log.</b> Issue #132's own template line-item lists one, but this app has no quest/
/// session-log domain model at all yet -- that's M12's job (see ROADMAP.md's "Session diary &amp;
/// calendar" milestone), not something this exporter can invent data for. This summary covers
/// exactly what a <see cref="Campaign"/> actually has today: its own metadata, every player
/// character (compact roster line), and every NPC (a full stat block, since an NPC's whole point is
/// being a GM's own reference sheet -- unlike <see cref="PlayerCharacterPdfExporter"/>'s one-PC
/// sheet, there is no player-facing redaction concern here).
/// </remarks>
public static class CampaignSummaryPdfExporter
{
    public static byte[] Export(Campaign campaign, CharacterSystem? system = null)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        var blocks = new List<PdfBlock> { new(campaign.Name, PdfBlockStyle.Title) };

        if (!string.IsNullOrWhiteSpace(campaign.GameSystem))
        {
            blocks.Add(new PdfBlock($"Game system: {campaign.GameSystem}", PdfBlockStyle.Body));
        }

        if (!string.IsNullOrWhiteSpace(campaign.Description))
        {
            blocks.Add(new PdfBlock(campaign.Description, PdfBlockStyle.Body));
        }

        var playerCharacters = campaign.PlayerCharacters
            .OrderBy(pc => pc.CharacterName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        blocks.Add(new PdfBlock($"Player Characters ({playerCharacters.Count})", PdfBlockStyle.Heading));
        foreach (var pc in playerCharacters)
        {
            blocks.Add(new PdfBlock(pc.CharacterName, PdfBlockStyle.SubHeading));
            var summary = $"Player: {OrDash(pc.PlayerName)}   Ancestry: {OrDash(pc.Ancestry)}   " +
                $"Class: {OrDash(pc.Class)}   Level: {pc.Level}";
            blocks.Add(new PdfBlock(summary, PdfBlockStyle.Body));
        }

        var npcs = campaign.Npcs
            .OrderBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        blocks.Add(new PdfBlock($"NPCs ({npcs.Count})", PdfBlockStyle.Heading));
        foreach (var npc in npcs)
        {
            AppendNpc(blocks, npc, system);
        }

        return SimplePdfWriter.Write($"{campaign.Name} - Campaign Summary", blocks);
    }

    private static void AppendNpc(List<PdfBlock> blocks, Npc npc, CharacterSystem? system)
    {
        blocks.Add(new PdfBlock(npc.Name, PdfBlockStyle.SubHeading));

        var summary = $"Role: {OrDash(npc.Role)}   Faction: {OrDash(npc.Faction)}   Location: {OrDash(npc.Location)}";
        blocks.Add(new PdfBlock(summary, PdfBlockStyle.Body));

        AppendIfPresent(blocks, "Appearance", npc.Appearance);
        AppendIfPresent(blocks, "Mannerism", npc.Mannerism);
        AppendIfPresent(blocks, "Motivation", npc.Motivation);
        AppendIfPresent(blocks, "Secret", npc.Secret);
        AppendIfPresent(blocks, "Notes", npc.Notes);

        var statBlocks = StatBlockBuilder.Build(npc.Stats, system?.NpcFields);
        if (statBlocks.Count > 0)
        {
            // A heading of its own (mirrors PlayerCharacterPdfExporter's identical "Stats" heading)
            // only when there's actually a stat block to show -- most incidental NPCs have none at
            // all (see Npc.Stats' own remarks), so this heading would otherwise appear before
            // nothing, right above the next NPC's own name.
            blocks.Add(new PdfBlock("Stats", PdfBlockStyle.Body));
            blocks.AddRange(statBlocks);
        }
    }

    private static void AppendIfPresent(List<PdfBlock> blocks, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            blocks.Add(new PdfBlock($"{label}: {value}", PdfBlockStyle.Body));
        }
    }

    private static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
}