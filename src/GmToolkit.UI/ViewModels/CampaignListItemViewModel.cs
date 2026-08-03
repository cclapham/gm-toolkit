using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Read-only presentation wrapper around a <see cref="Campaign"/> for a single row in
/// <see cref="CampaignsViewModel"/>'s list. Exists separately from <see cref="Campaign"/> itself
/// so the view has a place to bind view-only state (<see cref="IsActive"/>) without adding
/// presentation concerns to the Core domain model.
/// </summary>
/// <param name="characterSystemRegistry">Looked up (via <see cref="ICharacterSystemRegistry.TryGetById"/>,
/// never the throwing <see cref="ICharacterSystemRegistry.GetById"/>) purely to resolve
/// <see cref="Campaign.CharacterSystemId"/> to a display name for <see cref="CharacterSystemDisplayName"/>
/// -- see that property's remarks for why the non-throwing lookup matters here specifically.</param>
public sealed partial class CampaignListItemViewModel(Campaign campaign, ICharacterSystemRegistry characterSystemRegistry) : ObservableObject
{
    /// <summary>The underlying domain model -- exposed so <see cref="CampaignsViewModel"/> can
    /// pass it to <see cref="GmToolkit.Core.Services.ActiveCampaignContext.SelectCampaignAsync"/>
    /// and to <see cref="CampaignFormViewModel.BeginEdit"/>.</summary>
    public Campaign Campaign { get; } = campaign;

    public string Name => Campaign.Name;

    public string GameSystem => Campaign.GameSystem;

    /// <summary>
    /// Display label for the typed system attached via <see cref="Campaign.CharacterSystemId"/> (see
    /// SYSTEMS.md's "Attachment point"), shown as a badge next to the campaign's name -- <c>null</c>
    /// (Freeform, no system attached) shows no badge at all rather than an empty one. Uses
    /// <see cref="ICharacterSystemRegistry.TryGetById"/>, not the throwing
    /// <see cref="ICharacterSystemRegistry.GetById"/>: an id that no longer resolves (its system
    /// pack was uninstalled since this campaign attached it) is an ordinary, expected state for a
    /// read-only list row to handle gracefully, not a bug to throw over -- it falls back to showing
    /// the raw id with a "not installed" note so the attachment isn't silently hidden.
    /// </summary>
    public string? CharacterSystemDisplayName =>
        Campaign.CharacterSystemId is null
            ? null
            : characterSystemRegistry.TryGetById(Campaign.CharacterSystemId, out var system)
                ? system.Name
                : $"{Campaign.CharacterSystemId} (not installed)";

    /// <summary>Whether <see cref="CharacterSystemDisplayName"/> has anything to show -- drives the
    /// badge's <c>IsVisible</c> binding in <c>CampaignsView.axaml</c> rather than relying on an
    /// empty-string/whitespace converter.</summary>
    public bool HasCharacterSystem => CharacterSystemDisplayName is not null;

    public DateTime LastOpenedUtc => Campaign.LastOpenedUtc;

    public DateTime CreatedUtc => Campaign.CreatedUtc;

    /// <summary>
    /// <see cref="Campaign.Description"/> for display in the expanded row (issue #72), with a
    /// placeholder in place of an empty gap when the campaign has none -- <see cref="Description"/>
    /// is optional free text (see <c>Campaign.Description</c>'s doc comment), so a never-filled-in
    /// field is a routine case, not an error state.
    /// </summary>
    public string DescriptionOrPlaceholder =>
        string.IsNullOrWhiteSpace(Campaign.Description) ? "No description" : Campaign.Description;

    /// <summary>
    /// Counts come straight from <see cref="Campaign.PlayerCharacters"/>/<see cref="Campaign.Npcs"/>
    /// as populated by <c>ICampaignRepository.GetAllAsync</c> -- no separate count query needed at
    /// this data scale (see <see cref="CampaignsViewModel"/>'s doc comment).
    /// </summary>
    public int PlayerCharacterCount => Campaign.PlayerCharacters.Count;

    public int NpcCount => Campaign.Npcs.Count;

    public string CountsLabel =>
        $"{PlayerCharacterCount} PC{(PlayerCharacterCount == 1 ? string.Empty : "s")} · " +
        $"{NpcCount} NPC{(NpcCount == 1 ? string.Empty : "s")}";

    /// <summary>Copy for the inline delete-confirmation panel (issue #19): names the campaign and
    /// states the PC/NPC counts it will destroy, per that issue's acceptance criteria. Computed
    /// here (rather than assembled inline in <c>CampaignsView.axaml</c>) so the confirmation panel
    /// only needs one straightforward string binding.</summary>
    public string DeleteConfirmationPrompt =>
        $"Permanently delete \"{Name}\"? This also destroys its {CountsLabel}. This can't be undone.";

    /// <summary>Whether this is <see cref="GmToolkit.Core.Services.ActiveCampaignContext.ActiveCampaign"/>
    /// -- kept as separate bindable state rather than on <see cref="Campaign"/> itself, since it's
    /// a function of app-wide selection state, not a property of the campaign.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    /// <summary>Whether this row's inline delete-confirmation panel (issue #19) is showing --
    /// toggled by <see cref="CampaignsViewModel"/>, which owns the actual confirmation state
    /// (<c>DeleteConfirmationInput</c>, <c>ConfirmDeleteCommand</c>, etc.) since only one row can
    /// be mid-delete at a time. Kept here (rather than e.g. a converter comparing against a
    /// "pending delete" id on the parent view model) so <c>CampaignsView.axaml</c> can bind this
    /// row's confirmation panel visibility directly, the same way it already binds
    /// <see cref="IsActive"/>.</summary>
    [ObservableProperty]
    public partial bool IsShowingDeleteConfirmation { get; set; }

    /// <summary>
    /// Whether this row's expanded overview panel (issue #72: description + created date,
    /// alongside the compact summary line that stays visible either way) is showing. Collapsed by
    /// default and toggled independently per row (not a single shared "which row is expanded"
    /// piece of state on <see cref="CampaignsViewModel"/>, unlike <c>IsShowingDeleteConfirmation</c>
    /// above) -- there's no reason opening one campaign's details should close another's, the way
    /// there is for delete confirmations (only one destructive action should be able to be mid-flight
    /// at a time).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandToggleLabel))]
    public partial bool IsExpanded { get; set; }

    /// <summary>Button label for the expand/collapse toggle above, reflecting its current state --
    /// computed here (rather than a converter in <c>CampaignsView.axaml</c>) so the view only needs
    /// one straightforward string binding, matching <see cref="DeleteConfirmationPrompt"/>'s
    /// convention.</summary>
    public string ExpandToggleLabel => IsExpanded ? "Less ▲" : "More ▼";
}