using System.Collections.ObjectModel;

using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Services;

/// <summary>
/// The app-wide toast/notification sink (issue #32's "a toast/notification Avalonia control used
/// everywhere" task). A single instance is registered as a DI singleton
/// (<see cref="ServiceCollectionExtensions.AddGmToolkitUi"/>) and bound from
/// <c>ShellView.axaml</c> (via <c>ShellViewModel.Toasts</c>) to <c>Controls/ToastHost.axaml</c>, the
/// one persistent visual host every screen shares -- see <c>ShellView.axaml</c>'s remarks for why
/// the shell, specifically, is where this is hosted.
/// </summary>
public interface INotificationService
{
    /// <summary>Every toast currently showing, oldest first. <c>ToastHost</c> binds this directly;
    /// mutating it (add on <see cref="Show"/>, remove on dismiss/auto-dismiss) is what makes a
    /// toast appear/disappear on screen with no further plumbing.</summary>
    ObservableCollection<ToastViewModel> Toasts { get; }

    /// <summary>
    /// Shows a new toast with <paramref name="message"/>, auto-dismissed after a short delay (see
    /// <see cref="NotificationService"/>'s remarks) or sooner if the user dismisses it manually via
    /// <see cref="ToastViewModel.DismissCommand"/>.
    /// </summary>
    /// <remarks>
    /// <b>Callers on a background thread must marshal onto <see cref="Avalonia.Threading.Dispatcher.UIThread"/>
    /// themselves before calling this</b> -- mirrors this codebase's existing convention for every
    /// other UI-bound mutation reacting to a background-thread event (e.g.
    /// <c>ShellViewModel.OnActiveCampaignChanged</c>'s identical <c>Dispatcher.UIThread.Post</c>
    /// call at its own call site, not inside <c>ActiveCampaignContext</c> itself). This keeps
    /// <see cref="NotificationService"/> a plain, directly unit-testable class with no dispatcher
    /// dependency of its own; see <see cref="GlobalExceptionHandler"/> for the one caller that
    /// actually needs to marshal (its handlers can run on arbitrary threads).
    /// </remarks>
    void Show(string message, ToastSeverity severity = ToastSeverity.Info);
}