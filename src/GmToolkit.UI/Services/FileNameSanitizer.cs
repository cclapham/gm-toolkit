namespace GmToolkit.UI.Services;

/// <summary>
/// Turns a free-text domain name (a <c>Campaign.Name</c>/<c>PlayerCharacter.CharacterName</c>, both
/// system-agnostic free text with no filename-safety constraint of their own) into a legal suggested
/// filename for <see cref="IFileDialogService.SaveTextFileAsync"/>/<see cref="IFileDialogService.SaveBinaryFileAsync"/>
/// (issues #130-#132) -- shared by <c>CampaignsViewModel</c>'s export/PDF-export actions and
/// <c>CharacterFormViewModel.ExportToPdfCommand</c> rather than each reimplementing it.
/// </summary>
public static class FileNameSanitizer
{
    /// <summary>Strips characters that are invalid in a filename on any of this app's target
    /// platforms (Windows is the strictest -- <c>&lt;&gt;:"/\|?*</c> plus control characters) from
    /// <paramref name="name"/>, so a suggested filename is always legal without the OS save dialog
    /// needing to reject or silently mangle it first. Falls back to <c>"export"</c> if nothing legal
    /// is left (e.g. a name that's entirely punctuation).</summary>
    public static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return sanitized.Length == 0 ? "export" : sanitized;
    }
}