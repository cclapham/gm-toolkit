using GmToolkit.Core.Repositories;
using GmToolkit.Data.Repositories;

using Microsoft.Extensions.DependencyInjection;

namespace GmToolkit.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GmToolkitDatabase"/> and the repository implementations.
    /// </summary>
    /// <remarks>
    /// The caller is still responsible for calling
    /// <c>serviceProvider.GetRequiredService&lt;GmToolkitDatabase&gt;().InitializeAsync()</c>
    /// once at startup before the repositories are used — resolving the platform app-data
    /// path to pass in as <paramref name="databasePath"/> is issue #12's job, not this one.
    /// </remarks>
    public static IServiceCollection AddGmToolkitData(this IServiceCollection services, string databasePath)
    {
        services.AddSingleton(_ => new GmToolkitDatabase(databasePath));
        services.AddSingleton<ICampaignRepository, CampaignRepository>();
        services.AddSingleton<IPlayerCharacterRepository, PlayerCharacterRepository>();
        services.AddSingleton<INpcRepository, NpcRepository>();
        return services;
    }
}