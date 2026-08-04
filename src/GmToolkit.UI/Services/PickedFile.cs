namespace GmToolkit.UI.Services;

/// <summary>The result of <see cref="IFileDialogService.OpenTextFileAsync"/> -- plain CLR data, not
/// any <c>Avalonia.Platform.Storage</c> type, so a view model consuming it stays directly
/// unit-testable with no Avalonia runtime -- see <see cref="IFileDialogService"/>'s remarks.</summary>
/// <param name="FileName">The picked file's own display name (e.g. <c>"my-campaign.json"</c>), for
/// showing in a preview/error message -- not a full path, since Android's Storage Access Framework
/// picker never hands back one at all.</param>
/// <param name="Content">The file's full contents, decoded as UTF-8 text.</param>
public sealed record PickedFile(string FileName, string Content);