namespace GmToolkit.Core.Import;

/// <summary>
/// The outcome of a bulk, per-item import — <c>GmToolkit.Data.Repositories.PlayerCharacterRepository.ImportCharactersAsync</c>/
/// <c>NpcRepository.ImportCharactersAsync</c> — into an already-existing campaign. Unlike
/// <see cref="CampaignImportResult"/>'s all-or-nothing whole-campaign import, this is best-effort per
/// entry: an invalid or (when not overwriting) conflicting entry is skipped and reported in
/// <see cref="Errors"/> without preventing the rest of the batch from importing.
/// </summary>
/// <param name="Imported">Every entity that was actually created or overwritten, in input order.</param>
/// <param name="Errors">Every entry that was skipped, and why — see <see cref="ImportItemError"/>.</param>
public sealed record BulkImportResult<T>(IReadOnlyList<T> Imported, IReadOnlyList<ImportItemError> Errors)
{
    /// <summary><c>true</c> when every entry in the batch imported successfully.</summary>
    public bool AllSucceeded => Errors.Count == 0;
}