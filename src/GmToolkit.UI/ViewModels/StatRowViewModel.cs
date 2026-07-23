using CommunityToolkit.Mvvm.ComponentModel;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// One editable key/value row in <see cref="CharacterFormViewModel.StatRows"/> (issue #21) --
/// bound to a pair of <c>TextBox</c>es plus a per-row remove button in
/// <c>CharacterFormView.axaml</c>. Deliberately a plain mutable key/value pair rather than
/// re-using <see cref="System.Collections.Generic.KeyValuePair{TKey,TValue}"/>: that type is
/// immutable and gives Avalonia's two-way <c>TextBox.Text</c> binding nothing to write back into.
/// </summary>
public sealed partial class StatRowViewModel : ObservableObject
{
    public StatRowViewModel(string key, string value)
    {
        Key = key;
        Value = value;
    }

    [ObservableProperty]
    public partial string Key { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; }
}