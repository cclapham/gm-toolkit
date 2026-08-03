using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>A <see cref="StatFieldTypes.Text"/> field -- bound to a single-line <c>TextBox</c> in
/// <c>SchemaAwareStatsForm.axaml</c>.</summary>
public sealed partial class TextStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public int MaxLength => Definition.MaxLength ?? CharacterSystemLoader.DefaultTextMaxLength;

    public override void LoadRawValue(string? rawValue)
    {
        Value = rawValue ?? string.Empty;
        ErrorMessage = null;
    }

    public override string RawValue => Value;

    public override bool Validate()
    {
        ErrorMessage = StatFieldValidator.ValidateValue(Definition, Value);
        return ErrorMessage is null;
    }

    partial void OnValueChanged(string value)
    {
        Validate();
        RaiseChanged();
    }
}