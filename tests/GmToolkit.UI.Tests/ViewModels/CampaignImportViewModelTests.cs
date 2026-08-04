using System.Text.Json;

using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Services;
using GmToolkit.UI.Tests.Fakes;
using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Tests.ViewModels;

/// <remarks>See <see cref="CampaignsViewModelTests"/>'s remarks on <see cref="FakeCampaignRepository"/>
/// completing synchronously -- every fake this suite uses does too.</remarks>
public class CampaignImportViewModelTests
{
    private static string ValidJson(string name = "Wandering Souls", int characters = 1, int npcs = 0) =>
        JsonSerializer.Serialize(
            new CampaignExportDto
            {
                Name = name,
                PlayerCharacters = [.. Enumerable.Range(0, characters).Select(i => new PlayerCharacterExportDto { CharacterName = $"PC {i}" })],
                Npcs = [.. Enumerable.Range(0, npcs).Select(i => new NpcExportDto { Name = $"NPC {i}" })],
            },
            CampaignExportJsonContext.Default.CampaignExportDto);

    private static CampaignImportViewModel CreateViewModel(
        out FakeCampaignRepository campaignRepository, out FakeFileDialogService fileDialogService,
        Campaign[]? existingCampaigns = null)
    {
        campaignRepository = new FakeCampaignRepository(existingCampaigns ?? []);
        fileDialogService = new FakeFileDialogService();
        var vm = new CampaignImportViewModel(
            campaignRepository, new FakePlayerCharacterRepository(), new FakeNpcRepository(),
            CharacterSystemRegistry.FromEmbeddedSystems(), fileDialogService);
        vm.Begin();
        return vm;
    }

    [Fact]
    public void Begin_resets_to_the_file_picker_step()
    {
        var vm = CreateViewModel(out _, out _);

        Assert.True(vm.IsPickingFile);
        Assert.False(vm.IsPreviewStep);
        Assert.Equal(CampaignImportFormat.Json, vm.SelectedFormat);
    }

    [Theory]
    [InlineData(CampaignImportFormat.Json, true)]
    [InlineData(CampaignImportFormat.DndBeyond, false)]
    [InlineData(CampaignImportFormat.Csv, false)]
    public void IsFormatAvailable_only_json_is_selectable(CampaignImportFormat format, bool expected) =>
        Assert.Equal(expected, CampaignImportViewModel.IsFormatAvailable(format));

