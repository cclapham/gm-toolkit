using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// <see cref="FakePlayerCharacterRepository"/>/<see cref="FakeCampaignRepository"/> complete every
/// call synchronously, so <see cref="CharactersViewModel"/>'s fire-and-forget initial load (kicked
/// off from its constructor) has always finished loading by the time the constructor returns --
/// mirrors <see cref="CampaignsViewModelTests"/>' identical remark.
/// </remarks>
public class CharactersViewModelTests
{
    private static ActiveCampaignContext ActiveContextFor(Campaign campaign, FakeCampaignRepository? repository = null)
    {
        var context = new ActiveCampaignContext(repository ?? new FakeCampaignRepository(campaign));
        context.SelectCampaignAsync(campaign).GetAwaiter().GetResult();
        return context;
    }

    [Fact]
    public void With_no_characters_the_empty_state_is_shown()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = new CharactersViewModel(new FakePlayerCharacterRepository(), ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.Empty(vm.Characters);
    }

    [Fact]
    public void With_characters_the_roster_is_shown_not_the_empty_state()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var vm = new CharactersViewModel(new FakePlayerCharacterRepository(pc), ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Characters);
    }

    [Fact]
    public void Only_characters_belonging_to_the_active_campaign_are_shown()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var otherCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var inCampaign = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var inOtherCampaign = new PlayerCharacter { CampaignId = otherCampaign.Id, CharacterName = "Borin" };
        var repository = new FakePlayerCharacterRepository(inCampaign, inOtherCampaign);

        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        var item = Assert.Single(vm.Characters);
        Assert.Equal("Arannis", item.CharacterName);
    }

    [Fact]
    public void Characters_are_sorted_by_name_ascending_case_insensitively()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var zed = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "zed" };
        var arannis = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var borin = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "borin" };
        var repository = new FakePlayerCharacterRepository(zed, arannis, borin);

        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        Assert.Equal(["Arannis", "borin", "zed"], vm.Characters.Select(c => c.CharacterName));
    }

    [Fact]
    public void No_active_campaign_shows_the_empty_state_without_erroring()
    {
        var repository = new FakePlayerCharacterRepository();
        var context = new ActiveCampaignContext(new FakeCampaignRepository());

        var vm = new CharactersViewModel(repository, context);

        Assert.False(vm.IsLoading);
        Assert.False(vm.HasLoadError);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void A_failed_load_surfaces_an_error_instead_of_leaving_IsLoading_stuck_true()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakePlayerCharacterRepository { ThrowOnGetByCampaign = new InvalidOperationException("database is locked") };

        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        Assert.False(vm.IsLoading);
        Assert.True(vm.HasLoadError);
        Assert.Contains("database is locked", vm.LoadError);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
    }

    [Fact]
    public async Task RetryLoadCommand_after_a_failed_load_recovers_once_the_repository_succeeds()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(pc) { ThrowOnGetByCampaign = new InvalidOperationException("database is locked") };
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        Assert.True(vm.HasLoadError);

        repository.ThrowOnGetByCampaign = null;
        await vm.RetryLoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasLoadError);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Characters);
    }

    [Fact]
    public void ShowCreateFormCommand_switches_to_the_form_in_create_mode_for_the_active_campaign()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = new CharactersViewModel(new FakePlayerCharacterRepository(), ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));

        vm.ShowCreateFormCommand.Execute(null);

        Assert.True(vm.IsFormVisible);
        Assert.False(vm.Form.IsEditMode);
    }

    [Fact]
    public void SelectCommand_opens_the_form_in_edit_mode_pre_populated()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis", PlayerName = "Sam", Class = "Ranger", Level = 5 };
        var vm = new CharactersViewModel(new FakePlayerCharacterRepository(pc), ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        var item = Assert.Single(vm.Characters);

        vm.SelectCommand.Execute(item);

        Assert.True(vm.IsFormVisible);
        Assert.True(vm.Form.IsEditMode);
        Assert.Equal("Arannis", vm.Form.CharacterName);
        Assert.Equal("Sam", vm.Form.PlayerName);
        Assert.Equal("Ranger", vm.Form.Class);
        Assert.Equal(5, vm.Form.Level);
    }

    [Fact]
    public async Task Saving_the_form_hides_it_and_reloads_the_roster()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakePlayerCharacterRepository();
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        vm.ShowCreateFormCommand.Execute(null);
        vm.Form.CharacterName = "Arannis";

        await vm.Form.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.Single(vm.Characters);
        Assert.Equal("Arannis", vm.Characters[0].CharacterName);
    }

    [Fact]
    public void Cancelling_the_form_hides_it_without_changing_the_roster()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = new CharactersViewModel(new FakePlayerCharacterRepository(), ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        vm.ShowCreateFormCommand.Execute(null);

        vm.Form.CancelCommand.Execute(null);

        Assert.False(vm.IsFormVisible);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Deleting_from_the_form_hides_it_and_reloads_the_roster()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(pc);
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        var item = Assert.Single(vm.Characters);
        vm.SelectCommand.Execute(item);
        vm.Form.RequestDeleteCommand.Execute(null);
        vm.Form.DeleteConfirmationInput = "Arannis";

        await vm.Form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.Empty(vm.Characters);
    }

    [Fact]
    public async Task Switching_the_active_campaign_reloads_for_the_new_campaign_and_closes_the_form()
    {
        var firstCampaign = new Campaign { Name = "Wandering Souls" };
        var secondCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var firstPc = new PlayerCharacter { CampaignId = firstCampaign.Id, CharacterName = "Arannis" };
        var secondPc = new PlayerCharacter { CampaignId = secondCampaign.Id, CharacterName = "Borin" };
        var repository = new FakePlayerCharacterRepository(firstPc, secondPc);
        var campaignRepository = new FakeCampaignRepository(firstCampaign, secondCampaign);
        var context = ActiveContextFor(firstCampaign, campaignRepository);
        var vm = new CharactersViewModel(repository, context);
        vm.ShowCreateFormCommand.Execute(null);
        Assert.True(vm.IsFormVisible);

        await context.SelectCampaignAsync(secondCampaign);
        // ActiveCampaignChanged is marshaled via Dispatcher.UIThread.Post, which has no running
        // message loop in a plain xUnit test -- call the underlying handler directly instead
        // (mirrors ShellViewModelTests' identical pattern; see HandleActiveCampaignChanged's doc
        // comment / the InternalsVisibleTo in AssemblyInfo.cs).
        vm.HandleActiveCampaignChanged();

        Assert.False(vm.IsFormVisible);
        var item = Assert.Single(vm.Characters);
        Assert.Equal("Borin", item.CharacterName);
    }

    [Fact]
    public void Clearing_the_active_campaign_leaves_the_roster_empty()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(pc);
        var context = ActiveContextFor(campaign, new FakeCampaignRepository(campaign));
        var vm = new CharactersViewModel(repository, context);
        Assert.Single(vm.Characters);

        context.Clear();
        vm.HandleActiveCampaignChanged();

        Assert.Empty(vm.Characters);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void Subscribing_to_the_real_ActiveCampaignChanged_event_does_not_throw_synchronously_without_a_running_dispatcher()
    {
        // Mirrors CampaignsViewModelTests'/ShellViewModelTests' identical test -- proves the
        // subscription path itself doesn't throw before the Dispatcher.UIThread.Post-marshaled
        // work is even queued.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var campaignRepository = new FakeCampaignRepository(campaign);
        var context = new ActiveCampaignContext(campaignRepository);
        _ = new CharactersViewModel(new FakePlayerCharacterRepository(), context);

        var exception = Record.Exception(() => context.SelectCampaignAsync(campaign).GetAwaiter().GetResult());

        Assert.Null(exception);
    }

    [Fact]
    public async Task RefreshAsync_picks_up_a_character_added_directly_to_the_repository_bypassing_the_form()
    {
        // Mirrors issue #68's staleness class of bug: any save flow that persists straight through
        // IPlayerCharacterRepository without going through this view model's own Form should still
        // show up once RefreshAsync (see IRefreshable) is called.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakePlayerCharacterRepository();
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        Assert.True(vm.IsEmpty);

        await repository.AddAsync(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" });
        await vm.RefreshAsync();

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.Equal("Arannis", vm.Characters.Single().CharacterName);
    }

    // -- Skeptical review of PR #80: navigate-triggered RefreshAsync must not flicker the loading state --

    [Fact]
    public async Task RefreshAsync_does_not_toggle_IsLoading_true_even_while_the_refresh_is_genuinely_in_flight()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(pc);
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        Assert.False(vm.IsLoading);

        var isLoadingValuesSeen = new List<bool>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsLoading))
            {
                isLoadingValuesSeen.Add(vm.IsLoading);
            }
        };

        repository.GetByCampaignGate = new TaskCompletionSource();
        var refreshTask = vm.RefreshAsync();

        // Genuinely in flight -- the repository call hasn't been released yet -- and IsLoading is
        // still false, not just "already back to false by the time we checked".
        Assert.False(vm.IsLoading);

        repository.GetByCampaignGate.SetResult();
        await refreshTask;

        Assert.False(vm.IsLoading);
        Assert.DoesNotContain(true, isLoadingValuesSeen);
    }

    [Fact]
    public async Task RetryLoadCommand_still_shows_IsLoading_true_while_a_reload_is_genuinely_in_flight()
    {
        // Control for the test above: an explicit retry (as opposed to a navigate-triggered
        // RefreshAsync) must still show the loading state -- this isn't a case of IsLoading never
        // toggling true at all, only RefreshAsync deliberately suppressing it.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var pc = new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(pc);
        var vm = new CharactersViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign)));
        Assert.False(vm.IsLoading);

        repository.GetByCampaignGate = new TaskCompletionSource();
        var retryTask = vm.RetryLoadCommand.ExecuteAsync(null);

        Assert.True(vm.IsLoading);

        repository.GetByCampaignGate.SetResult();
        await retryTask;

        Assert.False(vm.IsLoading);
    }
}