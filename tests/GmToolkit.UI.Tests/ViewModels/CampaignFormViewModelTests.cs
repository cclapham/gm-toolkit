using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

public class CampaignFormViewModelTests
{
    [Fact]
    public void BeginCreate_resets_to_empty_fields_and_is_initially_invalid()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());

        form.BeginCreate();

        Assert.False(form.IsEditMode);
        Assert.Equal(string.Empty, form.Name);
        Assert.Equal(string.Empty, form.GameSystem);
        Assert.Equal(string.Empty, form.Description);
        Assert.False(form.CanSave);
        Assert.NotNull(form.NameError);
    }

    [Fact]
    public void Setting_a_valid_name_clears_the_error_and_allows_saving()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();

        form.Name = "Wandering Souls";

        Assert.Null(form.NameError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void Setting_a_name_over_200_characters_sets_a_visible_error_and_blocks_saving()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();

        form.Name = new string('a', Campaign.NameMaxLength + 1);

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public void Setting_a_whitespace_only_name_sets_a_visible_error_and_blocks_saving()
    {
        // Regression guard: DataAnnotations' [Required] alone does not reject whitespace-only
        // strings, but Campaign.Name's own setter does (see ValidateName's reuse of the domain
        // rule via Campaign's setter, in CampaignFormViewModel's remarks).
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();

        form.Name = "   ";

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public async Task SaveAsync_with_an_invalid_name_does_not_persist_anything()
    {
        var repository = new FakeCampaignRepository();
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task SaveAsync_with_valid_data_adds_a_new_campaign_and_raises_Saved()
    {
        var repository = new FakeCampaignRepository();
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";
        form.GameSystem = "Blades in the Dark";
        form.Description = "A crew of scoundrels in Duskvol.";
        Campaign? saved = null;
        form.Saved += campaign =>
        {
            saved = campaign;
            return Task.CompletedTask;
        };

        await form.SaveCommand.ExecuteAsync(null);

        var all = await repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Wandering Souls", all[0].Name);
        Assert.Equal("Blades in the Dark", all[0].GameSystem);
        Assert.Equal("A crew of scoundrels in Duskvol.", all[0].Description);
        Assert.NotNull(saved);
        Assert.Equal("Wandering Souls", saved!.Name);
    }

    [Fact]
    public async Task SaveAsync_when_the_repository_throws_a_DataAccessException_shows_its_friendly_message_instead_of_crashing()
    {
        // Issue #32's acceptance criterion, at the view-model level: a repository failure (e.g. a
        // real DataAccessException from the database file disappearing mid-session) must not
        // propagate out of this [RelayCommand]-generated async command uncaught -- it should be
        // caught and surfaced as SaveError, the same friendly inline text CampaignsViewModel.LoadAsync/
        // ConfirmDeleteAsync already show for their own repository failures.
        var repository = new FakeCampaignRepository { ThrowOnAdd = new DataAccessException("The database file is missing.") };
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";

        var exception = await Record.ExceptionAsync(() => form.SaveCommand.ExecuteAsync(null));

        Assert.Null(exception);
        Assert.NotNull(form.SaveError);
        Assert.Contains("The database file is missing.", form.SaveError);
    }

    [Fact]
    public void BeginEdit_populates_fields_from_the_existing_campaign_and_is_initially_valid()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", GameSystem = "D&D 5e", Description = "A classic dungeon crawl." };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), CharacterSystemRegistry.FromEmbeddedSystems());

        form.BeginEdit(campaign);

        Assert.True(form.IsEditMode);
        Assert.Equal("Shadows Over Blackmoor", form.Name);
        Assert.Equal("D&D 5e", form.GameSystem);
        Assert.Equal("A classic dungeon crawl.", form.Description);
        Assert.True(form.CanSave);
        Assert.Null(form.NameError);
    }

    [Fact]
    public async Task SaveAsync_in_edit_mode_updates_the_existing_campaign_via_the_repository()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var repository = new FakeCampaignRepository(campaign);
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);
        form.Name = "Shadows Over Blackmoor II";

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.UpdatedCampaigns);
        Assert.Same(campaign, repository.UpdatedCampaigns[0]);
        Assert.Equal("Shadows Over Blackmoor II", campaign.Name);
    }

    [Fact]
    public void Cancel_with_no_unsaved_changes_raises_Cancelled_immediately()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void Cancel_with_unsaved_changes_shows_the_inline_discard_guard_instead_of_raising_Cancelled()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
        Assert.False(form.IsFieldsVisible);
    }

    [Fact]
    public void ConfirmDiscard_raises_Cancelled_and_hides_the_guard()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";
        form.CancelCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.ConfirmDiscardCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void CancelDiscard_hides_the_guard_without_raising_Cancelled_and_keeps_the_edits()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";
        form.CancelCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelDiscardCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
        Assert.Equal("Wandering Souls", form.Name);
    }

    // -- Campaign system selector UI --

    [Fact]
    public void CharacterSystemOptions_starts_with_Freeform_followed_by_every_registered_system_alphabetically()
    {
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), registry);

        Assert.Equal(CharacterSystemOption.Freeform, form.CharacterSystemOptions[0]);
        var expectedRest = registry.GetAll()
            .OrderBy(system => system.Name, StringComparer.OrdinalIgnoreCase)
            .Select(system => system.Id);
        Assert.Equal(expectedRest, form.CharacterSystemOptions.Skip(1).Select(option => option.Id));
    }

    [Fact]
    public void BeginCreate_defaults_the_system_selector_to_Freeform()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository(), CharacterSystemRegistry.FromEmbeddedSystems());

        form.BeginCreate();

        Assert.Equal(CharacterSystemOption.Freeform, form.SelectedCharacterSystem);
        Assert.Null(form.MissingSystemWarning);
    }

    [Fact]
    public async Task SaveAsync_with_a_selected_system_persists_its_id_on_the_new_campaign()
    {
        var repository = new FakeCampaignRepository();
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();
        var form = new CampaignFormViewModel(repository, registry);
        form.BeginCreate();
        form.Name = "Wandering Souls";
        form.SelectedCharacterSystem = form.CharacterSystemOptions.Single(option => option.Id == "dnd5e-2024");

        await form.SaveCommand.ExecuteAsync(null);

        var all = await repository.GetAllAsync();
        Assert.Equal("dnd5e-2024", Assert.Single(all).CharacterSystemId);
    }

    [Fact]
    public async Task SaveAsync_with_Freeform_selected_persists_a_null_CharacterSystemId()
    {
        var repository = new FakeCampaignRepository();
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginCreate();
        form.Name = "Wandering Souls";
        // Freeform is already the default (see BeginCreate_defaults_the_system_selector_to_Freeform),
        // asserted explicitly here so this test doesn't silently pass if that default ever changes.
        Assert.Equal(CharacterSystemOption.Freeform, form.SelectedCharacterSystem);

        await form.SaveCommand.ExecuteAsync(null);

        var all = await repository.GetAllAsync();
        Assert.Null(Assert.Single(all).CharacterSystemId);
    }

    [Fact]
    public void BeginEdit_selects_the_campaigns_attached_system()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "dnd5e-2024" };
        var registry = CharacterSystemRegistry.FromEmbeddedSystems();
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), registry);

        form.BeginEdit(campaign);

        Assert.Equal("dnd5e-2024", form.SelectedCharacterSystem.Id);
        Assert.Equal(registry.GetById("dnd5e-2024").Name, form.SelectedCharacterSystem.Name);
        Assert.Null(form.MissingSystemWarning);
    }

    [Fact]
    public void BeginEdit_of_a_Freeform_campaign_selects_Freeform()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), CharacterSystemRegistry.FromEmbeddedSystems());

        form.BeginEdit(campaign);

        Assert.Equal(CharacterSystemOption.Freeform, form.SelectedCharacterSystem);
        Assert.Null(form.MissingSystemWarning);
    }

    [Fact]
    public async Task SaveAsync_in_edit_mode_persists_a_changed_system_selection()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "dnd5e-2024" };
        var repository = new FakeCampaignRepository(campaign);
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);

        form.SelectedCharacterSystem = form.CharacterSystemOptions.Single(option => option.Id == "gurps-4e");
        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("gurps-4e", campaign.CharacterSystemId);
        Assert.Same(campaign, Assert.Single(repository.UpdatedCampaigns));
    }

    [Fact]
    public async Task SaveAsync_in_edit_mode_can_switch_a_campaign_from_a_system_back_to_Freeform()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "dnd5e-2024" };
        var repository = new FakeCampaignRepository(campaign);
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);

        form.SelectedCharacterSystem = CharacterSystemOption.Freeform;
        await form.SaveCommand.ExecuteAsync(null);

        Assert.Null(campaign.CharacterSystemId);
    }

    [Fact]
    public void BeginEdit_of_a_campaign_whose_system_is_no_longer_installed_shows_a_placeholder_and_a_warning()
    {
        // Simulates a system pack having been removed since this campaign attached it (the
        // registry no longer has "some-removed-system") -- acceptance criterion: display gracefully
        // rather than throwing (ICharacterSystemRegistry.GetById would throw; TryGetById is used
        // instead, see CampaignFormViewModel.ResolveCharacterSystemOption).
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "some-removed-system" };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), CharacterSystemRegistry.FromEmbeddedSystems());

        var exception = Record.Exception(() => form.BeginEdit(campaign));

        Assert.Null(exception);
        Assert.Equal("some-removed-system", form.SelectedCharacterSystem.Id);
        Assert.NotNull(form.MissingSystemWarning);
        Assert.Contains("some-removed-system", form.MissingSystemWarning);
        Assert.Contains(form.SelectedCharacterSystem, form.CharacterSystemOptions);
    }

    [Fact]
    public async Task SaveAsync_of_a_campaign_with_a_missing_system_left_unchanged_keeps_its_original_id()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "some-removed-system" };
        var repository = new FakeCampaignRepository(campaign);
        var form = new CampaignFormViewModel(repository, CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Equal("some-removed-system", campaign.CharacterSystemId);
    }

    [Fact]
    public void BeginCreate_after_editing_a_campaign_with_a_missing_system_removes_the_stale_placeholder()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", CharacterSystemId = "some-removed-system" };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);
        Assert.Contains(form.CharacterSystemOptions, option => option.Id == "some-removed-system");

        form.BeginCreate();

        Assert.DoesNotContain(form.CharacterSystemOptions, option => option.Id == "some-removed-system");
        Assert.Null(form.MissingSystemWarning);
    }

    [Fact]
    public void Changing_only_the_system_selection_counts_as_an_unsaved_change()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor" };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign), CharacterSystemRegistry.FromEmbeddedSystems());
        form.BeginEdit(campaign);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.SelectedCharacterSystem = form.CharacterSystemOptions.Single(option => option.Id == "dnd5e-2024");
        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
    }
}