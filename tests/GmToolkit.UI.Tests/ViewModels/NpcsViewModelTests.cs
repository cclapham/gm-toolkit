using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>
/// <see cref="FakeNpcRepository"/>/<see cref="FakeCampaignRepository"/> complete every call
/// synchronously, so <see cref="NpcsViewModel"/>'s fire-and-forget initial load (kicked off from
/// its constructor) has always finished loading by the time the constructor returns -- mirrors
/// <see cref="CharactersViewModelTests"/>' identical remark.
/// </remarks>
public class NpcsViewModelTests
{
    private static ActiveCampaignContext ActiveContextFor(Campaign campaign, FakeCampaignRepository? repository = null)
    {
        var context = new ActiveCampaignContext(repository ?? new FakeCampaignRepository(campaign));
        context.SelectCampaignAsync(campaign).GetAwaiter().GetResult();
        return context;
    }

    private static Npc MakeNpc(
        Guid campaignId,
        string name,
        string role = "",
        string faction = "",
        string location = "",
        string notes = "",
        bool knownToPlayers = false,
        DateTime? createdUtc = null) =>
        new()
        {
            CampaignId = campaignId,
            Name = name,
            Role = role,
            Faction = faction,
            Location = location,
            Notes = notes,
            KnownToPlayers = knownToPlayers,
            CreatedUtc = createdUtc ?? DateTime.UtcNow,
        };

    [Fact]
    public void With_no_npcs_the_empty_state_is_shown()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = new NpcsViewModel(new FakeNpcRepository(), ActiveContextFor(campaign));

