using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <summary>Issues #130 (import)/#131 (export)/#132 (PDF export) coverage for
/// <see cref="CampaignsViewModel"/>'s own commands -- mirrors <see cref="CampaignsViewModelTests"/>'s
/// remark on <see cref="FakeCampaignRepository"/> completing synchronously.</summary>
public class CampaignsViewModelImportExportTests
{
    private static CampaignsViewModel CreateViewModel(
        out FakeFileDialogService fileDialogService, out NotificationService notificationService, params Campaign[] campaigns)
    {
        var repository = new FakeCampaignRepository(campaigns);
        fileDialogService = new FakeFileDialogService();
        notificationService = new NotificationService();
        return new CampaignsViewModel(
            repository, new ActiveCampaignContext(repository), CharacterSystemRegistry.FromEmbeddedSystems(),
            new FakePlayerCharacterRepository(), new FakeNpcRepository(), fileDialogService, notificationService);
    }

    [Fact]
    public void ShowImportCommand_shows_the_import_wizard_reset_to_its_first_step()
    {
        var vm = CreateViewModel(out _, out _);

        vm.ShowImportCommand.Execute(null);

        Assert.True(vm.IsImportVisible);
        Assert.False(vm.IsListVisible);
        Assert.True(vm.Import.IsPickingFile);
    }

    [Fact]
    public async Task Completing_import_hides_the_wizard_reloads_the_list_and_shows_a_toast()
    {
        var vm = CreateViewModel(out var fileDialogService, out var notificationService);
        vm.ShowImportCommand.Execute(null);

        var dto = new Core.Import.CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new Core.Import.PlayerCharacterExportDto { CharacterName = "Brannigan" }],
        };
        fileDialogService.FileToOpen = new PickedFile(
            "export.json",
            System.Text.Json.JsonSerializer.Serialize(dto, Core.Import.CampaignExportJsonContext.Default.CampaignExportDto));

        await vm.Import.PickFileCommand.ExecuteAsync(null);
        await vm.Import.ConfirmImportCommand.ExecuteAsync(null);

        Assert.False(vm.IsImportVisible);
        Assert.Single(vm.Campaigns);
        var toast = Assert.Single(notificationService.Toasts);
        Assert.Contains("Imported 1 character", toast.Message);
        Assert.Contains("Wandering Souls", toast.Message);
    }

    [Fact]
    public void ShowExportCommand_shows_the_export_panel_for_the_chosen_campaign()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(out _, out _, campaign);
        var item = Assert.Single(vm.Campaigns);

        vm.ShowExportCommand.Execute(item);

        Assert.True(vm.IsExportVisible);
        Assert.False(vm.IsListVisible);
        Assert.Equal("Wandering Souls", vm.Export.CampaignName);
    }

    [Fact]
    public async Task Completing_export_hides_the_panel_and_shows_a_toast()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(out var fileDialogService, out var notificationService, campaign);
        var item = Assert.Single(vm.Campaigns);
        vm.ShowExportCommand.Execute(item);

        await vm.Export.ExportCommand.ExecuteAsync(null);

        Assert.False(vm.IsExportVisible);
        var toast = Assert.Single(notificationService.Toasts);
        Assert.Contains("Wandering Souls", toast.Message);
        Assert.Single(fileDialogService.SavedFiles);
    }

    [Fact]
    public async Task ExportCampaignSummaryToPdfCommand_saves_a_pdf_and_shows_a_success_toast()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(out var fileDialogService, out var notificationService, campaign);
        var item = Assert.Single(vm.Campaigns);

        await vm.ExportCampaignSummaryToPdfCommand.ExecuteAsync(item);

        var saved = Assert.Single(fileDialogService.SavedFiles);
        Assert.Equal("pdf", saved.Extension);
        var toast = Assert.Single(notificationService.Toasts);
        Assert.Equal(ToastSeverity.Info, toast.Severity);
        Assert.Contains("Wandering Souls", toast.Message);
    }

    [Fact]
    public async Task ExportCampaignSummaryToPdfCommand_with_a_cancelled_save_dialog_shows_no_toast()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(out var fileDialogService, out var notificationService, campaign);
        fileDialogService.SaveShouldSucceed = false;
        var item = Assert.Single(vm.Campaigns);

        await vm.ExportCampaignSummaryToPdfCommand.ExecuteAsync(item);

        Assert.Empty(notificationService.Toasts);
    }
}