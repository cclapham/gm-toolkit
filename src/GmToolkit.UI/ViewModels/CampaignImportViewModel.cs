using System.Text.Json;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using GmToolkit.Core.Import;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Systems;
using GmToolkit.UI.Design;
using GmToolkit.UI.Services;

namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The in-place "Import Campaign" wizard (issue #130): format picker → file picker → preview →
/// conflict resolution → import. A single instance is reused by <see cref="CampaignsViewModel"/>,
/// the same "one shared, reset-before-showing view model" idiom as <see cref="CampaignFormViewModel"/>
/// (see that class's remarks for why -- Android's single-view lifetime has no popup-<see cref="Avalonia.Controls.Window"/>
/// equivalent, so this whole flow lives in-place inside <c>CampaignsView.axaml</c>, never a separate
/// dialog window).
/// </summary>
/// <remarks>
/// <para>
/// <b>Only <see cref="CampaignImportFormat.Json"/> is actually selectable.</b> This app's own JSON
/// export/import round trip (#129) is the only format that exists anywhere in
/// <c>GmToolkit.Core</c>/<c>GmToolkit.Data</c> today -- there is no D&amp;D Beyond client (that
/// service has no public, documented export API to build against, and MVP.md already lists
/// "Import from PDF, D&amp;D Beyond, or any other tool" as explicitly out of scope for exactly that
/// reason) and no CSV *importer* (only a CSV *exporter*, #131 -- turning an arbitrary spreadsheet's
/// columns back into a <see cref="Import.PlayerCharacterExportDto"/> is a real parsing problem of
/// its own, not something this issue's scope covers). Both still appear in
/// <see cref="AvailableFormats"/> (so the format picker itself matches issue #130's literal ask and
/// the door stays open for a future importer to fill either in without reworking this wizard's
/// shape), but <see cref="IsFormatAvailable"/> keeps them disabled with an explanatory message
/// rather than silently pretending to support them.
/// </para>
/// <para>
/// <b>Conflict resolution reuses <see cref="Import.CampaignImportOrchestrator"/> exactly</b> --
/// this view model's whole job is driving that orchestrator's inputs (the parsed/validated DTO, the
/// GM's chosen <see cref="Import.ImportConflictResolution"/>) from file-picker/preview UI state, not
/// reimplementing any of its logic. See that class's own remarks for what "Overwrite existing" and
/// "Merge" actually do.
/// </para>
/// </remarks>
public sealed partial class CampaignImportViewModel : ObservableObject
{
    private readonly ICampaignRepository _campaignRepository;
    private readonly ICharacterSystemRegistry _characterSystemRegistry;
    private readonly IFileDialogService _fileDialogService;
    private readonly CampaignImportOrchestrator _orchestrator;

    private CampaignExportDto? _parsedDto;

    public CampaignImportViewModel(
        ICampaignRepository campaignRepository,
        IPlayerCharacterRepository playerCharacterRepository,
        INpcRepository npcRepository,
        ICharacterSystemRegistry characterSystemRegistry,
        IFileDialogService fileDialogService)
    {
        _campaignRepository = campaignRepository;
        _characterSystemRegistry = characterSystemRegistry;
        _fileDialogService = fileDialogService;
        _orchestrator = new CampaignImportOrchestrator(campaignRepository, playerCharacterRepository, npcRepository);
    }

    /// <summary>Design-time-only constructor for the XAML previewer -- mirrors every other view
    /// model's identical parameterless constructor. Never used at runtime.</summary>
    public CampaignImportViewModel()
        : this(new DesignTimeCampaignRepository(), new DesignTimePlayerCharacterRepository(), new DesignTimeNpcRepository(), CharacterSystemRegistry.FromEmbeddedSystems(), new DesignTimeFileDialogService())
    {
    }

    /// <summary>Every format the picker shows (issue #130's literal ask) -- see this class's
    /// remarks on why only <see cref="CampaignImportFormat.Json"/> is selectable.</summary>
    public IReadOnlyList<CampaignImportFormat> AvailableFormats { get; } =
    [
        CampaignImportFormat.Json,
        CampaignImportFormat.DndBeyond,
        CampaignImportFormat.Csv,
    ];

    [ObservableProperty]
    public partial CampaignImportFormat SelectedFormat { get; set; } = CampaignImportFormat.Json;

    public static bool IsFormatAvailable(CampaignImportFormat format) => format == CampaignImportFormat.Json;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPreviewStep))]
    public partial bool IsPickingFile { get; set; } = true;

    public bool IsPreviewStep => !IsPickingFile;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Set when <see cref="PickFileCommand"/>'s parse/validate step fails -- shown inline
    /// on the file-picker step, mirroring <c>CampaignFormViewModel.SaveError</c>'s convention.</summary>
    [ObservableProperty]
    public partial string? PickFileError { get; set; }

    /// <summary>Every validation error from a parsed-but-invalid import file (issue #130's "friendly
    /// error dialog with validation errors") -- empty whenever <see cref="PickFileError"/> is
    /// <c>null</c> or the failure wasn't a validation failure (e.g. malformed JSON).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> PickFileErrors { get; set; } = [];

    [ObservableProperty]
    public partial string CampaignName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int CharacterCount { get; set; }

    [ObservableProperty]
    public partial int NpcCount { get; set; }

    /// <summary>Non-blocking warnings from <see cref="ImportValidator"/> (e.g. duplicate names
    /// within the import file itself) -- shown on the preview step underneath the counts.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Warnings { get; set; } = [];

    /// <summary>Whether a campaign already exists in this app with the same name as the parsed
    /// import -- when <c>true</c>, the preview step shows <see cref="ResolutionOptions"/> instead of
    /// going straight to <see cref="ConfirmImportCommand"/>.</summary>
    [ObservableProperty]
    public partial bool HasConflict { get; set; }

    public IReadOnlyList<ImportConflictResolution> ResolutionOptions { get; } =
    [
        ImportConflictResolution.Overwrite,
        ImportConflictResolution.Merge,
        ImportConflictResolution.Skip,
    ];

    [ObservableProperty]
    public partial ImportConflictResolution SelectedResolution { get; set; } = ImportConflictResolution.Overwrite;

    /// <summary>Set if <see cref="ConfirmImportCommand"/>'s repository call throws or the
    /// orchestrator itself reports a failure -- shown inline on the preview step.</summary>
    [ObservableProperty]
    public partial string? ImportError { get; set; }

    /// <summary>Raised after a successful (non-skipped) import, with the campaign name and
    /// character/NPC counts actually imported -- <see cref="CampaignsViewModel"/> uses this to show
    /// the "Imported X characters into [Campaign]" toast (issue #130) and refresh its list. A
    /// <see cref="Func{T1,T2,T3,TResult}"/> returning <see cref="Task"/> (not a plain
    /// <see cref="Action"/>), since the refresh it triggers is itself asynchronous -- mirrors
    /// <see cref="CampaignFormViewModel.Saved"/>'s identical shape/reasoning.</summary>
    public event Func<string, int, int, Task>? Completed;

    /// <summary>Raised when the user cancels out of this wizard, or after a
    /// <see cref="ImportConflictResolution.Skip"/> outcome (nothing was imported, but the flow is
    /// still done) -- <see cref="CampaignsViewModel"/> closes this panel either way.</summary>
    public event Action? Cancelled;

    /// <summary>Resets this wizard to its first step -- call before showing it, mirrors
    /// <see cref="CampaignFormViewModel.BeginCreate"/>'s "reset a shared instance before display"
    /// idiom.</summary>
    public void Begin()
    {
        _parsedDto = null;
        SelectedFormat = CampaignImportFormat.Json;
        IsPickingFile = true;
        IsBusy = false;
        PickFileError = null;
        PickFileErrors = [];
        CampaignName = string.Empty;
        CharacterCount = 0;
        NpcCount = 0;
        Warnings = [];
        HasConflict = false;
        SelectedResolution = ImportConflictResolution.Overwrite;
        ImportError = null;
    }

    [RelayCommand]
    private async Task PickFileAsync()
    {
        if (!IsFormatAvailable(SelectedFormat))
        {
            return;
        }

        PickFileError = null;
        PickFileErrors = [];
        IsBusy = true;

        try
        {
            var picked = await _fileDialogService.OpenTextFileAsync("Import Campaign", "JSON campaign export", ["json"]);
            if (picked is null)
            {
                // Cancelled the OS picker, or no TopLevel/StorageProvider available yet -- not an
                // error worth showing; the user is still on this step and can just try again.
                return;
            }

            CampaignExportDto dto;
            try
            {
                dto = JsonSerializer.Deserialize(picked.Content, CampaignExportJsonContext.Default.CampaignExportDto)
                    ?? throw new JsonException("The file's top-level value was JSON null.");
            }
            catch (JsonException ex)
            {
                PickFileError = $"'{picked.FileName}' isn't a valid GM Toolkit campaign export: {ex.Message}";
                return;
            }

            var validation = ImportValidator.ValidateCampaign(dto, _characterSystemRegistry);
            if (!validation.IsValid)
            {
                PickFileError = $"'{picked.FileName}' has validation errors and can't be imported:";
                PickFileErrors = validation.Errors;
                return;
            }

            _parsedDto = dto;
            CampaignName = dto.Name;
            CharacterCount = dto.PlayerCharacters.Count;
            NpcCount = dto.Npcs.Count;
            Warnings = validation.Warnings;

            var existing = await _campaignRepository.GetAllAsync();
            HasConflict = existing.Any(campaign => string.Equals(campaign.Name, dto.Name, StringComparison.Ordinal));
            SelectedResolution = ImportConflictResolution.Overwrite;

            IsPickingFile = false;
        }
        catch (Exception ex)
        {
            // A real I/O failure reading the picked file (see IFileDialogService.OpenTextFileAsync's
            // remarks -- most of these are already swallowed to null there, but defense in depth
            // matches this app's existing convention elsewhere, e.g. CampaignFormViewModel.SaveAsync).
            PickFileError = $"Couldn't read that file: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void BackToFilePicker()
    {
        IsPickingFile = true;
        ImportError = null;
    }

    [RelayCommand]
    private void SelectResolution(ImportConflictResolution resolution) => SelectedResolution = resolution;

    [RelayCommand]
    private async Task ConfirmImportAsync()
    {
        if (_parsedDto is null)
        {
            return;
        }

        ImportError = null;
        IsBusy = true;

        try
        {
            var resolution = HasConflict ? SelectedResolution : ImportConflictResolution.Overwrite;
            var outcome = await _orchestrator.ImportAsync(_parsedDto, resolution, _characterSystemRegistry);

            if (outcome.WasSkipped)
            {
                Cancelled?.Invoke();
                return;
            }

            if (!outcome.Succeeded)
            {
                ImportError = outcome.Validation.Errors.Count > 0
                    ? string.Join(' ', outcome.Validation.Errors)
                    : "The import didn't succeed.";
                return;
            }

            if (Completed is not null)
            {
                await Completed.Invoke(_parsedDto.Name, outcome.CharactersImported, outcome.NpcsImported);
            }
        }
        catch (Exception ex)
        {
            // A real repository failure (issue #32) -- mirrors CampaignFormViewModel.SaveAsync's
            // identical catch.
            ImportError = $"Couldn't import this campaign: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}