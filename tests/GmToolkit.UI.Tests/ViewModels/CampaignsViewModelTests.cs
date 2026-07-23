using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// <see cref="FakeCampaignRepository"/> completes every call synchronously (via
/// <see cref="Task.FromResult{TResult}"/>), so <see cref="CampaignsViewModel"/>'s
/// fire-and-forget initial load (kicked off from its constructor) has always finished loading by
/// the time the constructor returns -- no extra waiting needed in these tests. This would not
/// hold for a genuinely asynchronous repository (e.g. the real SQLite-backed one).
/// </remarks>
public class CampaignsViewModelTests
{
    [Fact]
    public void With_no_campaigns_the_empty_state_is_shown()
    {
        var vm = new CampaignsViewModel(new FakeCampaignRepository(), new ActiveCampaignContext(new FakeCampaignRepository()));

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.Empty(vm.Campaigns);
    }

    [Fact]
    public void With_campaigns_the_list_is_shown_not_the_empty_state()
    {
        var repository = new FakeCampaignRepository(new Campaign { Name = "Wandering Souls" });

        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Campaigns);
    }

    [Fact]
    public void Campaigns_are_sorted_by_LastOpenedUtc_descending()
    {
        var older = new Campaign { Name = "Shadows Over Blackmoor", LastOpenedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newest = new Campaign { Name = "Wandering Souls", LastOpenedUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var middle = new Campaign { Name = "The Rustbelt Job", LastOpenedUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        var repository = new FakeCampaignRepository(older, newest, middle);

        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));

        Assert.Equal(["Wandering Souls", "The Rustbelt Job", "Shadows Over Blackmoor"], vm.Campaigns.Select(c => c.Name));
    }

    [Fact]
    public void PC_and_NPC_counts_come_from_the_campaign_loaded_by_GetAllAsync()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" });
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Borin" });
        campaign.Npcs.Add(new Npc { CampaignId = campaign.Id, Name = "The Innkeeper" });
        var repository = new FakeCampaignRepository(campaign);

        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));

        Assert.Equal(2, vm.Campaigns[0].PlayerCharacterCount);
        Assert.Equal(1, vm.Campaigns[0].NpcCount);
    }

    [Fact]
    public void ShowCreateFormCommand_switches_to_the_form_in_create_mode()
    {
        var vm = new CampaignsViewModel(new FakeCampaignRepository(), new ActiveCampaignContext(new FakeCampaignRepository()));

        vm.ShowCreateFormCommand.Execute(null);

        Assert.True(vm.IsFormVisible);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.Form.IsEditMode);
    }

    [Fact]
    public async Task Saving_the_form_hides_it_and_reloads_the_list()
    {
        var repository = new FakeCampaignRepository();
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        vm.ShowCreateFormCommand.Execute(null);
        vm.Form.Name = "Wandering Souls";

        await vm.Form.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.Single(vm.Campaigns);
        Assert.Equal("Wandering Souls", vm.Campaigns[0].Name);
    }

    [Fact]
    public void Cancelling_the_form_hides_it_without_changing_the_list()
    {
        var vm = new CampaignsViewModel(new FakeCampaignRepository(), new ActiveCampaignContext(new FakeCampaignRepository()));
        vm.ShowCreateFormCommand.Execute(null);

        vm.Form.CancelCommand.Execute(null);

        Assert.False(vm.IsFormVisible);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Campaigns);
    }

    [Fact]
    public async Task SelectCommand_selects_the_campaign_via_ActiveCampaignContext_and_marks_the_row_active()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var vm = new CampaignsViewModel(repository, activeCampaignContext);
        var item = Assert.Single(vm.Campaigns);
        Assert.False(item.IsActive);

        await vm.SelectCommand.ExecuteAsync(item);

        Assert.Same(campaign, activeCampaignContext.ActiveCampaign);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task Only_the_selected_campaign_is_marked_active()
    {
        var first = new Campaign { Name = "Wandering Souls" };
        var second = new Campaign { Name = "Shadows Over Blackmoor" };
        var repository = new FakeCampaignRepository(first, second);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));

        await vm.SelectCommand.ExecuteAsync(vm.Campaigns.Single(c => c.Campaign.Id == first.Id));

        Assert.True(vm.Campaigns.Single(c => c.Campaign.Id == first.Id).IsActive);
        Assert.False(vm.Campaigns.Single(c => c.Campaign.Id == second.Id).IsActive);
    }

    [Fact]
    public void Subscribing_to_the_real_ActiveCampaignChanged_event_does_not_throw_synchronously_without_a_running_dispatcher()
    {
        // Mirrors ShellViewModelTests' identical test -- proves the subscription path itself
        // doesn't throw before the Dispatcher.UIThread.Post-marshaled work is even queued. See
        // CampaignsViewModel's OnActiveCampaignChanged.
        var repository = new FakeCampaignRepository();
        var activeCampaignContext = new ActiveCampaignContext(repository);
        _ = new CampaignsViewModel(repository, activeCampaignContext);
        var campaign = new Campaign { Name = "Wandering Souls" };

        var exception = Record.Exception(() => activeCampaignContext.SelectCampaignAsync(campaign).GetAwaiter().GetResult());

        Assert.Null(exception);
    }
}