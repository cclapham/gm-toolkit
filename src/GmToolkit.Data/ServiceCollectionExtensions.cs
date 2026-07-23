using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;
using GmToolkit.Data.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an already-initialized <see cref="GmToolkitDatabase"/>, the repository
    /// implementations, and the <see cref="ActiveCampaignContext"/> service that depends on them.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for resolving the platform app-data path and constructing
    /// <paramref name="database"/> via <see cref="GmToolkitDatabase.CreateAndInitializeAsync"/>
    /// before calling this method — that handles directory creation and first-run/corrupt-file
    /// recovery (issue #12). By the time it's passed in here, it's already open and initialized.
    /// </remarks>
    public static IServiceCollection AddGmToolkitData(this IServiceCollection services, GmToolkitDatabase database)
    {
        services.AddSingleton(database);
        services.AddSingleton<ICampaignRepository, CampaignRepository>();
        services.AddSingleton<IPlayerCharacterRepository, PlayerCharacterRepository>();
        services.AddSingleton<INpcRepository, NpcRepository>();
        services.AddSingleton<ActiveCampaignContext>();
        return services;
    }
}