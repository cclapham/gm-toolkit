using System.Collections.ObjectModel;

using Avalonia.Threading;

using GmToolkit.UI.ViewModels;

namespace GmToolkit.UI.Services;

/// <inheritdoc cref="INotificationService" />
/// <remarks>
/// <para>
/// <b>Auto-dismiss uses a real <see cref="DispatcherTimer"/></b>, not <see cref="System.Threading.Timer"/>/
/// <see cref="Task.Delay(TimeSpan)"/> -- its callback always runs on the UI thread by construction,
/// which matters here since it mutates <see cref="Toasts"/>, an <see cref="ObservableCollection{T}"/>
/// that <c>ToastHost</c>'s bound <c>ItemsControl</c> expects to only ever change on the UI thread.
/// This does mean the auto-dismiss delay itself isn't covered by an xUnit test (there's no pumped
/// dispatcher loop in a plain test, matching this app's existing convention of not unit-testing
/// pure visual/timing behavior -- e.g. <c>LoadingIndicator</c>'s spinner animation isn't tested
/// either); <see cref="Show"/> synchronously adding to <see cref="Toasts"/> and
/// <see cref="ToastViewModel.DismissCommand"/> removing from it (both plain, timer-free mutations)
/// are what's actually tested.
/// </para>
/// <para>
/// <b>Not itself dispatcher-thread-safe</b> -- see <see cref="INotificationService.Show"/>'s remarks
/// on why that's the caller's job, not this class's.
/// </para>
/// </remarks>
public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan AutoDismissDelay = TimeSpan.FromSeconds(6);

    public ObservableCollection<ToastViewModel> Toasts { get; } = [];

    public void Show(string message, ToastSeverity severity = ToastSeverity.Info)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);

        var toast = new ToastViewModel(message, severity, Dismiss);
        Toasts.Add(toast);

        var timer = new DispatcherTimer { Interval = AutoDismissDelay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Dismiss(toast);
        };
        timer.Start();
    }

    private void Dismiss(ToastViewModel toast) => Toasts.Remove(toast);
}