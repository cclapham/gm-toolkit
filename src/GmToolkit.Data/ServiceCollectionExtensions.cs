using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.Data.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an already-initialized <see cref="GmToolkitDatabase"/>, the repository
    /// implementations, the <see cref="ActiveCampaignContext"/> service that depends on them, and
    /// an already-constructed <paramref name="appSettingsService"/> (issue #31).
    /// </summary>
    /// <remarks>
    /// The caller is responsible for resolving the platform app-data path and constructing
    /// <paramref name="database"/> via <see cref="GmToolkitDatabase.CreateAndInitializeAsync"/>
    /// before calling this method — that handles directory creation and first-run/corrupt-file
    /// recovery (issue #12). By the time it's passed in here, it's already open and initialized.
    /// <paramref name="appSettingsService"/> is accepted the same way (already constructed by the
    /// composition root, pointed at the platform-resolved settings file path) rather than
    /// registered as an open-generic type here, since — like <paramref name="database"/> — only
    /// the composition root knows the platform-specific path to construct it with.
    /// </remarks>
    public static IServiceCollection AddGmToolkitData(this IServiceCollection services, GmToolkitDatabase database, IAppSettingsService appSettingsService)
    {
        services.AddSingleton(database);
        services.AddSingleton(appSettingsService);
        services.AddSingleton<ICampaignRepository, CampaignRepository>();
        services.AddSingleton<IPlayerCharacterRepository, PlayerCharacterRepository>();
        services.AddSingleton<INpcRepository, NpcRepository>();
        services.AddSingleton<ActiveCampaignContext>();
        return services;
    }
}