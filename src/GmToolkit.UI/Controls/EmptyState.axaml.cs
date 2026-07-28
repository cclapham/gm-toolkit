using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;

namespace GmToolkit.UI.Controls;

/// <summary>
/// Reusable "nothing here yet" / "no results" control (issue #23): an optional icon, a heading, an
/// optional message, and an optional call-to-action button. Used by <c>CampaignsView.axaml</c>,
/// <c>CharactersView.axaml</c> and <c>NpcsView.axaml</c> for both their "zero items at all" empty
/// state and (<c>NpcsView.axaml</c> only) their "zero items match the current search/filter" state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lives in <c>GmToolkit.UI/Controls</c>, a new folder, rather than alongside
/// <c>MarkdownNotesEditor.axaml</c> in <c>GmToolkit.UI/Views</c>.</b> <c>MarkdownNotesEditor</c> is
/// the only precedent for a reusable, non-screen <c>UserControl</c> in this codebase, and it lives
/// directly under <c>Views/</c> because introducing a whole new folder for one control wasn't worth
/// it. This issue adds two general-purpose controls at once (<see cref="EmptyState"/> and
/// <see cref="LoadingIndicator"/>), which is exactly the point at which "reusable UI building
/// block" becomes a real, recurring category rather than a one-off -- so a dedicated
/// <c>Controls/</c> folder earns its keep here, and is the clearer convention going forward for
/// whatever's added next (e.g. a future confirmation-dialog control to de-duplicate
/// <c>CampaignsView.axaml</c>'s inline delete-confirmation panel). <c>MarkdownNotesEditor.axaml</c>
/// itself is left where it is -- moving it isn't this issue's job, and it's arguably still fine
/// where it is as a single, screen-adjacent editing widget rather than a generic layout primitive
/// like the two controls added here.
/// </para>
/// <para>
/// <b>Icon is a plain glyph string, not an icon asset.</b> This codebase has no icon-font or
/// image-asset convention (checked -- there's no <c>Controls/</c> folder, and no existing screen
/// references an icon resource at all), and introducing an entire asset pipeline (font subsetting,
/// a resource dictionary of icon keys, licensing for a third-party icon set, etc.) just so this one
/// issue's empty states can show a picture would be a lot of infrastructure for very little payoff.
/// A single large emoji/glyph character rendered in a big <see cref="TextBlock"/> gets the same
/// "at-a-glance visual anchor" job done with zero new dependencies, no licensing to track, and
/// nothing left over to maintain if the app later does grow a real icon system -- callers just pass
/// a different string then, no control API change needed. See each view (e.g.
/// <c>CampaignsView.axaml</c>) for which glyph was chosen and why.
/// </para>
/// <para>
/// <b><see cref="Icon"/> is optional</b>, not required, in case a future caller has nothing
/// meaningful to show -- unset/empty hides the glyph's <see cref="TextBlock"/> entirely rather than
/// rendering an empty box that still takes up its row's height.
/// </para>
/// </remarks>
public partial class EmptyState : UserControl
{
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Icon));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string>(nameof(Title), defaultValue: string.Empty);

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsCompact));

    public static readonly StyledProperty<string?> ActionTextProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(ActionCommand));

    /// <summary>Read-only; recomputed by <see cref="UpdateHasAction"/> whenever
    /// <see cref="ActionText"/> or <see cref="ActionCommand"/> changes -- see <see cref="HasAction"/>'s
    /// remarks.</summary>
    private static readonly StyledProperty<bool> HasActionProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(HasAction));

    static EmptyState()
    {
        ActionTextProperty.Changed.AddClassHandler<EmptyState>((control, _) => control.UpdateHasAction());
        ActionCommandProperty.Changed.AddClassHandler<EmptyState>((control, _) => control.UpdateHasAction());
    }

    public EmptyState()
    {
        InitializeComponent();
    }

    /// <summary>A single large glyph/emoji character, or <c>null</c>/empty to show no icon at all --
    /// see this type's remarks for why a glyph string rather than an icon asset.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The heading. Rendered with the "display" type-scale step unless
    /// <see cref="IsCompact"/> is set.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Supporting body text below the heading, or <c>null</c>/empty to show none.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>When <c>true</c>, renders <see cref="Title"/> with the smaller "title" type-scale
    /// step instead of "display" -- used by <c>NpcsView.axaml</c>'s "no search results" state, which
    /// is a transient nudge rather than a first-run "welcome" moment and keeps its previous, less
    /// prominent look now that it shares this control.</summary>
    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>Call-to-action button label. Both this and <see cref="ActionCommand"/> must be
    /// supplied for the button to show -- see <see cref="HasAction"/>'s remarks.</summary>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>Call-to-action command. Both this and <see cref="ActionText"/> must be supplied for
    /// the button to show -- see <see cref="HasAction"/>'s remarks.</summary>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>
    /// Whether the call-to-action button should render: <c>true</c> only when both
    /// <see cref="ActionText"/> is non-empty and <see cref="ActionCommand"/> is non-null.
    /// </summary>
    /// <remarks>
    /// Requiring both, not either, avoids the two broken-looking half-states a caller could
    /// otherwise produce by accident: a button with a command but blank/no label (clickable, but
    /// says nothing about what it does), or a button with a label but no command (looks
    /// actionable, does nothing when clicked). <c>NpcsView.axaml</c>'s "no search results" state
    /// deliberately supplies neither, which is what makes the button not render at all there.
    /// </remarks>
    public bool HasAction => GetValue(HasActionProperty);

    private void UpdateHasAction() =>
        SetValue(HasActionProperty, !string.IsNullOrEmpty(ActionText) && ActionCommand is not null);
}