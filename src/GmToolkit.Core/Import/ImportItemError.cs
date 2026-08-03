namespace GmToolkit.Core.Import;

/// <summary>
/// One rejected entry from a <see cref="BulkImportResult{T}"/> — <see cref="Index"/> is its position
/// in the DTO list that was passed in, so a caller can point a GM back at exactly which entry in
/// their import file failed.
/// </summary>
/// <param name="Index">Zero-based position in the list of DTOs that was imported.</param>
/// <param name="Name">The entry's own name (<c>CharacterName</c>/<c>Name</c>), for a readable message even without the index.</param>
/// <param name="Errors">Why this entry wasn't imported.</param>
public sealed record ImportItemError(int Index, string Name, IReadOnlyList<string> Errors);