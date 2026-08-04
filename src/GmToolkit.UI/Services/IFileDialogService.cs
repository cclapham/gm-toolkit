namespace GmToolkit.UI.Services;

/// <summary>
/// Cross-platform file open/save dialogs (issues #130-#132) built on Avalonia's
/// <c>StorageProvider</c> (native OS file pickers on desktop, Android's Storage Access Framework
/// picker on Android) -- not a hand-rolled <see cref="Avalonia.Controls.Window"/>, which this app
/// can't use for anything Android-reachable (see <c>CampaignsViewModel</c>'s remarks on Android's
/// single-view lifetime having no popup-<c>Window</c> equivalent). Returns plain CLR data
/// (<see cref="PickedFile"/>/<see cref="bool"/>), never any <c>Avalonia.Platform.Storage</c> type,
/// so every view model that depends on this interface stays directly unit-testable with a fake
/// (<c>tests/GmToolkit.UI.Tests/Fakes</c>'s existing convention), with no Avalonia visual-tree/
/// dispatcher runtime needed.
/// </summary>
public interface IFileDialogService
{
    /// <summary>Whether a file dialog can actually be shown right now -- <c>false</c> before
    /// <see cref="Views.ShellView"/> has attached to the visual tree, or in any environment with no
    /// storage provider at all (a plain xUnit test/the XAML previewer). Callers check this before
    /// offering an import/export action that would otherwise silently no-op.</summary>
    bool CanShowDialogs { get; }

    /// <summary>
    /// Shows a native "open file" dialog restricted to <paramref name="extensions"/> (each a bare
    /// extension with no leading dot, e.g. <c>"json"</c>), reads the chosen file's full contents as
    /// UTF-8 text, and returns it plus the file's own display name -- or <c>null</c> if the user
    /// cancelled, <see cref="CanShowDialogs"/> was <c>false</c>, or reading the file failed.
    /// </summary>
    Task<PickedFile?> OpenTextFileAsync(string title, string typeName, IReadOnlyList<string> extensions);

    /// <summary>
    /// Shows a native "save file" dialog defaulting to <paramref name="suggestedFileName"/>, writes
    /// <paramref name="content"/> to the chosen location as UTF-8 text, and returns <c>true</c> --
    /// or <c>false</c> if the user cancelled, <see cref="CanShowDialogs"/> was <c>false</c>, or
    /// writing failed.
    /// </summary>
    Task<bool> SaveTextFileAsync(string title, string suggestedFileName, string extension, string content);

    /// <summary>Binary counterpart of <see cref="SaveTextFileAsync"/> -- used for PDF export
    /// (issue #132), whose bytes are never valid UTF-8 text.</summary>
    Task<bool> SaveBinaryFileAsync(string title, string suggestedFileName, string extension, byte[] content);
}