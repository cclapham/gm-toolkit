using System.Text;

using GmToolkit.Core.Models;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

public class CampaignExportViewModelTests
{
    private static CampaignExportViewModel CreateViewModel(
        Campaign campaign, out FakeFileDialogService fileDialogService)
    {
        var campaignRepository = new FakeCampaignRepository(campaign);
        fileDialogService = new FakeFileDialogService();
        var vm = new CampaignExportViewModel(campaignRepository, fileDialogService);
        vm.Begin(campaign.Id, campaign.Name);
        return vm;
    }

    [Fact]
    public async Task ExportAsync_as_json_saves_a_json_file_and_raises_completed()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(campaign, out var fileDialogService);
        vm.SelectedFormat = CampaignExportFormat.Json;

        CampaignExportFormat? completedFormat = null;
        vm.Completed += format => completedFormat = format;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal(CampaignExportFormat.Json, completedFormat);
        var saved = Assert.Single(fileDialogService.SavedFiles);
        Assert.Equal("json", saved.Extension);
        Assert.Contains("Wandering Souls", Encoding.UTF8.GetString(saved.Content));
    }

    [Fact]
    public async Task ExportAsync_as_csv_saves_a_csv_file_with_the_characters_only_shape()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        campaign.PlayerCharacters.Add(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Brannigan" });
        var vm = CreateViewModel(campaign, out var fileDialogService);
        vm.SelectedFormat = CampaignExportFormat.Csv;

        await vm.ExportCommand.ExecuteAsync(null);

        var saved = Assert.Single(fileDialogService.SavedFiles);
        Assert.Equal("csv", saved.Extension);
        var text = Encoding.UTF8.GetString(saved.Content);
        Assert.Contains("Name,Player,Ancestry,Class,Level,Notes", text);
        Assert.Contains("Brannigan", text);
    }

    [Fact]
    public async Task ExportAsync_with_a_cancelled_save_dialog_does_not_raise_completed()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var vm = CreateViewModel(campaign, out var fileDialogService);
        fileDialogService.SaveShouldSucceed = false;

        var completedRaised = false;
        vm.Completed += _ => completedRaised = true;

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.False(completedRaised);
        Assert.Null(vm.ExportError);
    }

    [Fact]
    public void Cancel_raises_cancelled()
    {
        var vm = CreateViewModel(new Campaign { Name = "Wandering Souls" }, out _);

        var cancelledRaised = false;
        vm.Cancelled += () => cancelledRaised = true;

        vm.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
    }
}