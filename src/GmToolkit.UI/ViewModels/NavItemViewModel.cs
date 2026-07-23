using CommunityToolkit.Mvvm.ComponentModel;

using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Presentation state for a single navigation-rail destination: whether it's the current screen,
/// whether it's currently reachable (see <see cref="ShellViewModel"/>'s active-campaign gating),
/// and — when it isn't — why, so the shell can surface that reason to the user instead of just
/// greying the button out silently.
/// </summary>
public partial class NavItemViewModel : ObservableObject
{
    public NavItemViewModel(NavigationDestination destination, string label, bool requiresActiveCampaign)
    {
        Destination = destination;
        Label = label;
        RequiresActiveCampaign = requiresActiveCampaign;
        IsEnabled = !requiresActiveCampaign;
    }

    public NavigationDestination Destination { get; }

    public string Label { get; }

    /// <summary>
    /// Whether this destination is unreachable without an active campaign (Characters, NPCs,
    /// Generator) as opposed to always available (Campaigns, Settings).
    /// </summary>
    public bool RequiresActiveCampaign { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>
    /// Human-readable reason this destination is currently disabled, shown as a tooltip;
    /// <c>null</c> whenever <see cref="IsEnabled"/> is <c>true</c>.
    /// </summary>
    [ObservableProperty]
    public partial string? GatingReason { get; set; }
}