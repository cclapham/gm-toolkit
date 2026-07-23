using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.Core.Services;

/// <summary>
/// Holds the campaign the user currently has open and notifies subscribers (e.g. bound Avalonia
/// view models, once #15 wires them up) when it changes.
/// </summary>
/// <remarks>
/// <para>
/// "Last opened" is persisted and restored across app launches by reusing
/// <see cref="Campaign.LastOpenedUtc"/> rather than a separate settings file or table: selecting
/// a campaign stamps and persists that field, and restoring on launch picks the campaign with the
/// latest value.
/// </para>
/// <para>
/// Registered as a singleton in the composition root (see
/// <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>) so every consumer in a
/// given process shares the same active campaign. Uses a plain C# event rather than
/// <see cref="System.ComponentModel.INotifyPropertyChanged"/> so <c>GmToolkit.Core</c> doesn't
/// need to take on a package reference just for this (see CONTRIBUTING.md's "keep Core clean"
/// rule — Core currently has zero NuGet package references).
/// </para>
/// </remarks>
public class ActiveCampaignContext(ICampaignRepository campaignRepository)
{
    /// <summary>The campaign currently selected, or <c>null</c> if none is (e.g. first run, before any
    /// campaign has ever been created, or after <see cref="Clear"/>).</summary>
    public Campaign? ActiveCampaign { get; private set; }

    /// <summary>Raised whenever <see cref="ActiveCampaign"/> changes, so bound views can re-render.</summary>
    public event Action? ActiveCampaignChanged;

    /// <summary>
    /// Selects <paramref name="campaign"/> as the active campaign: stamps
    /// <see cref="Campaign.LastOpenedUtc"/> with the current time and persists it via the
    /// repository, then updates in-memory state and raises <see cref="ActiveCampaignChanged"/>.
    /// </summary>
    public async Task SelectCampaignAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        campaign.LastOpenedUtc = DateTime.UtcNow;
        await campaignRepository.UpdateAsync(campaign, cancellationToken);

        ActiveCampaign = campaign;
        ActiveCampaignChanged?.Invoke();
    }

    /// <summary>
    /// Restores the active campaign on launch: the campaign with the latest
    /// <see cref="Campaign.LastOpenedUtc"/>, or <c>null</c> if no campaigns exist yet — a normal
    /// first-run state, not an error. This is a read, not a fresh selection, so unlike
    /// <see cref="SelectCampaignAsync"/> it does not re-persist <see cref="Campaign.LastOpenedUtc"/>.
    /// </summary>
    /// <remarks>
    /// Requires an async repository call, so it can't happen in the constructor — the composition
    /// root calls this explicitly once, at startup.
    /// </remarks>
    public async Task RestoreLastOpenedAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await campaignRepository.GetAllAsync(cancellationToken);

        ActiveCampaign = campaigns.Count == 0
            ? null
            : campaigns.Aggregate((latest, candidate) => candidate.LastOpenedUtc > latest.LastOpenedUtc ? candidate : latest);

        ActiveCampaignChanged?.Invoke();
    }

    /// <summary>
    /// Clears the active campaign without touching persistence — just in-memory state and the
    /// change notification. Doesn't persist anything by design: clearing isn't "closing" a
    /// campaign in a way that should affect what's restored next launch (that's still whichever
    /// campaign has the latest <see cref="Campaign.LastOpenedUtc"/>). Needed by #19 (campaign
    /// delete), which clears the active campaign if it was the one just deleted.
    /// </summary>
    public void Clear()
    {
        ActiveCampaign = null;
        ActiveCampaignChanged?.Invoke();
    }
}