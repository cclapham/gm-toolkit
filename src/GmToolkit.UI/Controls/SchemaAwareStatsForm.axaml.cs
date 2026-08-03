using Avalonia;
using Avalonia.Controls;

using GmToolkit.UI.ViewModels.Stats;

namespace GmToolkit.UI.Controls;

/// <summary>
/// Reusable schema-driven stats form (issues #89/#90) -- renders one control per field in a
/// <see cref="SchemaStatsFormViewModel.Fields"/>, the right kind for each field's own
/// <see cref="StatFieldViewModel"/> runtime type (a <c>NumericUpDown</c> for
/// <see cref="NumberStatFieldViewModel"/>, a <c>ComboBox</c> for <see cref="EnumStatFieldViewModel"/>,
/// etc. -- see <c>SchemaAwareStatsForm.axaml</c>'s implicit per-type <c>DataTemplate</c>s). Shared by
/// <c>CharacterFormView.axaml</c> (<c>CharacterFormViewModel.SchemaForm</c>) and
/// <c>NpcFormView.axaml</c> (<c>NpcFormViewModel.SchemaForm</c>) -- its only public surface is
/// <see cref="FormViewModel"/>, so it carries no <c>PlayerCharacter</c>/<c>Npc</c>-specific knowledge
/// whatsoever, mirroring <c>MarkdownNotesEditor</c>'s identical "generic, both forms drop it in"
/// reasoning one level up in complexity.
/// </summary>
/// <remarks>
/// A <see cref="RepeatingGroupStatFieldViewModel"/>'s own template renders a nested
/// <c>ItemsControl</c> over each row's <see cref="RepeatingGroupRowViewModel.Fields"/> using these
/// same implicit templates -- safe without any special-casing because a row's item fields can never
/// themselves be another <see cref="RepeatingGroupStatFieldViewModel"/> or
/// <see cref="DerivedStatFieldViewModel"/> (rejected at pack-load time by
/// <c>CharacterSystemLoader</c>), so there's exactly one level of nesting, never unbounded recursion.
/// </remarks>
public partial class SchemaAwareStatsForm : UserControl
{
    public static readonly StyledProperty<SchemaStatsFormViewModel?> FormViewModelProperty =
        AvaloniaProperty.Register<SchemaAwareStatsForm, SchemaStatsFormViewModel?>(nameof(FormViewModel));

    public SchemaAwareStatsForm()
    {
        InitializeComponent();
    }

    /// <summary>The schema form to render -- bind to <c>CharacterFormViewModel.SchemaForm</c>/
    /// <c>NpcFormViewModel.SchemaForm</c>. Renders nothing when <c>null</c> (the caller is expected
    /// to gate this control's own <c>IsVisible</c> on <c>HasSchema</c> instead, but a <c>null</c>
    /// value is handled gracefully regardless).</summary>
    public SchemaStatsFormViewModel? FormViewModel
    {
        get => GetValue(FormViewModelProperty);
        set => SetValue(FormViewModelProperty, value);
    }
}