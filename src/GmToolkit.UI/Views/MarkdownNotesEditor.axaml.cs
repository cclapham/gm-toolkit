using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

using GmToolkit.UI.Markdown;

namespace GmToolkit.UI.Views;

/// <summary>
/// Reusable edit/preview markdown control (issue #22) -- a plain multi-line <see cref="TextBox"/>
/// with a toggle to switch to a read-only rendered preview built by
/// <see cref="Markdown.MarkdownRenderer"/>. Bind <see cref="Text"/> to whatever plain-string domain
/// property stores the markdown source (e.g. <c>PlayerCharacter.Notes</c>, see
/// <c>CharacterFormView.axaml</c>) -- nothing about the underlying storage changes; this is purely a
/// presentation concern, same as <c>MarkdownRenderer</c> itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>PlayerCharacter</c>/<c>Npc</c>-specific knowledge whatsoever</b> -- its whole public
/// surface is <see cref="Text"/>, <see cref="PlaceholderText"/> and <see cref="EditorHeight"/>, all
/// generic. That's deliberate: issue #22 only wires this into <c>CharacterFormView.axaml</c> for
/// <c>PlayerCharacter.Notes</c>, but issue #25 (the not-yet-built NPC form) needs the exact same
/// control for <c>Npc.Notes</c> later, and should be able to drop it in with nothing more than
/// <c>&lt;views:MarkdownNotesEditor Text="{Binding Notes, Mode=TwoWay}" PlaceholderText="..." /&gt;</c>.
/// </para>
/// <para>
/// <b>Starts in edit mode</b> (<see cref="IsPreview"/> defaults to <c>false</c>) -- a brand-new or
/// currently-empty note has nothing worth previewing yet, and this matches the plain <c>TextBox</c>
/// this control replaces (see <c>CharacterFormView.axaml</c>'s git history) having simply been an
/// editor with no separate preview step at all.
/// </para>
/// <para>
/// <b>The preview is rebuilt lazily, not on every keystroke.</b> <see cref="MarkdownRenderer.Render"/>
/// only actually runs when preview mode is entered, or when <see cref="Text"/> changes while preview
/// mode is already showing -- there's no reason to pay a re-parse+re-render cost on every character
/// typed while the preview isn't even visible.
/// </para>
/// </remarks>
public partial class MarkdownNotesEditor : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownNotesEditor, string>(nameof(Text), defaultValue: string.Empty, defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<MarkdownNotesEditor, string?>(nameof(PlaceholderText));

    /// <summary>Default of 80 mirrors the fixed <c>Height="80"</c> the plain <c>TextBox</c> this
    /// control replaces used to have.</summary>
    public static readonly StyledProperty<double> EditorHeightProperty =
        AvaloniaProperty.Register<MarkdownNotesEditor, double>(nameof(EditorHeight), defaultValue: 80);

    public static readonly StyledProperty<bool> IsPreviewProperty =
        AvaloniaProperty.Register<MarkdownNotesEditor, bool>(nameof(IsPreview));

    private readonly MarkdownRenderer _renderer = new();

    static MarkdownNotesEditor()
    {
        TextProperty.Changed.AddClassHandler<MarkdownNotesEditor>((editor, _) => editor.RefreshPreviewIfShown());
        IsPreviewProperty.Changed.AddClassHandler<MarkdownNotesEditor>((editor, _) => editor.OnIsPreviewChanged());
    }

    public MarkdownNotesEditor()
    {
        InitializeComponent();
        OnIsPreviewChanged();
    }

    /// <summary>The raw markdown source -- bind this to the underlying domain string property (e.g.
    /// <c>PlayerCharacter.Notes</c>). Two-way by default.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Placeholder shown in the editor's <see cref="TextBox"/> when <see cref="Text"/> is empty.</summary>
    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Height of the editable text area, and minimum height of the rendered preview -- see
    /// <see cref="EditorHeightProperty"/>'s remarks on its default.</summary>
    public double EditorHeight
    {
        get => GetValue(EditorHeightProperty);
        set => SetValue(EditorHeightProperty, value);
    }

    /// <summary>Whether the read-only rendered preview (rather than the editable <see cref="TextBox"/>)
    /// is currently shown.</summary>
    public bool IsPreview
    {
        get => GetValue(IsPreviewProperty);
        set => SetValue(IsPreviewProperty, value);
    }

    private void OnEditClick(object? sender, RoutedEventArgs e) => IsPreview = false;

    private void OnPreviewClick(object? sender, RoutedEventArgs e) => IsPreview = true;

    /// <summary>Keeps the Edit/Preview buttons' highlighted state in sync with <see cref="IsPreview"/>,
    /// and refreshes the preview content if it just became visible.</summary>
    private void OnIsPreviewChanged()
    {
        EditButton?.Classes.Set("accent", !IsPreview);
        PreviewButton?.Classes.Set("accent", IsPreview);
        RefreshPreviewIfShown();
    }

    private void RefreshPreviewIfShown()
    {
        if (IsPreview && PreviewHost is not null)
        {
            PreviewHost.Content = _renderer.Render(Text);
        }
    }
}