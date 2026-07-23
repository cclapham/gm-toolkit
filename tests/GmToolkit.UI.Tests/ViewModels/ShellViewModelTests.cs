using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

public class ShellViewModelTests
{
    [Fact]
    public void With_no_active_campaign_Characters_Npcs_and_Generator_are_disabled_and_gated()
    {
        var shell = new ShellViewModel(new NavigationService(), new ActiveCampaignContext(new FakeCampaignRepository()));

        Assert.False(shell.CharactersNavItem.IsEnabled);
        Assert.False(shell.NpcsNavItem.IsEnabled);
        Assert.False(shell.GeneratorNavItem.IsEnabled);
        Assert.Equal("Select a campaign first", shell.CharactersNavItem.GatingReason);
        Assert.Equal("Select a campaign first", shell.NpcsNavItem.GatingReason);
        Assert.Equal("Select a campaign first", shell.GeneratorNavItem.GatingReason);
    }

    [Fact]
    public void With_no_active_campaign_Campaigns_and_Settings_remain_enabled_and_ungated()
    {
        var shell = new ShellViewModel(new NavigationService(), new ActiveCampaignContext(new FakeCampaignRepository()));

        Assert.True(shell.CampaignsNavItem.IsEnabled);
        Assert.True(shell.SettingsNavItem.IsEnabled);
        Assert.Null(shell.CampaignsNavItem.GatingReason);
        Assert.Null(shell.SettingsNavItem.GatingReason);
    }

    [Fact]
    public void With_no_active_campaign_the_banner_is_shown()
    {
        var shell = new ShellViewModel(new NavigationService(), new ActiveCampaignContext(new FakeCampaignRepository()));

        Assert.True(shell.ShowActiveCampaignBanner);
    }

    [Fact]
    public async Task With_a_campaign_already_restored_before_construction_all_destinations_start_enabled()
    {
        // Mirrors real startup: both heads await/block on ActiveCampaignContext.RestoreLastOpenedAsync()
        // before the shell view model is ever constructed (see Program.cs / Application.cs), so its
        // initial state must already reflect the restored campaign with no extra wiring.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var activeCampaignContext = new ActiveCampaignContext(new FakeCampaignRepository(campaign));
        await activeCampaignContext.RestoreLastOpenedAsync();

        var shell = new ShellViewModel(new NavigationService(), activeCampaignContext);

        Assert.True(shell.CharactersNavItem.IsEnabled);
        Assert.True(shell.NpcsNavItem.IsEnabled);
        Assert.True(shell.GeneratorNavItem.IsEnabled);
        Assert.Null(shell.CharactersNavItem.GatingReason);
        Assert.False(shell.ShowActiveCampaignBanner);
    }

    [Fact]
    public void Campaigns_is_selected_on_construction()
    {
        var shell = new ShellViewModel(new NavigationService(), new ActiveCampaignContext(new FakeCampaignRepository()));

        Assert.True(shell.CampaignsNavItem.IsSelected);
        Assert.False(shell.CharactersNavItem.IsSelected);
        Assert.False(shell.NpcsNavItem.IsSelected);
        Assert.False(shell.GeneratorNavItem.IsSelected);
        Assert.False(shell.SettingsNavItem.IsSelected);
        Assert.IsType<CampaignsViewModel>(shell.CurrentViewModel);
    }

    [Fact]
    public async Task NavigateToCommand_to_an_enabled_destination_navigates_and_updates_selection()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var activeCampaignContext = new ActiveCampaignContext(new FakeCampaignRepository(campaign));
        await activeCampaignContext.RestoreLastOpenedAsync();
        var shell = new ShellViewModel(new NavigationService(), activeCampaignContext);

        shell.NavigateToCommand.Execute(NavigationDestination.Characters);

