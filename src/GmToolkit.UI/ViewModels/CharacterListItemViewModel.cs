using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Models;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Read-only presentation wrapper around a <see cref="PlayerCharacter"/> for a single row in
/// <see cref="CharactersViewModel"/>'s roster (issue #20). Exists separately from
/// <see cref="PlayerCharacter"/> itself so the view has a place to bind presentation-only
/// formatting (<see cref="ClassAndLevelLabel"/>, <see cref="StatsSummary"/>) without adding those
/// concerns to the Core domain model -- mirrors <see cref="CampaignListItemViewModel"/>.
/// </summary>
public sealed partial class CharacterListItemViewModel(PlayerCharacter playerCharacter) : ObservableObject
{
    /// <summary>The underlying domain model -- exposed so <see cref="CharactersViewModel"/> can
    /// pass it to <see cref="CharacterFormViewModel.BeginEdit"/>.</summary>
    public PlayerCharacter PlayerCharacter { get; } = playerCharacter;

    public string CharacterName => PlayerCharacter.CharacterName;

    public string PlayerName => PlayerCharacter.PlayerName;

    public string Class => PlayerCharacter.Class;

    public int Level => PlayerCharacter.Level;

    public string ClassAndLevelLabel => string.IsNullOrWhiteSpace(Class) ? $"Level {Level}" : $"{Class} · Level {Level}";

    /// <summary>
    /// Compact, single-line "at a glance" rendering of every entry in
    /// <see cref="PlayerCharacter.Stats"/> -- see <see cref="CharactersViewModel"/>'s remarks for
    /// why this shows the whole (system-agnostic, open-ended) stat bag rather than a curated
    /// "pinned" subset. <c>CharactersView.axaml</c> binds this with
    /// <c>TextTrimming="CharacterEllipsis"</c> and no wrapping, so a PC with an unusually large
    /// stat bag truncates gracefully instead of growing the row -- every row stays a small, fixed
    /// height regardless of stat count, which is what makes "four PCs readable on one screen
    /// without scrolling" (issue #20's acceptance criterion) actually hold.
    /// </summary>
    public string StatsSummary => PlayerCharacter.Stats.Count == 0
        ? "No stats recorded yet"
        : string.Join("  ·  ", PlayerCharacter.Stats.Select(stat => $"{stat.Key} {stat.Value}"));
}