using System.Diagnostics;
using System.Threading.Tasks;

using Avalonia.Threading;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.UI.Services;

/// <summary>
/// Wires up process-wide/dispatcher-wide exception handling (issue #32's "global exception
/// handling around each head's composition root, never a blank/frozen screen" task) -- lives here,
/// in <c>GmToolkit.UI</c>, rather than being duplicated in both <c>GmToolkit.Desktop/Program.cs</c>
/// and <c>GmToolkit.Android/Application.cs</c>, since both heads already share every other
/// composition-root concern through this project (<c>ServiceCollectionExtensions.AddGmToolkitUi</c>,
/// <c>App.axaml.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Three separate hooks, because no single one covers every failure mode this app can hit:</b>
/// </para>
/// <list type="bullet">
/// <item><see cref="AppDomain.UnhandledException"/> -- the last-resort net for anything that
/// escapes every other handler. By the time this fires <see cref="UnhandledExceptionEventArgs.IsTerminating"/>
/// is already <c>true</c> in practice; nothing here can stop the process exiting, so this only
/// logs. Installed by <see cref="InstallProcessWideHandlers"/>, called as the very first line of
/// each head's <c>Main</c>/<c>OnCreate</c> so it's in place before anything else (DB bootstrap, DI
/// container construction) that could itself throw.</item>
/// <item><see cref="TaskScheduler.UnobservedTaskException"/> -- fires when a faulted
/// <see cref="Task"/> is garbage-collected without anyone having awaited/observed its exception.
/// <see cref="UnobservedTaskExceptionEventArgs.SetObserved"/> below prevents this from being fatal;
/// also installed by <see cref="InstallProcessWideHandlers"/>.</item>
/// <item><see cref="Dispatcher.UnhandledException"/> -- the one that matters most for this issue's
/// concrete acceptance criterion. CommunityToolkit.Mvvm's generated <c>[RelayCommand]</c> async
/// commands (every <c>SaveAsync</c>/<c>ConfirmDeleteAsync</c> in <c>GmToolkit.UI.ViewModels</c>) by
/// default re-throw any exception the command's <see cref="Task"/> failed with, back onto whatever
/// <see cref="System.Threading.SynchronizationContext"/> was current when the button's click first
/// invoked it (see <c>CommunityToolkit.Mvvm.Input.AsyncRelayCommand.Execute</c>/
/// <c>AwaitAndThrowIfFailed</c>) -- on the classic desktop lifetime, that is Avalonia's own
/// dispatcher-backed <see cref="System.Threading.SynchronizationContext"/>, whose <c>Post</c>
/// callbacks are wrapped in exactly the try/catch that raises this event. Without this hook, an
/// exception from a Save/Delete click that isn't already caught by that view model's own
/// <c>catch</c> block (see <c>CampaignFormViewModel.SaveAsync</c> etc.) would otherwise crash the
/// whole app instead of being merely unfortunate. <see cref="DispatcherUnhandledExceptionEventArgs.Handled"/>
/// set to <c>true</c> below tells Avalonia's dispatcher loop to keep running rather than crash.
/// Installed by <see cref="InstallDispatcherHandler"/>, called from <c>App.axaml.cs</c> once
/// <c>Dispatcher.UIThread</c> is guaranteed to exist and the DI container is resolvable.</item>
/// </list>
/// <para>
/// <b>Logging is best-effort <see cref="Trace"/> output, not a persisted crash-log file.</b> This
/// app has no existing file-based logging infrastructure (checked before starting this issue), and
/// building one (rotation, a platform-specific log directory, etc.) is meaningfully more scope than
/// this issue asks for -- its acceptance criterion is about the user seeing a comprehensible
/// message, not about developer-facing crash forensics. <see cref="Trace"/> output is visible via
/// each head's own <c>AppBuilder.LogToTrace()</c> (already configured) when a debugger/trace
/// listener is attached, which is proportionate to a hobby-scale MVP's needs today.
/// </para>
/// <para>
/// <b>Toasts are best-effort, guarded by <see cref="App.Services"/> possibly being unset.</b>
/// <see cref="InstallProcessWideHandlers"/> is called before either head builds its DI container
/// (see this class's remarks above), so a failure in that narrow startup window has nowhere to
/// route a toast to -- it's still logged via <see cref="Trace"/>, just not shown on screen (there is
/// no screen yet at that point anyway).
/// </para>
/// </remarks>
public static class GlobalExceptionHandler
{
    private const string UserFacingMessage =
        "Something went wrong and GM Toolkit had to recover. If this keeps happening, please " +
        "restart the app.";

    public static void InstallProcessWideHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void InstallDispatcherHandler()
    {
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            LogException("AppDomain.UnhandledException", exception);
        }

        // IsTerminating is true here in every case actually observed (confirmed while building
        // this fix) -- there is nothing left to do that keeps the process alive; this is a
        // logging-only last resort, see this class's remarks.
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogException("TaskScheduler.UnobservedTaskException", e.Exception);

        // Prevents this from being treated as a fatal, process-ending failure -- the whole point
        // of this hook existing (see this class's remarks).
        e.SetObserved();

        NotifyUserOnUiThread(e.Exception);
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("Dispatcher.UnhandledException", e.Exception);

        // Already on the UI thread (that's what this event is), so no marshaling needed -- unlike
        // NotifyUserOnUiThread's callers, which can fire from any thread.
        App.Services?.GetService<INotificationService>()?.Show(UserFacingMessage, ToastSeverity.Error);

        // Tells Avalonia's dispatcher loop to keep running rather than let this crash the app --
        // see this class's remarks for why this specific hook matters most for issue #32's
        // acceptance criterion.
        e.Handled = true;
    }

    private static void NotifyUserOnUiThread(Exception exception)
    {
        // TaskScheduler.UnobservedTaskException can fire from any thread (typically a finalizer
        // thread, since it's raised when a faulted Task is garbage-collected) -- always marshal
        // onto the UI thread before touching INotificationService.Toasts, mirroring every other
        // background-thread-to-UI-thread handoff already established in this codebase (e.g.
        // ShellViewModel.OnActiveCampaignChanged).
        Dispatcher.UIThread.Post(() =>
            App.Services?.GetService<INotificationService>()?.Show(UserFacingMessage, ToastSeverity.Error));
    }

    [Conditional("DEBUG")]
    private static void LogException(string source, Exception exception) =>
        Trace.TraceError($"GmToolkit: unhandled exception via {source}: {exception}");
}