namespace GmToolkit.UI.ViewModels;

/// <summary>The format choices <see cref="CampaignImportViewModel"/>'s first step offers (issue
/// #130's "format picker: D&amp;D Beyond / JSON / CSV") -- see <see cref="CampaignImportViewModel"/>'s
/// remarks for why only <see cref="Json"/> is actually selectable today.</summary>
public enum CampaignImportFormat
{
    Json,
    DndBeyond,
    Csv,
}