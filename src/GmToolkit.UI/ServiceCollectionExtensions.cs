using GmToolkit.Core.Generator;
using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the presentation-layer services owned by <c>GmToolkit.UI</c>: the navigation
    /// service, the app shell's view model, and the generator (issue #28) services
    /// <see cref="IGeneratorRegistry"/>/<see cref="INpcGenerator"/>. Both heads
    /// (<c>GmToolkit.Desktop</c>, <c>GmToolkit.Android</c>) call this so registration lives in one
    /// place instead of being duplicated across composition roots -- mirrors
    /// <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>,
    /// since <see cref="ShellViewModel"/> depends on <c>GmToolkit.Core.Services.ActiveCampaignContext</c>,
    /// which that call registers.
    /// </para>
    /// <para>
    /// <b><see cref="IGeneratorRegistry"/>/<see cref="INpcGenerator"/> are registered here, not in
    /// <c>GmToolkit.Data</c>'s <c>AddGmToolkitData</c>.</b> Both types (and everything they're built
    /// from -- <see cref="GeneratorTableLoader"/>'s embedded JSON resources) live entirely in
    /// <c>GmToolkit.Core</c> with zero dependency on <c>GmToolkit.Data</c>/SQLite, so registering them
    /// alongside this project's own navigation/shell services is the correct home, same reasoning as
    /// every other <c>GmToolkit.Core</c> service already registered here. Both are singletons: a
    /// <see cref="GeneratorRegistry"/> is immutable once built from the embedded tables (there's
    /// nothing per-request or per-campaign about it), and <see cref="NpcGenerator"/> is a stateless
    /// wrapper around it -- constructing either per view model instance would just mean re-parsing
    /// the same embedded JSON for no benefit.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGmToolkitUi(this IServiceCollection services)
    {
        services.AddSingleton<IGeneratorRegistry>(_ => GeneratorRegistry.FromEmbeddedTables());
        services.AddSingleton<INpcGenerator, NpcGenerator>();
        services.AddSingleton<INavigationService, NavigationService>();
        // Singleton (issue #32): one app-wide toast stack, not one per screen -- see
        // INotificationService's remarks on why the shell hosts it.
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}