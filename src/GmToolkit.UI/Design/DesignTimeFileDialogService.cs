using GmToolkit.UI.Services;

namespace GmToolkit.UI.Design;

/// <summary>
/// Always-declines <see cref="IFileDialogService"/> used only to construct view models for the
/// XAML previewer's <c>Design.DataContext</c> -- previewers have no running Avalonia application
/// lifetime, so <see cref="ITopLevelProvider.Current"/> would never be set anyway. Never used at
/// runtime; both real heads resolve <see cref="IFileDialogService"/> from the DI container instead
/// (see <c>ServiceCollectionExtensions.AddGmToolkitUi</c>).
/// </summary>
internal sealed class DesignTimeFileDialogService : IFileDialogService
{
    public bool CanShowDialogs => false;

    public Task<PickedFile?> OpenTextFileAsync(string title, string typeName, IReadOnlyList<string> extensions) =>
        Task.FromResult<PickedFile?>(null);

    public Task<bool> SaveTextFileAsync(string title, string suggestedFileName, string extension, string content) =>
        Task.FromResult(false);

    public Task<bool> SaveBinaryFileAsync(string title, string suggestedFileName, string extension, byte[] content) =>
        Task.FromResult(false);
}