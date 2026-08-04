namespace GmToolkit.UI.ViewModels;

/// <summary>The two export formats <see cref="CampaignExportViewModel"/> offers (issue #131) --
/// unlike <see cref="CampaignImportFormat"/>, both are fully functional: <see cref="Json"/> is a
/// full-fidelity round trip (<c>GmToolkit.Core.Import.CampaignExportJsonContext</c>) and
/// <see cref="Csv"/> is the flattened, characters-only spreadsheet view
/// (<c>GmToolkit.Core.Export.CampaignCsvExporter</c>).</summary>
public enum CampaignExportFormat
{
    Json,
    Csv,
}