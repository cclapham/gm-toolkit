using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <summary>Issue #132's per-character PDF export coverage for <see cref="CharacterFormViewModel"/>.</summary>
public class CharacterFormViewModelPdfExportTests
{
    private static async Task<(CharacterFormViewModel Form, FakeFileDialogService FileDialogService, NotificationService NotificationService)> CreateEditingFormAsync(
        PlayerCharacter character)
    {
        var campaign = new Campaign { Id = character.CampaignId, Name = "Wandering Souls" };
        var campaignRepository = new FakeCampaignRepository(campaign);
        var activeCampaignContext = new ActiveCampaignContext(campaignRepository);
        await activeCampaignContext.SelectCampaignAsync(campaign);

        var fileDialogService = new FakeFileDialogService();
        var notificationService = new NotificationService();
        var form = new CharacterFormViewModel(
            new FakePlayerCharacterRepository(character), characterSystemRegistry: null, activeCampaignContext, fileDialogService, notificationService);
        form.BeginEdit(character);

        return (form, fileDialogService, notificationService);
    }

    [Fact]
    public void IsExportToPdfAvailable_is_false_in_create_mode()
    {
        var form = new CharacterFormViewModel(new FakePlayerCharacterRepository());
        form.BeginCreate(Guid.NewGuid());

        Assert.False(form.IsExportToPdfAvailable);
    }

    [Fact]
    public async Task ExportToPdfAsync_in_edit_mode_saves_a_pdf_and_shows_a_success_toast()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        var (form, fileDialogService, notificationService) = await CreateEditingFormAsync(character);

        Assert.True(form.IsExportToPdfAvailable);

        await form.ExportToPdfCommand.ExecuteAsync(null);

        var saved = Assert.Single(fileDialogService.SavedFiles);
        Assert.Equal("pdf", saved.Extension);
        Assert.Contains("Brannigan", saved.SuggestedFileName);
        var toast = Assert.Single(notificationService.Toasts);
        Assert.Equal(ToastSeverity.Info, toast.Severity);
        Assert.Contains("Brannigan", toast.Message);
    }

    [Fact]
    public async Task ExportToPdfAsync_with_a_cancelled_save_dialog_shows_no_toast()
    {
        var character = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        var (form, fileDialogService, notificationService) = await CreateEditingFormAsync(character);
        fileDialogService.SaveShouldSucceed = false;

        await form.ExportToPdfCommand.ExecuteAsync(null);

        Assert.Empty(notificationService.Toasts);
    }
}