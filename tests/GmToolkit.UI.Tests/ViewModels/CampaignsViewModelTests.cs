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
    public void A_failed_initial_load_surfaces_an_error_instead_of_leaving_IsLoading_stuck_true()
    {
        var repository = new FakeCampaignRepository { ThrowOnGetAll = new InvalidOperationException("database is locked") };

        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));

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
        var repository = new FakeCampaignRepository(campaign) { ThrowOnGetAll = new InvalidOperationException("database is locked") };
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        Assert.True(vm.HasLoadError);

        repository.ThrowOnGetAll = null;
        await vm.RetryLoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasLoadError);
        Assert.Null(vm.LoadError);
        Assert.True(vm.IsListVisible);
        Assert.Single(vm.Campaigns);
    }

    [Fact]
    public void DeleteConfirmationPrompt_names_the_campaign_and_states_its_PC_and_NPC_counts()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Arannis" });
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Borin" });
        campaign.Npcs.Add(new Npc { CampaignId = campaign.Id, Name = "The Innkeeper" });
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);

        Assert.Contains("Wandering Souls", item.DeleteConfirmationPrompt);
        Assert.Contains("2 PCs", item.DeleteConfirmationPrompt);
        Assert.Contains("1 NPC", item.DeleteConfirmationPrompt);
    }

    [Fact]
    public void RequestEditCommand_opens_the_form_in_edit_mode_prefilled_with_the_campaigns_data_without_activating_it()
    {
        var campaign = new Campaign { Name = "Wandering Souls", GameSystem = "D&D 5e", Description = "A long-running homebrew campaign." };
        var repository = new FakeCampaignRepository(campaign);
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var vm = new CampaignsViewModel(repository, activeCampaignContext);
        var item = Assert.Single(vm.Campaigns);

        vm.RequestEditCommand.Execute(item);

        Assert.True(vm.IsFormVisible);
        Assert.True(vm.Form.IsEditMode);
        Assert.Equal("Wandering Souls", vm.Form.Name);
        Assert.Equal("D&D 5e", vm.Form.GameSystem);
        Assert.Equal("A long-running homebrew campaign.", vm.Form.Description);
        Assert.Null(activeCampaignContext.ActiveCampaign);
        Assert.False(item.IsActive);
    }

    [Fact]
    public async Task Saving_an_edit_updates_the_existing_campaign_rather_than_creating_a_new_one()
    {
        var campaign = new Campaign { Name = "Wandering Souls", GameSystem = "D&D 5e" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.RequestEditCommand.Execute(item);

        vm.Form.Name = "Wandering Souls Renamed";
        await vm.Form.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.IsFormVisible);
        var updated = Assert.Single(vm.Campaigns);
        Assert.Equal(campaign.Id, updated.Campaign.Id);
        Assert.Equal("Wandering Souls Renamed", updated.Name);
    }

    [Fact]
    public void RequestDeleteCommand_shows_the_confirmation_panel_for_the_requested_row_and_ConfirmDeleteCommand_is_disabled()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);

        vm.RequestDeleteCommand.Execute(item);

        Assert.True(item.IsShowingDeleteConfirmation);
        Assert.False(vm.CanConfirmDelete);
        Assert.False(vm.ConfirmDeleteCommand.CanExecute(null));
        Assert.Single(vm.Campaigns);
    }

    [Fact]
    public async Task Typing_the_wrong_name_does_not_enable_ConfirmDeleteCommand_and_does_not_delete()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.RequestDeleteCommand.Execute(item);

        vm.DeleteConfirmationInput = "wandering souls"; // wrong case -- must not match

        Assert.False(vm.CanConfirmDelete);
        Assert.False(vm.ConfirmDeleteCommand.CanExecute(null));

        // Even if somehow invoked directly (bypassing the disabled button), it must be a no-op.
        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Single(vm.Campaigns);
        Assert.True(item.IsShowingDeleteConfirmation);
    }

    [Fact]
    public void Typing_the_exact_campaign_name_enables_ConfirmDeleteCommand()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.RequestDeleteCommand.Execute(item);

        vm.DeleteConfirmationInput = "Wandering Souls";

        Assert.True(vm.CanConfirmDelete);
        Assert.True(vm.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirming_delete_removes_the_campaign_from_the_list_and_calls_the_repository()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.RequestDeleteCommand.Execute(item);
        vm.DeleteConfirmationInput = "Wandering Souls";

        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Empty(vm.Campaigns);
        Assert.Null(await repository.GetAsync(campaign.Id));
    }

    [Fact]
    public async Task Deleting_the_active_campaign_clears_it_via_ActiveCampaignContext()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var vm = new CampaignsViewModel(repository, activeCampaignContext);
        var item = Assert.Single(vm.Campaigns);
        await vm.SelectCommand.ExecuteAsync(item);
        Assert.NotNull(activeCampaignContext.ActiveCampaign);

        vm.RequestDeleteCommand.Execute(item);
        vm.DeleteConfirmationInput = "Wandering Souls";
        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Null(activeCampaignContext.ActiveCampaign);
    }

    [Fact]
    public async Task Deleting_a_non_active_campaign_leaves_the_active_campaign_untouched()
    {
        var active = new Campaign { Name = "Wandering Souls" };
        var other = new Campaign { Name = "Shadows Over Blackmoor" };
        var repository = new FakeCampaignRepository(active, other);
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var vm = new CampaignsViewModel(repository, activeCampaignContext);
        await vm.SelectCommand.ExecuteAsync(vm.Campaigns.Single(c => c.Campaign.Id == active.Id));
        var otherItem = vm.Campaigns.Single(c => c.Campaign.Id == other.Id);

        vm.RequestDeleteCommand.Execute(otherItem);
        vm.DeleteConfirmationInput = "Shadows Over Blackmoor";
        await vm.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.NotNull(activeCampaignContext.ActiveCampaign);
        Assert.Equal(active.Id, activeCampaignContext.ActiveCampaign!.Id);
    }

    [Fact]
    public void CancelDeleteCommand_hides_the_confirmation_panel_without_deleting_anything()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.RequestDeleteCommand.Execute(item);
        vm.DeleteConfirmationInput = "Wandering Souls";

        vm.CancelDeleteCommand.Execute(null);

        Assert.False(item.IsShowingDeleteConfirmation);
        Assert.False(vm.ConfirmDeleteCommand.CanExecute(null));
        Assert.Single(vm.Campaigns);
    }

    [Fact]
    public void RequestDeleteCommand_on_a_different_row_closes_the_previous_rows_confirmation()
    {
        var first = new Campaign { Name = "Wandering Souls" };
        var second = new Campaign { Name = "Shadows Over Blackmoor" };
        var repository = new FakeCampaignRepository(first, second);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var firstItem = vm.Campaigns.Single(c => c.Campaign.Id == first.Id);
        var secondItem = vm.Campaigns.Single(c => c.Campaign.Id == second.Id);
        vm.RequestDeleteCommand.Execute(firstItem);
        vm.DeleteConfirmationInput = "Wandering Souls";
        Assert.True(vm.CanConfirmDelete);

        vm.RequestDeleteCommand.Execute(secondItem);

        Assert.False(firstItem.IsShowingDeleteConfirmation);
        Assert.True(secondItem.IsShowingDeleteConfirmation);
        Assert.False(vm.CanConfirmDelete);
        Assert.Equal(string.Empty, vm.DeleteConfirmationInput);
    }

    [Fact]
    public void ToggleExpandedCommand_expands_a_collapsed_row_without_activating_it()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var activeCampaignContext = new ActiveCampaignContext(repository);
        var vm = new CampaignsViewModel(repository, activeCampaignContext);
        var item = Assert.Single(vm.Campaigns);
        Assert.False(item.IsExpanded);

        vm.ToggleExpandedCommand.Execute(item);

        Assert.True(item.IsExpanded);
        Assert.Null(activeCampaignContext.ActiveCampaign);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void ToggleExpandedCommand_collapses_an_already_expanded_row()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);
        vm.ToggleExpandedCommand.Execute(item);
        Assert.True(item.IsExpanded);

        vm.ToggleExpandedCommand.Execute(item);

        Assert.False(item.IsExpanded);
    }

    [Fact]
    public void Each_rows_expanded_state_is_independent()
    {
        var first = new Campaign { Name = "Wandering Souls" };
        var second = new Campaign { Name = "Shadows Over Blackmoor" };
        var repository = new FakeCampaignRepository(first, second);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var firstItem = vm.Campaigns.Single(c => c.Campaign.Id == first.Id);
        var secondItem = vm.Campaigns.Single(c => c.Campaign.Id == second.Id);

        vm.ToggleExpandedCommand.Execute(firstItem);

        Assert.True(firstItem.IsExpanded);
        Assert.False(secondItem.IsExpanded);
    }

    [Fact]
    public void DescriptionOrPlaceholder_falls_back_to_a_placeholder_when_the_campaign_has_no_description()
    {
        var campaign = new Campaign { Name = "Wandering Souls", Description = string.Empty };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);

        Assert.Equal("No description", item.DescriptionOrPlaceholder);
    }

    [Fact]
    public void DescriptionOrPlaceholder_shows_the_campaigns_description_when_set()
    {
        var campaign = new Campaign { Name = "Wandering Souls", Description = "A long-running homebrew campaign." };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);

        Assert.Equal("A long-running homebrew campaign.", item.DescriptionOrPlaceholder);
    }

    [Fact]
    public void CreatedUtc_is_exposed_from_the_underlying_campaign()
    {
        var createdUtc = new DateTime(2023, 5, 17, 0, 0, 0, DateTimeKind.Utc);
        var campaign = new Campaign { Name = "Wandering Souls", CreatedUtc = createdUtc };
        var repository = new FakeCampaignRepository(campaign);
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        var item = Assert.Single(vm.Campaigns);

        Assert.Equal(createdUtc, item.CreatedUtc);
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

    [Fact]
    public async Task RefreshAsync_picks_up_a_campaign_added_directly_to_the_repository()
    {
        // Mirrors issue #68's staleness class of bug -- see IRefreshable's remarks.
        var repository = new FakeCampaignRepository();
        var vm = new CampaignsViewModel(repository, new ActiveCampaignContext(repository));
        Assert.True(vm.IsEmpty);

        await repository.AddAsync(new Campaign { Name = "Wandering Souls" });
        await vm.RefreshAsync();

        Assert.False(vm.IsEmpty);
        Assert.True(vm.IsListVisible);
        Assert.Equal("Wandering Souls", vm.Campaigns.Single().Name);
    }
}