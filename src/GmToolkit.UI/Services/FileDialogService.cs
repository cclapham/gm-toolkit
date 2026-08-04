using System.Text;

using Avalonia.Platform.Storage;

namespace GmToolkit.UI.Services;

/// <inheritdoc cref="IFileDialogService" />
public sealed class FileDialogService(ITopLevelProvider topLevelProvider) : IFileDialogService
{
    public bool CanShowDialogs => topLevelProvider.Current?.StorageProvider is not null;

    public async Task<PickedFile?> OpenTextFileAsync(string title, string typeName, IReadOnlyList<string> extensions)
    {
        ArgumentNullException.ThrowIfNull(extensions);

        var storageProvider = topLevelProvider.Current?.StorageProvider;
        if (storageProvider is null || !storageProvider.CanOpen)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(typeName) { Patterns = extensions.Select(ext => $"*.{ext}").ToList() }],
        };

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0)
        {
            return null;
        }

        using var file = files[0];

        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            return new PickedFile(file.Name, content);
        }
        catch (Exception)
        {
            // A file the picker itself already resolved can still fail to open/read (permission
            // revoked between pick and read, removable media unplugged, ...) -- surfaced to the
            // caller as "nothing was picked" rather than propagating, matching this app's existing
            // "a failed background operation shows a friendly message, never an unhandled crash"
            // convention (see GlobalExceptionHandler's remarks).
            return null;
        }
    }

    public Task<bool> SaveTextFileAsync(string title, string suggestedFileName, string extension, string content) =>
        SaveBinaryFileAsync(title, suggestedFileName, extension, Encoding.UTF8.GetBytes(content ?? string.Empty));

    public async Task<bool> SaveBinaryFileAsync(string title, string suggestedFileName, string extension, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var storageProvider = topLevelProvider.Current?.StorageProvider;
        if (storageProvider is null || !storageProvider.CanSave)
        {
            return false;
        }

        var options = new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            DefaultExtension = extension,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }],
        };

        var file = await storageProvider.SaveFilePickerAsync(options);
        if (file is null)
        {
            return false;
        }

        using var _ = file;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            await stream.WriteAsync(content);
            return true;
        }
        catch (Exception)
        {
            // See OpenTextFileAsync's identical catch -- a chosen save location can still fail to
            // write to (disk full, permission revoked, removable media unplugged).
            return false;
        }
    }
}