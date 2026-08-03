using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>A <see cref="StatFieldTypes.Boolean"/> field -- bound to a <c>CheckBox</c> in
/// <c>SchemaAwareStatsForm.axaml</c>. Always has a concrete <c>true</c>/<c>false</c> value (a
/// <c>CheckBox</c> has no third "not set" state), so unlike every other leaf field type an absent or
/// unparseable stored value simply loads as <c>false</c> rather than staying blank/null.</summary>
public sealed partial class BooleanStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    [ObservableProperty]
    public partial bool Value { get; set; }

    public override void LoadRawValue(string? rawValue)
    {
        Value = bool.TryParse(rawValue, out var parsed) && parsed;
        ErrorMessage = null;
    }

    public override string RawValue => Value ? bool.TrueString : bool.FalseString;

    public override bool Validate()
    {
        // Always valid -- see this class's remarks on why there's no "unset" state to reject.
        ErrorMessage = null;
        return true;
    }

    partial void OnValueChanged(bool value) => RaiseChanged();
}