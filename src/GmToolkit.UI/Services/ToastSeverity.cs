namespace GmToolkit.UI.Services;

/// <summary>
/// Visual/urgency category for a toast raised via <see cref="INotificationService"/> (issue #32).
/// Drives which brush <c>Controls/ToastHost.axaml</c>'s per-severity style applies -- see
/// <c>Styles/Controls.axaml</c>'s <c>Border.toast-*</c> styles.
/// </summary>
public enum ToastSeverity
{
    /// <summary>A neutral, non-urgent notice. Uses the app's ordinary surface/accent colors.</summary>
    Info,

    /// <summary>Something a GM should notice but that isn't itself a failure (e.g. a fallback
    /// kicked in). Uses the same amber accent this app's Fluent palette already uses for its
    /// primary accent color, so it reads as "notable" without introducing a brand-new hue.</summary>
    Warning,

    /// <summary>A failure -- e.g. a caught <see cref="GmToolkit.Core.Repositories.DataAccessException"/>
    /// or a global unhandled/unobserved exception (see <see cref="GlobalExceptionHandler"/>). Uses
    /// <c>DangerBrush</c>, the same red already used for inline validation/save/delete errors
    /// throughout this app.</summary>
    Error,
}