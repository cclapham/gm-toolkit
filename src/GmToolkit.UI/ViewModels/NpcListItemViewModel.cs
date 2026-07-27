using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Models;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Read-only presentation wrapper around an <see cref="Npc"/> for a single row in
/// <see cref="NpcsViewModel"/>'s list (issue #24). Exists separately from <see cref="Npc"/> itself
/// so the view has a place to bind presentation-only formatting (<see cref="NotesPreview"/>) without
/// adding that concern to the Core domain model -- mirrors <see cref="CharacterListItemViewModel"/>/
/// <see cref="CampaignListItemViewModel"/>.
/// </summary>
public sealed partial class NpcListItemViewModel(Npc npc) : ObservableObject
{
    /// <summary>The underlying domain model -- exposed so <see cref="NpcsViewModel"/> can read
    /// straight from it (e.g. for the faction/location filter option lists) rather than needing a
    /// second lookup back to the source <see cref="Npc"/>.</summary>
    public Npc Npc { get; } = npc;

    public string Name => Npc.Name;

    public string Role => Npc.Role;

    public string Faction => Npc.Faction;

    public string Location => Npc.Location;

    /// <summary>Visual marker for "known to players" (issue #24's own task) -- see
    /// <c>NpcsView.axaml</c>'s row template for the badge itself and its styling rationale.</summary>
    public bool KnownToPlayers => Npc.KnownToPlayers;

    public DateTime CreatedUtc => Npc.CreatedUtc;

    /// <summary>
    /// Single-line, ellipsis-truncated rendering of <see cref="Npc.Notes"/> -- same "compact
    /// inline summary" idiom as <see cref="CharacterListItemViewModel.StatsSummary"/>, so a row
    /// with a long notes field stays a small, fixed height instead of growing and pushing other
    /// NPCs out of view, which matters for this issue's "find one NPC in a list of 100 quickly"
    /// acceptance criterion just as much as the search/filter/sort themselves do.
    /// </summary>
    public string NotesPreview => string.IsNullOrWhiteSpace(Notes) ? "No notes yet" : Notes;

    private string Notes => Npc.Notes;
}