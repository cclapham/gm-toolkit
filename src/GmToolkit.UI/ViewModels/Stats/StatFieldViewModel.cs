using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>
/// Base type for one schema-driven stat field's editable state (issues #89/#90) -- a typed "view"
/// over a single entry in a <c>PlayerCharacter.Stats</c>/<c>Npc.Stats</c> bag, the same relationship
/// <see cref="CharacterSystem"/> itself has to that bag as a whole. Concrete subclasses (one per
/// <see cref="StatFieldTypes"/> discriminator -- see <see cref="StatFieldViewModelFactory"/>) each
/// carry whatever typed <c>Value</c> property their control actually binds to (a <c>decimal?</c> for
/// <see cref="NumberStatFieldViewModel"/>, a <c>string</c> for <see cref="TextStatFieldViewModel"/>,
/// etc.); this base only carries what every field type shares -- its <see cref="Definition"/>,
/// display text, a validation <see cref="ErrorMessage"/>, and the raw string form
/// <see cref="RawValue"/> that's actually what gets written back into the Stats dictionary.
/// </summary>
public abstract partial class StatFieldViewModel(StatFieldDefinition definition) : ObservableObject
{
    /// <summary>The schema field this instance renders/edits.</summary>
    public StatFieldDefinition Definition { get; } = definition;

    public string Key => Definition.Key;

    public string Label => Definition.Label;

    public string? HelpText => Definition.HelpText;

    /// <summary>First validation message from the most recent <see cref="Validate"/> call, or
    /// <c>null</c> if this field's current value is valid -- mirrors
    /// <see cref="CharacterFormViewModel.NameError"/>'s bindable-error idiom, one level down.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Raised whenever this field's edited value changes -- never raised by
    /// <see cref="DerivedStatFieldViewModel"/>, which is never user-edited (see its own remarks).
    /// <see cref="SchemaStatsFormViewModel"/> subscribes to recompute derived fields and re-validate
    /// on every keystroke, mirroring <c>CharacterFormViewModel.OnStatRowChanged</c>'s identical
    /// per-edit re-validation for the freeform stat rows this form replaces when a schema is attached.
    /// </summary>
    public event Action? Changed;

    /// <summary>Loads this field's editable state from <paramref name="rawValue"/> (the string
    /// currently stored under <see cref="Key"/> in a Stats bag, or <c>null</c> if absent) -- a pure
    /// load, no <see cref="Changed"/> raised and no validation performed (the caller validates once,
    /// after every field in the form has finished loading).</summary>
    public abstract void LoadRawValue(string? rawValue);

    /// <summary>This field's current value, string-serialized exactly as it belongs in a Stats bag --
    /// what <see cref="SchemaStatsFormViewModel.BuildStats"/> actually writes back under
    /// <see cref="Key"/>. Never persisted at all for <see cref="DerivedStatFieldViewModel"/> (see its
    /// own remarks); <see cref="SchemaStatsFormViewModel"/> skips derived fields entirely rather than
    /// relying on this property returning something meaningful for them.</summary>
    public abstract string RawValue { get; }

    /// <summary>Validates this field's current value against <see cref="Definition"/>, sets
    /// <see cref="ErrorMessage"/>, and returns whether it's currently valid.</summary>
    public abstract bool Validate();

    protected void RaiseChanged() => Changed?.Invoke();
}