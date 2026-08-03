using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// One selectable entry in <see cref="CampaignFormViewModel.CharacterSystemOptions"/>'s system
/// dropdown -- either <see cref="Freeform"/> (<see cref="Id"/> <c>null</c>, mapping to a
/// <see cref="Core.Models.Campaign.CharacterSystemId"/> of <c>null</c>) or a real,
/// <see cref="ICharacterSystemRegistry"/>-backed <see cref="CharacterSystem"/>. A thin wrapper
/// rather than binding the dropdown directly to <see cref="CharacterSystem"/> itself, since
/// Freeform has no backing <see cref="CharacterSystem"/> instance to point to and the placeholder
/// built for a campaign's missing/uninstalled system (see
/// <see cref="CampaignFormViewModel.BeginEdit"/>'s remarks) has no real one either.
/// </summary>
/// <param name="Id">The <see cref="CharacterSystem.Id"/> this option maps to, or <c>null</c> for
/// <see cref="Freeform"/> -- exactly the value this option assigns to
/// <see cref="Core.Models.Campaign.CharacterSystemId"/> when selected and saved.</param>
/// <param name="Name">Display text -- shown via this record's own <see cref="ToString"/> override
/// (see its remarks) rather than a separate <c>DisplayMemberBinding</c>/converter.</param>
public sealed record CharacterSystemOption(string? Id, string Name)
{
    /// <summary>The sentinel "no system attached" option, always first in
    /// <see cref="CampaignFormViewModel.CharacterSystemOptions"/> -- see
    /// <see cref="Core.Models.Campaign.CharacterSystemId"/>'s own remarks for what a <c>null</c>
    /// id means for a campaign's stats.</summary>
    public static readonly CharacterSystemOption Freeform = new(null, "Freeform");

    /// <inheritdoc/>
    /// <remarks>Avalonia's default <c>ComboBox</c> item rendering falls back to
    /// <see cref="object.ToString"/> when no <c>ItemTemplate</c>/<c>DisplayMemberBinding</c> is
    /// set -- the same idiom <c>SettingsView.axaml</c>'s <c>ThemePreference</c> dropdown already
    /// relies on for an enum. Overridden here so the dropdown shows <see cref="Name"/> rather than
    /// this record's compiler-generated default <c>ToString</c>.</remarks>
    public override string ToString() => Name;
}