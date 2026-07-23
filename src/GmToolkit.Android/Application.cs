using Android.App;
using Android.Runtime;

using Avalonia;
using Avalonia.Android;

using GmToolkit.Core.Services;
using GmToolkit.Data;
using GmToolkit.UI;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.Android
{
    [Application]
    public class Application : AvaloniaAndroidApplication<App>
    {
        private ServiceProvider? _serviceProvider;

        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        // Avalonia.Android has no async-friendly entry point before CustomizeAppBuilder is
        // invoked (that happens from the main Activity's OnCreate), so database bootstrap is
        // done here, in the Application's OnCreate — which the Android framework guarantees
        // runs once per process, before any Activity is created. There's no clean way to await
        // async work from this override, so we block on it; this runs once, early in process
        // startup before any UI is shown, and talks only to a local SQLite file, so the blocking
        // wait is short-lived in practice. This is a deliberate, narrow exception to "no blocking
        // waits on async calls" (CONTRIBUTING.md) forced by the platform lifecycle contract, not
        // a shortcut — the alternative (async void OnCreate) would let Android proceed to
        // CustomizeAppBuilder/the Activity before the database is ready, which is worse. The work
        // is wrapped in Task.Run so the awaited continuations run on a thread-pool thread rather
        // than being posted back to (and deadlocking on) the blocked main-thread
        // SynchronizationContext, per the standard "block via Task.Run" pattern for sync-over-async
        // on a UI thread.
        //
        // Restoring the last-opened campaign (#16) is the same story: it's one more local-SQLite
        // read that has to finish before any UI is shown (so the restored campaign is available
        // to the very first view model that asks for it), and OnCreate offers no clean async
        // path either — so it's blocked on for the same reasons as the database bootstrap above.
        public override void OnCreate()
        {
            base.OnCreate();

            var databasePath = System.IO.Path.Combine(FilesDir!.AbsolutePath, AppDataPaths.DatabaseFileName);
            var database = Task.Run(() => GmToolkitDatabase.CreateAndInitializeAsync(databasePath))
                .GetAwaiter()
                .GetResult();

            var services = new ServiceCollection();
            services.AddGmToolkitData(database);
            services.AddGmToolkitUi();
            _serviceProvider = services.BuildServiceProvider();

            // Make the container reachable from GmToolkit.UI (see App.Services' doc comment), and
            // restore whichever campaign was last opened before any UI is shown.
            App.Services = _serviceProvider;
            var activeCampaignContext = _serviceProvider.GetRequiredService<ActiveCampaignContext>();
            Task.Run(() => activeCampaignContext.RestoreLastOpenedAsync())
                .GetAwaiter()
                .GetResult();
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}