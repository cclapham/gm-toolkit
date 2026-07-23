using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Models;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Read-only presentation wrapper around a <see cref="Campaign"/> for a single row in
/// <see cref="CampaignsViewModel"/>'s list. Exists separately from <see cref="Campaign"/> itself
/// so the view has a place to bind view-only state (<see cref="IsActive"/>) without adding
/// presentation concerns to the Core domain model.
/// </summary>
public sealed partial class CampaignListItemViewModel(Campaign campaign) : ObservableObject
{
    /// <summary>The underlying domain model -- exposed so <see cref="CampaignsViewModel"/> can
    /// pass it to <see cref="GmToolkit.Core.Services.ActiveCampaignContext.SelectCampaignAsync"/>
    /// and to <see cref="CampaignFormViewModel.BeginEdit"/>.</summary>
    public Campaign Campaign { get; } = campaign;

    public string Name => Campaign.Name;

    public string GameSystem => Campaign.GameSystem;

    public DateTime LastOpenedUtc => Campaign.LastOpenedUtc;

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
}