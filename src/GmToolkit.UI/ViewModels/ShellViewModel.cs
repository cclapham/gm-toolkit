using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Services;
using GmToolkit.UI.Design;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Root view model for the app shell: a persistent navigation rail plus a content area showing
/// whichever screen <see cref="INavigationService"/> currently selects.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layout decision (issue #15, recorded here since it's expensive to reverse later):</b> a
/// single narrow navigation rail down the left edge (see <c>ShellView.axaml</c>, ~84px wide),
/// used unchanged at both desktop and phone-portrait widths — not two separate layouts swapped
/// at a breakpoint (e.g. side rail on desktop, bottom tab bar on phone). Reasoning:
/// </para>
/// <list type="bullet">
/// <item>Only 5 destinations. A rail this narrow comfortably fits all 5 without crowding, at both
/// ~360-400px phone-portrait width and typical desktop widths, so nothing forces a second layout.</item>
/// <item>One layout is meaningfully less surface than two for a first pass with no real screen
/// content behind these destinations yet (that lands in #17-#19+). A responsive breakpoint system
/// is a second visual/interaction design to build, test and keep in sync going forward, for a
/// benefit ("more idiomatic on phone") that isn't validated yet. Per CONTRIBUTING.md, the bar for
/// this hobby project is "does it work and is it tested," not building ahead of proven need.</item>
/// <item>Deviates from a literal icon-forward rail (the original ask): no icon font/asset exists
/// yet anywhere in the design system (<c>Styles/</c> has colors, spacing and typography, but no
/// icon set), and adding a new package dependency just for this issue's placeholder screens would
/// be scope creep. Short caption-styled text labels ("Campaigns", "Characters", ...) fill the icon
/// role for now; swapping in glyphs later is a pure <c>ShellView.axaml</c> change with no view
/// model impact.</item>
/// </list>
/// <para>
/// If real screen content later proves the rail too cramped at phone-portrait width, revisit
/// then — cheap to do once there's something concrete to react to, expensive to speculatively
/// build now.
/// </para>
/// </remarks>
public partial class ShellViewModel : ViewModelBase, System.IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly ActiveCampaignContext _activeCampaignContext;

    public ShellViewModel(INavigationService navigationService, ActiveCampaignContext activeCampaignContext)
    {
        _navigationService = navigationService;
        _activeCampaignContext = activeCampaignContext;

        NavItems =
        [
            CampaignsNavItem,
            CharactersNavItem,
            NpcsNavItem,
            GeneratorNavItem,
            SettingsNavItem,
        ];

        _navigationService.PropertyChanged += OnNavigationServicePropertyChanged;
        _activeCampaignContext.ActiveCampaignChanged += OnActiveCampaignChanged;

        // RestoreLastOpenedAsync() has already completed by the time either head constructs this
        // view model (both Program.cs and Application.cs await/block on it before starting the
        // Avalonia lifetime), so ActiveCampaignContext.ActiveCampaign already reflects the
        // restored state here -- no additional startup-ordering work needed.
        RefreshGatingState();
        RefreshSelection();
    }

    /// <summary>
    /// Design-time-only constructor for the XAML previewer's <c>Design.DataContext</c> (see
    /// <c>ShellView.axaml</c>) -- previewers construct view models directly rather than resolving
    /// them from the app's DI container. Wires up a real <see cref="NavigationService"/> and a
    /// real <see cref="ActiveCampaignContext"/> backed by an in-memory, always-empty repository:
    /// good enough to render the shell chrome (with no active campaign, i.e. the gated look) at
    /// design time. Never used at runtime -- both heads always resolve the constructor above via
    /// <c>App.Services</c>.
    /// </summary>
    public ShellViewModel()
        : this(new NavigationService(), new ActiveCampaignContext(new DesignTimeCampaignRepository()))
    {
    }

    public NavItemViewModel CampaignsNavItem { get; } = new(NavigationDestination.Campaigns, "Campaigns", requiresActiveCampaign: false);

    public NavItemViewModel CharactersNavItem { get; } = new(NavigationDestination.Characters, "Characters", requiresActiveCampaign: true);

    public NavItemViewModel NpcsNavItem { get; } = new(NavigationDestination.Npcs, "NPCs", requiresActiveCampaign: true);

    public NavItemViewModel GeneratorNavItem { get; } = new(NavigationDestination.Generator, "Generator", requiresActiveCampaign: true);

    public NavItemViewModel SettingsNavItem { get; } = new(NavigationDestination.Settings, "Settings", requiresActiveCampaign: false);

    /// <summary>All 5 nav items in rail order -- convenient for uniform iteration (gating,
    /// selection refresh) and for tests; the individual properties above exist for direct XAML
    /// binding.</summary>
    public IReadOnlyList<NavItemViewModel> NavItems { get; }

    public ViewModelBase CurrentViewModel => _navigationService.CurrentViewModel;

    /// <summary>
    /// Whether to show the "select a campaign" banner above the content area. True whenever
    /// there's no active campaign, regardless of which screen is currently showing, so the reason
    /// Characters/NPCs/Generator are greyed out stays visible even while parked on Campaigns or
    /// Settings (the two always-available destinations).
    /// </summary>
    [ObservableProperty]
    public partial bool ShowActiveCampaignBanner { get; set; }

    [RelayCommand]
    private void NavigateTo(NavigationDestination destination)
    {
        var item = NavItems.First(navItem => navItem.Destination == destination);

        // The primary gate is the disabled nav button itself (it can't be clicked). This is a
        // defensive fallback for any navigation attempt that doesn't go through the button -- e.g.
        // a future deep link -- so it always lands somewhere valid instead of showing a gated
        // screen.
        _navigationService.NavigateTo(item.IsEnabled ? destination : NavigationDestination.Campaigns);
    }

    private void OnNavigationServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(INavigationService.CurrentDestination):
                RefreshSelection();
                break;
            case nameof(INavigationService.CurrentViewModel):
                OnPropertyChanged(nameof(CurrentViewModel));
                break;
        }
    }

    private void OnActiveCampaignChanged()
    {
        // ActiveCampaignContext.ActiveCampaignChanged can fire from a non-UI thread (Android
        // restores the active campaign inside a background Task.Run at startup) -- always marshal
        // back to the UI thread before touching bound state.
        Dispatcher.UIThread.Post(HandleActiveCampaignChanged);
    }

    /// <summary>
    /// The actual work done in response to <see cref="ActiveCampaignContext.ActiveCampaignChanged"/>,
    /// factored out of <see cref="OnActiveCampaignChanged"/> so it's callable directly (bypassing
    /// <see cref="Dispatcher.UIThread"/>) from tests that don't run inside an Avalonia dispatcher
    /// loop. Not called directly by application code -- see <c>InternalsVisibleTo</c> in
    /// <c>AssemblyInfo.cs</c>.
    /// </summary>
    internal void HandleActiveCampaignChanged()
    {
        RefreshGatingState();

        // If we're currently on a gated screen and the active campaign just disappeared (e.g.
        // #19's delete-active-campaign flow calls ActiveCampaignContext.Clear()), redirect back
        // to Campaigns rather than leaving a now-invalid screen showing.
        var currentItem = NavItems.First(navItem => navItem.Destination == _navigationService.CurrentDestination);
        if (!currentItem.IsEnabled)
        {
            _navigationService.NavigateTo(NavigationDestination.Campaigns);
        }
    }

    private void RefreshGatingState()
    {
        var hasActiveCampaign = _activeCampaignContext.ActiveCampaign is not null;

        foreach (var item in NavItems)
        {
            item.IsEnabled = !item.RequiresActiveCampaign || hasActiveCampaign;
            item.GatingReason = item.IsEnabled ? null : "Select a campaign first";
        }

        ShowActiveCampaignBanner = !hasActiveCampaign;
    }

    private void RefreshSelection()
    {
        foreach (var item in NavItems)
        {
            item.IsSelected = item.Destination == _navigationService.CurrentDestination;
        }
    }

    public void Dispose()
    {
        _navigationService.PropertyChanged -= OnNavigationServicePropertyChanged;
        _activeCampaignContext.ActiveCampaignChanged -= OnActiveCampaignChanged;
    }
}