        Assert.False(vm.IsLoading);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.IsSearchBarVisible);
        Assert.Empty(vm.Npcs);
    }

    [Fact]
    public void With_npcs_the_list_is_shown_not_the_empty_state()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.True(vm.IsSearchBarVisible);
        Assert.Single(vm.Npcs);
    }

    [Fact]
    public void Only_npcs_belonging_to_the_active_campaign_are_shown()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var otherCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var inCampaign = MakeNpc(campaign.Id, "Baelor the Butcher");
        var inOtherCampaign = MakeNpc(otherCampaign.Id, "Zoric the Pale");
        var repository = new FakeNpcRepository(inCampaign, inOtherCampaign);

        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign, new FakeCampaignRepository(campaign, otherCampaign)));

        var item = Assert.Single(vm.Npcs);
        Assert.Equal("Baelor the Butcher", item.Name);
    }

    [Fact]
    public void A_failed_load_surfaces_an_error_instead_of_leaving_IsLoading_stuck_true()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeNpcRepository { ThrowOnGetByCampaign = new InvalidOperationException("database is locked") };

        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));

        Assert.False(vm.IsLoading);
        Assert.True(vm.HasLoadError);
        Assert.Contains("database is locked", vm.LoadError);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.IsSearchBarVisible);
    }

    [Fact]
    public async Task RetryLoadCommand_after_a_failed_load_recovers_once_the_repository_succeeds()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var repository = new FakeNpcRepository(npc) { ThrowOnGetByCampaign = new InvalidOperationException("database is locked") };
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        Assert.True(vm.HasLoadError);

        repository.ThrowOnGetByCampaign = null;
        await vm.RetryLoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasLoadError);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Npcs);
    }

    [Fact]
    public void No_active_campaign_shows_the_empty_state_without_erroring()
    {
        var repository = new FakeNpcRepository();
        var context = new ActiveCampaignContext(new FakeCampaignRepository());

        var vm = new NpcsViewModel(repository, context);

        Assert.False(vm.IsLoading);
        Assert.False(vm.HasLoadError);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public void Search_text_matches_across_name_role_faction_location_and_notes()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var byName = MakeNpc(campaign.Id, "Baelor the Butcher");
        var byRole = MakeNpc(campaign.Id, "Second NPC", role: "Blacksmith");
        var byFaction = MakeNpc(campaign.Id, "Third NPC", faction: "The Iron Concord");
        var byLocation = MakeNpc(campaign.Id, "Fourth NPC", location: "Blackmoor Docks");
        var byNotes = MakeNpc(campaign.Id, "Fifth NPC", notes: "Owes a blood debt to the Concord.");
        var noMatch = MakeNpc(campaign.Id, "Sixth NPC", role: "Farmer", faction: "None", location: "Elsewhere", notes: "Nothing relevant.");
        var repository = new FakeNpcRepository(byName, byRole, byFaction, byLocation, byNotes, noMatch);
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));

        vm.SearchText = "blood";
        Assert.Equal(["Fifth NPC"], vm.Npcs.Select(n => n.Name));

        vm.SearchText = "Concord";
        Assert.Equal(["Fifth NPC", "Third NPC"], vm.Npcs.Select(n => n.Name).OrderBy(n => n));

        vm.SearchText = "Blacksmith";
        Assert.Equal(["Second NPC"], vm.Npcs.Select(n => n.Name));

        vm.SearchText = "Docks";
        Assert.Equal(["Fourth NPC"], vm.Npcs.Select(n => n.Name));

        vm.SearchText = "Baelor";
        Assert.Equal(["Baelor the Butcher"], vm.Npcs.Select(n => n.Name));

        vm.SearchText = string.Empty;
        Assert.Equal(6, vm.Npcs.Count);
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));

        vm.SearchText = "BAELOR";

        Assert.Single(vm.Npcs);
    }

    [Fact]
    public void Faction_filter_narrows_to_npcs_in_that_faction_and_All_shows_everyone()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var concord = MakeNpc(campaign.Id, "Baelor", faction: "The Iron Concord");
        var cult = MakeNpc(campaign.Id, "Zoric", faction: "The Whispering Cult");
        var noFaction = MakeNpc(campaign.Id, "Unaffiliated Bob");
        var vm = new NpcsViewModel(new FakeNpcRepository(concord, cult, noFaction), ActiveContextFor(campaign));

        Assert.Equal(["All", "The Iron Concord", "The Whispering Cult"], vm.FactionOptions);

        vm.SelectedFaction = "The Iron Concord";
        Assert.Equal(["Baelor"], vm.Npcs.Select(n => n.Name));

        vm.SelectedFaction = NpcsViewModel.AllOption;
        Assert.Equal(3, vm.Npcs.Count);
    }

    [Fact]
    public void Location_filter_narrows_to_npcs_in_that_location_and_All_shows_everyone()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var docks = MakeNpc(campaign.Id, "Baelor", location: "Blackmoor Docks");
        var market = MakeNpc(campaign.Id, "Zoric", location: "Blackmoor Market");
        var vm = new NpcsViewModel(new FakeNpcRepository(docks, market), ActiveContextFor(campaign));

        Assert.Equal(["All", "Blackmoor Docks", "Blackmoor Market"], vm.LocationOptions);

        vm.SelectedLocation = "Blackmoor Market";
        Assert.Equal(["Zoric"], vm.Npcs.Select(n => n.Name));

        vm.SelectedLocation = NpcsViewModel.AllOption;
        Assert.Equal(2, vm.Npcs.Count);
    }

    [Fact]
    public void Search_and_filters_combine_rather_than_replace_each_other()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var match = MakeNpc(campaign.Id, "Baelor the Butcher", faction: "The Iron Concord", location: "Blackmoor Docks");
        var wrongFaction = MakeNpc(campaign.Id, "Baelor's Cousin", faction: "The Whispering Cult", location: "Blackmoor Docks");
        var wrongLocation = MakeNpc(campaign.Id, "Baelor's Apprentice", faction: "The Iron Concord", location: "Blackmoor Market");
        var vm = new NpcsViewModel(new FakeNpcRepository(match, wrongFaction, wrongLocation), ActiveContextFor(campaign));

        vm.SearchText = "Baelor";
        vm.SelectedFaction = "The Iron Concord";
        vm.SelectedLocation = "Blackmoor Docks";

        Assert.Equal(["Baelor the Butcher"], vm.Npcs.Select(n => n.Name));
    }

    [Fact]
    public void HasActiveFilters_reflects_search_and_faction_and_location_but_not_sort_order()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor", faction: "The Iron Concord", location: "Blackmoor Docks");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));
        Assert.False(vm.HasActiveFilters);

        vm.SortByRecentlyAddedCommand.Execute(null);
        Assert.False(vm.HasActiveFilters);

        vm.SearchText = "Bael";
        Assert.True(vm.HasActiveFilters);
        vm.SearchText = string.Empty;
        Assert.False(vm.HasActiveFilters);

        // Whitespace-only search matches nothing extra in ApplyFilter (it trims before
        // matching), so it shouldn't count as an active filter either -- otherwise "Clear
        // filters" would appear with nothing for it to actually clear.
        vm.SearchText = "   ";
        Assert.False(vm.HasActiveFilters);
        vm.SearchText = string.Empty;

        vm.SelectedFaction = "The Iron Concord";
        Assert.True(vm.HasActiveFilters);
        vm.SelectedFaction = NpcsViewModel.AllOption;
        Assert.False(vm.HasActiveFilters);

        vm.SelectedLocation = "Blackmoor Docks";
        Assert.True(vm.HasActiveFilters);
    }

    [Fact]
    public void ClearFiltersCommand_resets_search_and_both_dropdowns_but_leaves_sort_order_alone()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var match = MakeNpc(campaign.Id, "Baelor the Butcher", faction: "The Iron Concord", location: "Blackmoor Docks");
        var other = MakeNpc(campaign.Id, "Zoric the Pale", faction: "The Whispering Cult", location: "Blackmoor Market");
        var vm = new NpcsViewModel(new FakeNpcRepository(match, other), ActiveContextFor(campaign));
        vm.SearchText = "Baelor";
        vm.SelectedFaction = "The Iron Concord";
        vm.SelectedLocation = "Blackmoor Docks";
        vm.SortByRecentlyAddedCommand.Execute(null);
        Assert.Single(vm.Npcs);

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(NpcsViewModel.AllOption, vm.SelectedFaction);
        Assert.Equal(NpcsViewModel.AllOption, vm.SelectedLocation);
        Assert.False(vm.HasActiveFilters);
        Assert.Equal(2, vm.Npcs.Count);
        Assert.True(vm.IsSortedByRecentlyAdded);
    }

    [Fact]
    public void No_matches_for_the_current_search_is_distinct_from_the_empty_campaign_state()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));

        vm.SearchText = "no such npc exists";

        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.True(vm.IsNoSearchResults);
        Assert.Empty(vm.Npcs);
    }

    [Fact]
    public void Default_sort_is_by_name_ascending_case_insensitively()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var zed = MakeNpc(campaign.Id, "zed");
        var arannis = MakeNpc(campaign.Id, "Arannis");
        var borin = MakeNpc(campaign.Id, "borin");
        var vm = new NpcsViewModel(new FakeNpcRepository(zed, arannis, borin), ActiveContextFor(campaign));

        Assert.True(vm.IsSortedByName);
        Assert.Equal(["Arannis", "borin", "zed"], vm.Npcs.Select(n => n.Name));
    }

    [Fact]
    public void SortByRecentlyAddedCommand_sorts_by_CreatedUtc_descending()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var oldest = MakeNpc(campaign.Id, "Oldest", createdUtc: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var middle = MakeNpc(campaign.Id, "Middle", createdUtc: new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newest = MakeNpc(campaign.Id, "Newest", createdUtc: new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var vm = new NpcsViewModel(new FakeNpcRepository(middle, oldest, newest), ActiveContextFor(campaign));

        vm.SortByRecentlyAddedCommand.Execute(null);

        Assert.True(vm.IsSortedByRecentlyAdded);
        Assert.False(vm.IsSortedByName);
        Assert.Equal(["Newest", "Middle", "Oldest"], vm.Npcs.Select(n => n.Name));

        vm.SortByNameCommand.Execute(null);

        Assert.True(vm.IsSortedByName);
        Assert.Equal(["Middle", "Newest", "Oldest"], vm.Npcs.Select(n => n.Name));
    }

    [Fact]
    public void Known_to_players_marker_reflects_each_npc()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var known = MakeNpc(campaign.Id, "Known NPC", knownToPlayers: true);
        var unknown = MakeNpc(campaign.Id, "Unknown NPC", knownToPlayers: false);
        var vm = new NpcsViewModel(new FakeNpcRepository(known, unknown), ActiveContextFor(campaign));

        var knownItem = vm.Npcs.Single(n => n.Name == "Known NPC");
        var unknownItem = vm.Npcs.Single(n => n.Name == "Unknown NPC");

        Assert.True(knownItem.KnownToPlayers);
        Assert.False(unknownItem.KnownToPlayers);
    }

    [Fact]
    public async Task Switching_the_active_campaign_reloads_for_the_new_campaign()
    {
        var firstCampaign = new Campaign { Name = "Wandering Souls" };
        var secondCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var firstNpc = MakeNpc(firstCampaign.Id, "Baelor the Butcher");
        var secondNpc = MakeNpc(secondCampaign.Id, "Zoric the Pale");
        var repository = new FakeNpcRepository(firstNpc, secondNpc);
        var campaignRepository = new FakeCampaignRepository(firstCampaign, secondCampaign);
        var context = ActiveContextFor(firstCampaign, campaignRepository);
        var vm = new NpcsViewModel(repository, context);
        Assert.Single(vm.Npcs);

        await context.SelectCampaignAsync(secondCampaign);
        // ActiveCampaignChanged is marshaled via Dispatcher.UIThread.Post, which has no running
        // message loop in a plain xUnit test -- call the underlying handler directly instead
        // (mirrors CharactersViewModelTests'/ShellViewModelTests' identical pattern; see
        // HandleActiveCampaignChanged's doc comment / the InternalsVisibleTo in AssemblyInfo.cs).
        vm.HandleActiveCampaignChanged();

        var item = Assert.Single(vm.Npcs);
        Assert.Equal("Zoric the Pale", item.Name);
    }

    [Fact]
    public void Clearing_the_active_campaign_leaves_the_list_empty()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var repository = new FakeNpcRepository(npc);
        var context = ActiveContextFor(campaign);
        var vm = new NpcsViewModel(repository, context);
        Assert.Single(vm.Npcs);

        context.Clear();
        vm.HandleActiveCampaignChanged();

        Assert.Empty(vm.Npcs);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task A_faction_selection_that_no_longer_exists_after_switching_campaigns_resets_to_All()
    {
        var firstCampaign = new Campaign { Name = "Wandering Souls" };
        var secondCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var firstNpc = MakeNpc(firstCampaign.Id, "Baelor", faction: "The Iron Concord");
        var secondNpc = MakeNpc(secondCampaign.Id, "Zoric", faction: "The Whispering Cult");
        var repository = new FakeNpcRepository(firstNpc, secondNpc);
        var campaignRepository = new FakeCampaignRepository(firstCampaign, secondCampaign);
        var context = ActiveContextFor(firstCampaign, campaignRepository);
        var vm = new NpcsViewModel(repository, context);
        vm.SelectedFaction = "The Iron Concord";

        await context.SelectCampaignAsync(secondCampaign);
        vm.HandleActiveCampaignChanged();

        Assert.Equal(NpcsViewModel.AllOption, vm.SelectedFaction);
        Assert.Single(vm.Npcs);
    }

    [Fact]
    public void Subscribing_to_the_real_ActiveCampaignChanged_event_does_not_throw_synchronously_without_a_running_dispatcher()
    {
        // Mirrors CharactersViewModelTests'/CampaignsViewModelTests'/ShellViewModelTests' identical
        // test -- proves the subscription path itself doesn't throw before the
        // Dispatcher.UIThread.Post-marshaled work is even queued.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var campaignRepository = new FakeCampaignRepository(campaign);
        var context = new ActiveCampaignContext(campaignRepository);
        _ = new NpcsViewModel(new FakeNpcRepository(), context);

        var exception = Record.Exception(() => context.SelectCampaignAsync(campaign).GetAwaiter().GetResult());

        Assert.Null(exception);
    }

    // -- Issue #25: create/edit/delete folded into the list's three states plus search bar --

    [Fact]
    public void ShowCreateFormCommand_opens_the_form_in_create_mode_and_hides_the_empty_state()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = new NpcsViewModel(new FakeNpcRepository(), ActiveContextFor(campaign));
        Assert.True(vm.IsEmpty);

        vm.ShowCreateFormCommand.Execute(null);

        Assert.True(vm.IsFormVisible);
        Assert.False(vm.Form.IsEditMode);
        Assert.False(vm.IsEmpty);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.IsNoSearchResults);
        Assert.False(vm.IsSearchBarVisible);
    }

    [Fact]
    public void ShowCreateFormCommand_hides_the_populated_list_and_search_bar_too()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));
        Assert.True(vm.IsListVisible);
        Assert.True(vm.IsSearchBarVisible);

        vm.ShowCreateFormCommand.Execute(null);

        Assert.True(vm.IsFormVisible);
        Assert.False(vm.IsListVisible);
        Assert.False(vm.IsSearchBarVisible);
    }

    [Fact]
    public void ShowCreateFormCommand_hides_the_no_search_results_state_too()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));
        vm.SearchText = "no such npc exists";
        Assert.True(vm.IsNoSearchResults);

        vm.ShowCreateFormCommand.Execute(null);

        Assert.True(vm.IsFormVisible);
        Assert.False(vm.IsNoSearchResults);
    }

    [Fact]
    public void SelectCommand_opens_the_form_in_edit_mode_populated_with_the_chosen_npc()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher", role: "Blacksmith");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));
        var item = Assert.Single(vm.Npcs);

        vm.SelectCommand.Execute(item);

        Assert.True(vm.IsFormVisible);
        Assert.True(vm.Form.IsEditMode);
        Assert.Equal("Baelor the Butcher", vm.Form.Name);
        Assert.Equal("Blacksmith", vm.Form.Role);
        Assert.False(vm.IsListVisible);
    }

    [Fact]
    public async Task Saving_a_new_npc_via_the_form_closes_it_and_reloads_the_list()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeNpcRepository();
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        vm.ShowCreateFormCommand.Execute(null);
        vm.Form.Name = "Baelor the Butcher";

        await vm.Form.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Npcs);
        Assert.Equal("Baelor the Butcher", vm.Npcs.Single().Name);
    }

    [Fact]
    public async Task Saving_an_edited_npc_via_the_form_closes_it_and_reflects_the_change_in_the_list()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor");
        var repository = new FakeNpcRepository(npc);
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        var item = Assert.Single(vm.Npcs);
        vm.SelectCommand.Execute(item);
        vm.Form.Name = "Baelor the Butcher";

        await vm.Form.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.Equal("Baelor the Butcher", vm.Npcs.Single().Name);
    }

    [Fact]
    public void Cancelling_the_form_closes_it_without_changing_the_list()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var vm = new NpcsViewModel(new FakeNpcRepository(npc), ActiveContextFor(campaign));
        vm.ShowCreateFormCommand.Execute(null);

        vm.Form.CancelCommand.Execute(null);

        Assert.False(vm.IsFormVisible);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Npcs);
    }

    [Fact]
    public async Task Deleting_the_npc_being_edited_closes_the_form_and_removes_it_from_the_list()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var npc = MakeNpc(campaign.Id, "Baelor the Butcher");
        var repository = new FakeNpcRepository(npc);
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        var item = Assert.Single(vm.Npcs);
        vm.SelectCommand.Execute(item);
        vm.Form.RequestDeleteCommand.Execute(null);
        vm.Form.DeleteConfirmationInput = "Baelor the Butcher";

        await vm.Form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Npcs);
    }

    [Fact]
    public async Task Switching_the_active_campaign_while_the_form_is_open_closes_it()
    {
        var firstCampaign = new Campaign { Name = "Wandering Souls" };
        var secondCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var campaignRepository = new FakeCampaignRepository(firstCampaign, secondCampaign);
        var context = ActiveContextFor(firstCampaign, campaignRepository);
        var vm = new NpcsViewModel(new FakeNpcRepository(), context);
        vm.ShowCreateFormCommand.Execute(null);
        Assert.True(vm.IsFormVisible);

        await context.SelectCampaignAsync(secondCampaign);
        vm.HandleActiveCampaignChanged();

        Assert.False(vm.IsFormVisible);
    }

    [Fact]
    public async Task RefreshAsync_picks_up_an_npc_added_directly_to_the_repository_bypassing_the_form()
    {
        // Mirrors issue #68's actual root cause: GeneratorViewModel.SaveAsync persists straight
        // through INpcRepository.AddAsync, never touching this view model's own Form -- so nothing
        // but a navigate-triggered RefreshAsync (see IRefreshable) should be needed to see it.
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeNpcRepository();
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        Assert.True(vm.IsEmpty);

        await repository.AddAsync(MakeNpc(campaign.Id, "Generated Ghoul"));
        await vm.RefreshAsync();

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.Equal("Generated Ghoul", vm.Npcs.Single().Name);
    }

    [Fact]
    public async Task RefreshAsync_preserves_search_text_and_faction_filter()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var matching = MakeNpc(campaign.Id, "Baelor the Butcher", faction: "The Iron Concord");
        var nonMatching = MakeNpc(campaign.Id, "Zoric the Pale", faction: "The Whispering Cult");
        var repository = new FakeNpcRepository(matching, nonMatching);
        var vm = new NpcsViewModel(repository, ActiveContextFor(campaign));
        vm.SearchText = "Baelor";
        vm.SelectedFaction = "The Iron Concord";

        await repository.AddAsync(MakeNpc(campaign.Id, "Another Generated NPC", faction: "The Iron Concord"));
        await vm.RefreshAsync();

        Assert.Equal("Baelor", vm.SearchText);
        Assert.Equal("The Iron Concord", vm.SelectedFaction);
        Assert.Equal("Baelor the Butcher", vm.Npcs.Single().Name);
    }
}