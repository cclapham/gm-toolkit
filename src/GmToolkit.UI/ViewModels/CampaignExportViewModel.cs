using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Export;
using GmToolkit.Core.Import;
using GmToolkit.Core.Repositories;
using GmToolkit.UI.Design;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The in-place "Export Campaign" panel (issue #131): format picker (JSON full-fidelity / CSV
/// characters-only) → file save dialog. A single instance is reused by
/// <see cref="CampaignsViewModel"/>, the same "one shared, reset-before-showing view model" idiom as
/// <see cref="CampaignFormViewModel"/>/<see cref="CampaignImportViewModel"/> -- see
/// <see cref="CampaignImportViewModel"/>'s remarks on why this whole flow lives in-place, never a
/// separate dialog window.
/// </summary>
public sealed partial class CampaignExportViewModel : ObservableObject
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly IFileDialogService _fileDialogService;

    private Guid _campaignId;

    public CampaignExportViewModel(ICampaignRepository campaignRepository, IFileDialogService fileDialogService)
    {
        _campaignRepository = campaignRepository;
        _fileDialogService = fileDialogService;
    }

    /// <summary>Design-time-only constructor for the XAML previewer -- mirrors every other view
    /// model's identical parameterless constructor. Never used at runtime.</summary>
    public CampaignExportViewModel()
        : this(new DesignTimeCampaignRepository(), new DesignTimeFileDialogService())
    {
    }

    public IReadOnlyList<CampaignExportFormat> AvailableFormats { get; } = [CampaignExportFormat.Json, CampaignExportFormat.Csv];

    [ObservableProperty]
    public partial CampaignExportFormat SelectedFormat { get; set; } = CampaignExportFormat.Json;

    [ObservableProperty]
    public partial string CampaignName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Set if <see cref="ExportAsync"/> fails; <c>null</c> otherwise -- mirrors
    /// <see cref="CampaignImportViewModel.ImportError"/>.</summary>
    [ObservableProperty]
    public partial string? ExportError { get; set; }

    /// <summary>Raised after a successful export, with the format actually written -- <see cref="CampaignsViewModel"/>
    /// uses this to show a success toast and close this panel.</summary>
    public event Action<CampaignExportFormat>? Completed;

    /// <summary>Raised when the user cancels out of this panel.</summary>
    public event Action? Cancelled;

    /// <summary>Resets this panel for exporting <paramref name="campaignId"/> -- call before
    /// showing it, mirrors <see cref="CampaignImportViewModel.Begin"/>.</summary>
    public void Begin(Guid campaignId, string campaignName)
    {
        _campaignId = campaignId;
        CampaignName = campaignName;
        SelectedFormat = CampaignExportFormat.Json;
        IsBusy = false;
        ExportError = null;
    }

    [RelayCommand]
    private void SelectFormat(CampaignExportFormat format) => SelectedFormat = format;

    [RelayCommand]
    private async Task ExportAsync()
    {
        ExportError = null;
        IsBusy = true;

        try
        {
            var dto = await _campaignRepository.ExportCampaignAsync(_campaignId);
            if (dto is null)
            {
                ExportError = "This campaign no longer exists.";
                return;
            }

            var fileNameStem = FileNameSanitizer.Sanitize(dto.Name);

            var saved = SelectedFormat switch
            {
                CampaignExportFormat.Json => await _fileDialogService.SaveTextFileAsync(
                    "Export Campaign", $"{fileNameStem}.json", "json",
                    JsonSerializer.Serialize(dto, CampaignExportJsonContext.Default.CampaignExportDto)),
                CampaignExportFormat.Csv => await _fileDialogService.SaveTextFileAsync(
                    "Export Campaign (characters, CSV)", $"{fileNameStem}-characters.csv", "csv",
                    CampaignCsvExporter.Export(dto)),
                _ => throw new ArgumentOutOfRangeException(nameof(SelectedFormat), SelectedFormat, "Unknown export format."),
            };

            if (!saved)
            {
                // Cancelled the OS save dialog, or the write itself failed -- IFileDialogService
                // already swallows the specific reason (see its own remarks), so there's nothing
                // more specific to say than "it didn't happen"; not treated as an error the user
                // needs to dismiss, since cancelling the save dialog is the overwhelmingly common
                // case here.
                return;
            }

            Completed?.Invoke(SelectedFormat);
        }
        catch (Exception ex)
        {
            // A real repository failure (issue #32) -- mirrors CampaignImportViewModel.ConfirmImportAsync's
            // identical catch.
            ExportError = $"Couldn't export this campaign: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}