    [Fact]
    public async Task PickFileAsync_with_a_cancelled_picker_leaves_the_wizard_on_the_file_picker_step()
    {
        var vm = CreateViewModel(out _, out var fileDialogService);
        fileDialogService.FileToOpen = null;

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.IsPickingFile);
        Assert.Null(vm.PickFileError);
    }

    [Fact]
    public async Task PickFileAsync_with_malformed_json_shows_a_friendly_error_and_stays_on_the_file_picker_step()
    {
        var vm = CreateViewModel(out _, out var fileDialogService);
        fileDialogService.FileToOpen = new PickedFile("broken.json", "{ not json");

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.IsPickingFile);
        Assert.NotNull(vm.PickFileError);
        Assert.Contains("broken.json", vm.PickFileError);
    }

    [Fact]
    public async Task PickFileAsync_with_a_validation_error_shows_the_error_list_and_stays_on_the_file_picker_step()
    {
        var vm = CreateViewModel(out _, out var fileDialogService);
        var invalidJson = JsonSerializer.Serialize(
            new CampaignExportDto { Name = string.Empty }, CampaignExportJsonContext.Default.CampaignExportDto);
        fileDialogService.FileToOpen = new PickedFile("invalid.json", invalidJson);

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.IsPickingFile);
        Assert.NotNull(vm.PickFileError);
        Assert.NotEmpty(vm.PickFileErrors);
    }

    [Fact]
    public async Task PickFileAsync_with_a_valid_file_and_no_conflict_moves_to_the_preview_step()
    {
        var vm = CreateViewModel(out _, out var fileDialogService);
        fileDialogService.FileToOpen = new PickedFile("export.json", ValidJson(characters: 2, npcs: 1));

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.False(vm.IsPickingFile);
        Assert.True(vm.IsPreviewStep);
        Assert.Equal("Wandering Souls", vm.CampaignName);
        Assert.Equal(2, vm.CharacterCount);
        Assert.Equal(1, vm.NpcCount);
        Assert.False(vm.HasConflict);
    }

    [Fact]
    public async Task PickFileAsync_detects_a_conflict_with_an_existing_campaign_of_the_same_name()
    {
        var vm = CreateViewModel(out _, out var fileDialogService, [new Campaign { Name = "Wandering Souls" }]);
        fileDialogService.FileToOpen = new PickedFile("export.json", ValidJson());

        await vm.PickFileCommand.ExecuteAsync(null);

        Assert.True(vm.HasConflict);
    }

    [Fact]
    public async Task ConfirmImportAsync_with_no_conflict_creates_the_campaign_and_raises_completed()
    {
        var vm = CreateViewModel(out var campaignRepository, out var fileDialogService);
        fileDialogService.FileToOpen = new PickedFile("export.json", ValidJson(characters: 3));
        await vm.PickFileCommand.ExecuteAsync(null);

        string? completedName = null;
        var completedCharacters = -1;
        vm.Completed += (name, characters, npcs) =>
        {
            completedName = name;
            completedCharacters = characters;
            return Task.CompletedTask;
        };

        await vm.ConfirmImportCommand.ExecuteAsync(null);

        Assert.Equal("Wandering Souls", completedName);
        Assert.Equal(3, completedCharacters);
        Assert.Single(await campaignRepository.GetAllAsync());
    }

    [Fact]
    public async Task ConfirmImportAsync_with_skip_resolution_on_a_conflict_raises_cancelled_and_writes_nothing()
    {
        var existing = new Campaign { Name = "Wandering Souls", Description = "Original" };
        var vm = CreateViewModel(out var campaignRepository, out var fileDialogService, [existing]);
        fileDialogService.FileToOpen = new PickedFile("export.json", ValidJson());
        await vm.PickFileCommand.ExecuteAsync(null);
        vm.SelectedResolution = ImportConflictResolution.Skip;

        var cancelledRaised = false;
        vm.Cancelled += () => cancelledRaised = true;
        var completedRaised = false;
        vm.Completed += (_, _, _) => { completedRaised = true; return Task.CompletedTask; };

        await vm.ConfirmImportCommand.ExecuteAsync(null);

        Assert.True(cancelledRaised);
        Assert.False(completedRaised);
        var only = Assert.Single(await campaignRepository.GetAllAsync());
        Assert.Equal("Original", only.Description);
    }

    [Fact]
    public async Task ConfirmImportAsync_with_overwrite_resolution_on_a_conflict_replaces_the_campaign()
    {
        var existing = new Campaign { Name = "Wandering Souls", Description = "Original" };
        var vm = CreateViewModel(out var campaignRepository, out var fileDialogService, [existing]);
        fileDialogService.FileToOpen = new PickedFile("export.json", ValidJson());
        await vm.PickFileCommand.ExecuteAsync(null);
        vm.SelectedResolution = ImportConflictResolution.Overwrite;

        await vm.ConfirmImportCommand.ExecuteAsync(null);

        var only = Assert.Single(await campaignRepository.GetAllAsync());
        Assert.NotEqual(existing.Id, only.Id);
    }

    [Fact]
    public void Cancel_raises_cancelled()
    {
        var vm = CreateViewModel(out _, out _);

        var cancelledRaised = false;
        vm.Cancelled += () => cancelledRaised = true;

        vm.CancelCommand.Execute(null);

        Assert.True(cancelledRaised);
    }
}