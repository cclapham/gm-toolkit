using Avalonia;
using Avalonia.Controls;

namespace GmToolkit.UI.Controls;

/// <summary>
/// Reusable loading indicator (issue #23), shown while a screen's <c>IsLoading</c> is <c>true</c> --
/// an indeterminate <see cref="ProgressBar"/> plus an optional caption. Used by
/// <c>CampaignsView.axaml</c>, <c>CharactersView.axaml</c> and <c>NpcsView.axaml</c>, none of which
/// previously rendered anything at all for this state (verified before starting this issue: no
/// <c>IsVisible="{Binding IsLoading}"</c> block existed in any of the three), which meant a slow
/// load showed a genuinely blank area -- exactly what this issue's acceptance criterion rules out.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deliberately not a skeleton-shimmer layout</b> that mimics the eventual rows/cards. A real
/// skeleton system (placeholder shapes matching each screen's list-item layout, a shimmer/pulse
/// animation, per-screen skeleton templates for the campaign row vs. the character row vs. the NPC
/// row) is a genuinely different, much larger amount of work than a spinner, and it buys close to
/// nothing here: every one of this app's loads is a read from a local SQLite file (see
/// <c>GmToolkit.Data</c>), not a network round-trip, so in practice <c>IsLoading</c> is true for
/// single-digit milliseconds except on the slowest hardware this app targets (Raspberry Pi 4). An
/// indeterminate <see cref="ProgressBar"/> (Avalonia's built-in animated bar, no extra work needed)
/// plus an optional message is proportionate to that latency profile; a skeleton system sized for
/// perceived-performance-on-a-slow-network is not a problem this app actually has.
/// </para>
/// <para>
/// <b><see cref="Message"/> is optional</b>, matching <c>EmptyState.Message</c>'s idiom -- a bare
/// spinner with no caption is a perfectly legible "something is happening" signal on its own, so
/// callers aren't forced to supply text that would just repeat the screen's own heading.
/// </para>
/// </remarks>
public partial class LoadingIndicator : UserControl
{
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<LoadingIndicator, string?>(nameof(Message));

    public LoadingIndicator()
    {
        InitializeComponent();
    }

    /// <summary>Optional caption shown below the spinner, or <c>null</c>/empty to show none.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
}