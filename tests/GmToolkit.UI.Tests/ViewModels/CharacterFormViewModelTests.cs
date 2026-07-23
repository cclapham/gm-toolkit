using GmToolkit.Core.Models;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

public class CharacterFormViewModelTests
{
    [Fact]
    public void BeginCreate_resets_to_empty_fields_and_is_initially_invalid()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());

        form.BeginCreate(Guid.NewGuid());

        Assert.False(form.IsEditMode);
        Assert.Equal(string.Empty, form.CharacterName);
        Assert.Equal(string.Empty, form.PlayerName);
        Assert.Equal(string.Empty, form.Ancestry);
        Assert.Equal(string.Empty, form.Class);
        Assert.Equal(1, form.Level);
        Assert.Equal(string.Empty, form.Notes);
        Assert.Empty(form.StatRows);
        Assert.False(form.CanSave);
        Assert.NotNull(form.NameError);
        Assert.False(form.IsDeleteAvailable);
    }

    [Fact]
    public void Setting_a_valid_name_clears_the_error_and_allows_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());

        form.CharacterName = "Arannis Windrunner";

        Assert.Null(form.NameError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void Setting_a_name_over_the_max_length_sets_a_visible_error_and_blocks_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());

        form.CharacterName = new string('a', PlayerCharacter.NameMaxLength + 1);

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public void Setting_a_whitespace_only_name_sets_a_visible_error_and_blocks_saving()
    {
        // Regression guard: DataAnnotations' [Required] alone does not reject whitespace-only
        // strings, but PlayerCharacter.CharacterName's own setter does -- mirrors
        // CampaignFormViewModelTests' identical regression guard.
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());

        form.CharacterName = "   ";

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public async Task SaveAsync_with_an_invalid_name_does_not_persist_anything()
    {
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(Guid.NewGuid());

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await repository.GetByCampaignAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveAsync_with_valid_data_adds_a_new_character_and_raises_Saved()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.CharacterName = "Arannis Windrunner";
        form.PlayerName = "Sam";
        form.Ancestry = "Elf";
        form.Class = "Ranger";
        form.Level = 3;
        form.Notes = "Has a pet hawk.";
        PlayerCharacter? saved = null;
        form.Saved += character =>
        {
            saved = character;
            return Task.CompletedTask;
        };

        await form.SaveCommand.ExecuteAsync(null);

        var all = await repository.GetByCampaignAsync(campaignId);
        var persisted = Assert.Single(all);
        Assert.Equal("Arannis Windrunner", persisted.CharacterName);
        Assert.Equal("Sam", persisted.PlayerName);
        Assert.Equal("Elf", persisted.Ancestry);
        Assert.Equal("Ranger", persisted.Class);
        Assert.Equal(3, persisted.Level);
        Assert.Equal("Has a pet hawk.", persisted.Notes);
        Assert.Equal(campaignId, persisted.CampaignId);
        Assert.NotNull(saved);
        Assert.Same(persisted, saved);
    }

    [Fact]
    public void BeginEdit_populates_fields_including_stat_rows_from_the_existing_character()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis", Class = "Ranger", Level = 5 };
        character.Stats["STR"] = "16";
        character.Stats["DEX"] = "14";
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(character));

        form.BeginEdit(character);

        Assert.True(form.IsEditMode);
        Assert.True(form.IsDeleteAvailable);
        Assert.Equal("Arannis", form.CharacterName);
        Assert.Equal("Ranger", form.Class);
        Assert.Equal(5, form.Level);
        Assert.True(form.CanSave);
        Assert.Null(form.NameError);
        Assert.Equal(2, form.StatRows.Count);
        Assert.Contains(form.StatRows, row => row.Key == "STR" && row.Value == "16");
        Assert.Contains(form.StatRows, row => row.Key == "DEX" && row.Value == "14");
    }

    [Fact]
    public async Task SaveAsync_in_edit_mode_updates_the_existing_character_via_the_repository()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(character);
        var form = new CharacterFormViewModel(repository);
        form.BeginEdit(character);
        form.CharacterName = "Arannis Windrunner";

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Single(repository.UpdatedPlayerCharacters);
        Assert.Same(character, repository.UpdatedPlayerCharacters[0]);
        Assert.Equal("Arannis Windrunner", character.CharacterName);
    }

    [Fact]
    public async Task Saving_with_stat_rows_produces_a_D_and_D_style_stat_bag()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.CharacterName = "Arannis Windrunner";
        foreach (var (key, value) in new[] { ("STR", "16"), ("DEX", "14"), ("CON", "12"), ("INT", "10"), ("WIS", "13"), ("CHA", "8"), ("HP", "28"), ("AC", "16") })
        {
            form.AddStatRowCommand.Execute(null);
            var row = form.StatRows[^1];
            row.Key = key;
            row.Value = value;
        }

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal(8, persisted.Stats.Count);
        Assert.Equal("16", persisted.Stats["STR"]);
        Assert.Equal("28", persisted.Stats["HP"]);
    }

    [Fact]
    public async Task Saving_with_stat_rows_produces_a_Call_of_Cthulhu_style_stat_bag()
    {
        // Same form, entirely different key set -- proves the form is genuinely system-agnostic
        // (issue #21's acceptance criterion), not implicitly D&D-shaped.
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.CharacterName = "Prof. Armitage";
        foreach (var (key, value) in new[] { ("SAN", "65"), ("Idea", "70"), ("Luck", "50"), ("HP", "11") })
        {
            form.AddStatRowCommand.Execute(null);
            var row = form.StatRows[^1];
            row.Key = key;
            row.Value = value;
        }

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal(4, persisted.Stats.Count);
        Assert.Equal("65", persisted.Stats["SAN"]);
        Assert.Equal("50", persisted.Stats["Luck"]);
    }

    [Fact]
    public void AddStatRowCommand_appends_a_blank_row_that_does_not_block_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";

        form.AddStatRowCommand.Execute(null);

        Assert.Single(form.StatRows);
        Assert.Null(form.StatsError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void A_stat_row_with_a_value_but_no_key_sets_a_visible_error_and_blocks_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);

        form.StatRows[0].Value = "16";

        Assert.NotNull(form.StatsError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public void Duplicate_stat_keys_set_a_visible_error_and_block_saving()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);
        form.AddStatRowCommand.Execute(null);
        form.StatRows[0].Key = "STR";
        form.StatRows[0].Value = "16";

        form.StatRows[1].Key = "STR";
        form.StatRows[1].Value = "18";

        Assert.NotNull(form.StatsError);
        Assert.Contains("STR", form.StatsError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public void Fixing_a_duplicate_stat_key_clears_the_error_and_allows_saving_again()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);
        form.AddStatRowCommand.Execute(null);
        form.StatRows[0].Key = "STR";
        form.StatRows[1].Key = "STR";
        Assert.NotNull(form.StatsError);

        form.StatRows[1].Key = "DEX";

        Assert.Null(form.StatsError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void RemoveStatRowCommand_removes_the_row_and_clears_any_error_it_caused()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);
        form.AddStatRowCommand.Execute(null);
        form.StatRows[0].Key = "STR";
        form.StatRows[1].Key = "STR";
        var duplicateRow = form.StatRows[1];
        Assert.NotNull(form.StatsError);

        form.RemoveStatRowCommand.Execute(duplicateRow);

        Assert.Single(form.StatRows);
        Assert.Null(form.StatsError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public async Task SaveAsync_trims_whitespace_from_stat_keys_and_values()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);
        form.StatRows[0].Key = "  STR  ";
        form.StatRows[0].Value = "  16  ";

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal("16", persisted.Stats["STR"]);
        Assert.False(persisted.Stats.ContainsKey("  STR  "));
    }

    [Fact]
    public void Cancel_with_no_unsaved_changes_raises_Cancelled_immediately()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void Cancel_with_unsaved_changes_shows_the_inline_discard_guard_instead_of_raising_Cancelled()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
        Assert.False(form.IsFieldsVisible);
    }

    [Fact]
    public void Adding_a_filled_stat_row_counts_as_an_unsaved_change()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.AddStatRowCommand.Execute(null);
        form.StatRows[0].Key = "STR";
        form.StatRows[0].Value = "16";
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.True(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void Adding_a_blank_stat_row_and_leaving_it_empty_does_not_count_as_an_unsaved_change()
    {
        // BuildStats() already treats an entirely-blank row as a no-op on save (see the
        // AddStatRowCommand_appends_a_blank_row_that_does_not_block_saving test) -- the discard
        // guard should agree, so "+ Add stat" alone doesn't trigger a false "unsaved changes?"
        // prompt on Cancel. Editing an existing character (rather than BeginCreate, which starts
        // with a blank name) isolates this to the stat row specifically, not a name change.
        var repository = new FakePlayerCharacterRepository();
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var form = new CharacterFormViewModel(repository);
        form.BeginEdit(character);
        form.AddStatRowCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
    }

    [Fact]
    public void ConfirmDiscard_raises_Cancelled_and_hides_the_guard()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
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
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());
        form.CharacterName = "Arannis";
        form.CancelCommand.Execute(null);
        var cancelledRaised = false;
        form.Cancelled += () => cancelledRaised = true;

        form.CancelDiscardCommand.Execute(null);

        Assert.False(cancelledRaised);
        Assert.False(form.IsShowingDiscardConfirmation);
        Assert.Equal("Arannis", form.CharacterName);
    }

    [Fact]
    public void RequestDeleteCommand_is_a_no_op_in_create_mode()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());

        form.RequestDeleteCommand.Execute(null);

        Assert.False(form.IsShowingDeleteConfirmation);
    }

    [Fact]
    public void RequestDeleteCommand_in_edit_mode_shows_the_confirmation_panel_and_ConfirmDeleteCommand_is_disabled()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(character));
        form.BeginEdit(character);

        form.RequestDeleteCommand.Execute(null);

        Assert.True(form.IsShowingDeleteConfirmation);
        Assert.False(form.CanConfirmDelete);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Typing_the_wrong_name_does_not_enable_ConfirmDeleteCommand_and_does_not_delete()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(character);
        var form = new CharacterFormViewModel(repository);
        form.BeginEdit(character);
        form.RequestDeleteCommand.Execute(null);

        form.DeleteConfirmationInput = "arannis"; // wrong case -- must not match

        Assert.False(form.CanConfirmDelete);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));

        // Even if somehow invoked directly (bypassing the disabled button), it must be a no-op --
        // mirrors CampaignsViewModelTests' identical bypass-resistance test, since
        // IAsyncRelayCommand.ExecuteAsync does not itself call CanExecute.
        await form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.NotEmpty(await repository.GetByCampaignAsync(character.CampaignId));
        Assert.True(form.IsShowingDeleteConfirmation);
    }

    [Fact]
    public void Typing_the_exact_character_name_enables_ConfirmDeleteCommand()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository(character));
        form.BeginEdit(character);
        form.RequestDeleteCommand.Execute(null);

        form.DeleteConfirmationInput = "Arannis";

        Assert.True(form.CanConfirmDelete);
        Assert.True(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task Confirming_delete_removes_the_character_and_raises_Deleted()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(character);
        var form = new CharacterFormViewModel(repository);
        form.BeginEdit(character);
        form.RequestDeleteCommand.Execute(null);
        form.DeleteConfirmationInput = "Arannis";
        var deletedRaised = false;
        form.Deleted += () =>
        {
            deletedRaised = true;
            return Task.CompletedTask;
        };

        await form.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.True(deletedRaised);
        Assert.False(form.IsShowingDeleteConfirmation);
        Assert.Null(await repository.GetAsync(character.Id));
    }

    [Fact]
    public void CancelDeleteCommand_hides_the_confirmation_panel_without_deleting_anything()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Arannis" };
        var repository = new FakePlayerCharacterRepository(character);
        var form = new CharacterFormViewModel(repository);
        form.BeginEdit(character);
        form.RequestDeleteCommand.Execute(null);
        form.DeleteConfirmationInput = "Arannis";

        form.CancelDeleteCommand.Execute(null);

        Assert.False(form.IsShowingDeleteConfirmation);
        Assert.False(form.ConfirmDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_cleared_level_falls_back_to_1_rather_than_blocking_save()
    {
        var campaignId = Guid.NewGuid();
        var repository = new FakePlayerCharacterRepository();
        var form = new CharacterFormViewModel(repository);
        form.BeginCreate(campaignId);
        form.CharacterName = "Arannis";
        form.Level = null;
        Assert.True(form.CanSave);

        await form.SaveCommand.ExecuteAsync(null);

        var persisted = Assert.Single(await repository.GetByCampaignAsync(campaignId));
        Assert.Equal(1, persisted.Level);
    }
}