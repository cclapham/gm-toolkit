using GmToolkit.Core.Models;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

public class CampaignFormViewModelTests
{
    [Fact]
    public void BeginCreate_resets_to_empty_fields_and_is_initially_invalid()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository());

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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
        form.BeginCreate();

        form.Name = "Wandering Souls";

        Assert.Null(form.NameError);
        Assert.True(form.CanSave);
    }

    [Fact]
    public void Setting_a_name_over_200_characters_sets_a_visible_error_and_blocks_saving()
    {
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
        form.BeginCreate();

        form.Name = "   ";

        Assert.NotNull(form.NameError);
        Assert.False(form.CanSave);
    }

    [Fact]
    public async Task SaveAsync_with_an_invalid_name_does_not_persist_anything()
    {
        var repository = new FakeCampaignRepository();
        var form = new CampaignFormViewModel(repository);
        form.BeginCreate();

        await form.SaveCommand.ExecuteAsync(null);

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task SaveAsync_with_valid_data_adds_a_new_campaign_and_raises_Saved()
    {
        var repository = new FakeCampaignRepository();
        var form = new CampaignFormViewModel(repository);
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
    public void BeginEdit_populates_fields_from_the_existing_campaign_and_is_initially_valid()
    {
        var campaign = new Campaign { Name = "Shadows Over Blackmoor", GameSystem = "D&D 5e", Description = "A classic dungeon crawl." };
        var form = new CampaignFormViewModel(new FakeCampaignRepository(campaign));

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
        var form = new CampaignFormViewModel(repository);
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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
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
        var form = new CampaignFormViewModel(new FakeCampaignRepository());
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
}