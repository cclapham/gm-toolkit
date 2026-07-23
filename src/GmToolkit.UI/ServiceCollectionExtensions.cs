using GmToolkit.UI.Services;
using GmToolkit.UI.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the presentation-layer services owned by <c>GmToolkit.UI</c>: the navigation
    /// service and the app shell's view model. Both heads (<c>GmToolkit.Desktop</c>,
    /// <c>GmToolkit.Android</c>) call this so registration lives in one place instead of being
    /// duplicated across composition roots -- mirrors
    /// <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>,
    /// since <see cref="ShellViewModel"/> depends on <c>GmToolkit.Core.Services.ActiveCampaignContext</c>,
    /// which that call registers.
    /// </remarks>
    public static IServiceCollection AddGmToolkitUi(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ShellViewModel>();
        return services;
    }
}