using GmToolkit.UI.Services;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="IFileDialogService"/> for testing view models built against issues
/// #130-#132 without a real Avalonia visual tree/<c>TopLevel</c> -- a test configures
/// <see cref="FileToOpen"/>/<see cref="SaveShouldSucceed"/> to control what "the user picked"
/// looks like, then asserts against <see cref="SavedFiles"/> for what was actually written.</summary>
internal sealed class FakeFileDialogService : IFileDialogService
{
    public bool CanShowDialogs { get; set; } = true;

    /// <summary>What <see cref="OpenTextFileAsync"/> returns -- <c>null</c> (the default) simulates
    /// the user cancelling the OS picker.</summary>
    public PickedFile? FileToOpen { get; set; }

    /// <summary>Whether <see cref="SaveTextFileAsync"/>/<see cref="SaveBinaryFileAsync"/> should
    /// report success -- <c>false</c> simulates the user cancelling the OS save dialog (or the
    /// write itself failing; either way <see cref="IFileDialogService"/>'s contract is the same
    /// boolean either way -- see its own remarks).</summary>
    public bool SaveShouldSucceed { get; set; } = true;

    /// <summary>Every "save" call this fake actually recorded, in order -- lets a test assert on
    /// the exact filename/extension/content a view model tried to write.</summary>
    public List<(string Title, string SuggestedFileName, string Extension, byte[] Content)> SavedFiles { get; } = [];

    public Task<PickedFile?> OpenTextFileAsync(string title, string typeName, IReadOnlyList<string> extensions) =>
        Task.FromResult(FileToOpen);

    public Task<bool> SaveTextFileAsync(string title, string suggestedFileName, string extension, string content) =>
        SaveBinaryFileAsync(title, suggestedFileName, extension, System.Text.Encoding.UTF8.GetBytes(content));

    public Task<bool> SaveBinaryFileAsync(string title, string suggestedFileName, string extension, byte[] content)
    {
        if (SaveShouldSucceed)
        {
            SavedFiles.Add((title, suggestedFileName, extension, content));
        }

        return Task.FromResult(SaveShouldSucceed);
    }
}