        Assert.IsType<CharactersViewModel>(shell.CurrentViewModel);
        Assert.True(shell.CharactersNavItem.IsSelected);
        Assert.False(shell.CampaignsNavItem.IsSelected);
    }

    [Fact]
    public void NavigateToCommand_to_a_gated_destination_without_an_active_campaign_redirects_to_Campaigns()
    {
        var shell = new ShellViewModel(new NavigationService(), new ActiveCampaignContext(new FakeCampaignRepository()));
        shell.NavigateToCommand.Execute(NavigationDestination.Settings);

        shell.NavigateToCommand.Execute(NavigationDestination.Npcs);

        Assert.IsType<CampaignsViewModel>(shell.CurrentViewModel);
        Assert.True(shell.CampaignsNavItem.IsSelected);
        Assert.False(shell.NpcsNavItem.IsSelected);
    }

    [Fact]
    public async Task HandleActiveCampaignChanged_after_selecting_a_campaign_enables_gated_destinations()
    {
        var repository = new FakeCampaignRepository();
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var shell = new ShellViewModel(new NavigationService(), activeCampaignContext);
        Assert.False(shell.CharactersNavItem.IsEnabled);

        var campaign = new Campaign { Name = "Wandering Souls" };
        await activeCampaignContext.SelectCampaignAsync(campaign);
        // The real ActiveCampaignChanged handler marshals through Avalonia.Threading.Dispatcher.UIThread,
        // which has no running message loop in a plain xUnit test -- call the underlying handler
        // directly instead (see its doc comment / the InternalsVisibleTo in AssemblyInfo.cs).
        shell.HandleActiveCampaignChanged();

        Assert.True(shell.CharactersNavItem.IsEnabled);
        Assert.True(shell.NpcsNavItem.IsEnabled);
        Assert.True(shell.GeneratorNavItem.IsEnabled);
        Assert.False(shell.ShowActiveCampaignBanner);
    }

    [Fact]
    public async Task HandleActiveCampaignChanged_redirects_away_from_a_now_gated_screen_when_the_active_campaign_clears()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var activeCampaignContext = new ActiveCampaignContext(new FakeCampaignRepository(campaign));
        await activeCampaignContext.RestoreLastOpenedAsync();
        var shell = new ShellViewModel(new NavigationService(), activeCampaignContext);
        shell.NavigateToCommand.Execute(NavigationDestination.Npcs);
        Assert.IsType<NpcsViewModel>(shell.CurrentViewModel);

        activeCampaignContext.Clear();
        shell.HandleActiveCampaignChanged();

        Assert.IsType<CampaignsViewModel>(shell.CurrentViewModel);
        Assert.True(shell.CampaignsNavItem.IsSelected);
        Assert.False(shell.NpcsNavItem.IsEnabled);
    }

    [Fact]
    public void Subscribing_to_the_real_ActiveCampaignChanged_event_does_not_throw_synchronously_without_a_running_dispatcher()
    {
        // This only proves the subscription path itself doesn't throw before the marshaled work
        // is even queued -- Dispatcher.UIThread.Post queues HandleActiveCampaignChanged rather
        // than running it, and there's no message loop pumping it in a plain xUnit test, so this
        // does NOT verify the marshaled handler's behavior. That's covered separately by calling
        // HandleActiveCampaignChanged() directly (see the other tests in this file).
        var activeCampaignContext = new ActiveCampaignContext(new FakeCampaignRepository());
        _ = new ShellViewModel(new NavigationService(), activeCampaignContext);
        var campaign = new Campaign { Name = "Wandering Souls" };

        var exception = Record.Exception(() => activeCampaignContext.SelectCampaignAsync(campaign).GetAwaiter().GetResult());

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_unsubscribes_from_the_navigation_service_so_selection_no_longer_updates()
    {
        var navigationService = new NavigationService();
        var shell = new ShellViewModel(navigationService, new ActiveCampaignContext(new FakeCampaignRepository()));

        shell.Dispose();
        navigationService.NavigateTo(NavigationDestination.Settings);

        Assert.Equal(NavigationDestination.Settings, navigationService.CurrentDestination);
        Assert.True(shell.CampaignsNavItem.IsSelected);
        Assert.False(shell.SettingsNavItem.IsSelected);
    }

    [Fact]
    public void Dispose_unsubscribes_from_ActiveCampaignChanged()
    {
        var activeCampaignContext = new ActiveCampaignContext(new FakeCampaignRepository());
        var shell = new ShellViewModel(new NavigationService(), activeCampaignContext);

        shell.Dispose();

        // ActiveCampaignChanged is a plain field-like event, so its backing delegate field shares
        // the event's name -- reflection is the only way to confirm the subscriber count without
        // adding test-only surface to ActiveCampaignContext itself. This is what actually proves
        // the second Dispose() unsubscription happened; the other Dispose test above only exercises
        // the navigation-service unsubscription.
        var backingField = typeof(ActiveCampaignContext).GetField(
            nameof(ActiveCampaignContext.ActiveCampaignChanged),
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var subscribers = (backingField!.GetValue(activeCampaignContext) as System.Delegate)?.GetInvocationList() ?? [];

        Assert.Empty(subscribers);
    }
}