using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>A <see cref="StatFieldTypes.FreeTextBlock"/> field -- bound to a multi-line <c>TextBox</c>
/// in <c>SchemaAwareStatsForm.axaml</c>. Identical shape to <see cref="TextStatFieldViewModel"/>
/// (both are a plain <see cref="string"/> value) but a separate type rather than a shared base with a
/// "is this multiline" flag: they differ in default <see cref="MaxLength"/>
/// (<see cref="CharacterSystemLoader.DefaultFreeTextBlockMaxLength"/> vs.
/// <see cref="CharacterSystemLoader.DefaultTextMaxLength"/>), never support <c>pattern</c>
/// (<see cref="StatFieldValidator"/> enforces this), and the view picks a taller editor for this type
/// by its runtime type, same "implicit DataTemplate per view model type" idiom every other field type
/// uses.</summary>
public sealed partial class FreeTextBlockStatFieldViewModel(StatFieldDefinition definition) : StatFieldViewModel(definition)
{
    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;

    public int MaxLength => Definition.MaxLength ?? CharacterSystemLoader.DefaultFreeTextBlockMaxLength;

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