namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Implemented by screen view models that list campaign-owned data (<see cref="CampaignsViewModel"/>,
/// <see cref="CharactersViewModel"/>, <see cref="NpcsViewModel"/>) which some other screen's action
/// could make stale while this one sits cached forever in <see cref="Services.NavigationService"/>
/// (issue #68) -- e.g. <see cref="GeneratorViewModel"/> saving a generated NPC straight through
/// <see cref="Core.Repositories.INpcRepository.AddAsync"/> rather than through
/// <see cref="NpcsViewModel"/>'s own <see cref="NpcsViewModel.Form"/>, which raises nothing
/// <see cref="NpcsViewModel"/> was ever listening for.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Services.NavigationService.NavigateTo"/> calls <see cref="RefreshAsync"/> every
/// time it navigates to an <see cref="IRefreshable"/> screen, including the very first time (when
/// the screen is also being constructed).</b> That's a deliberate single call site for "make sure
/// this screen's data is fresh", rather than one path for construction-time loading and a second,
/// possibly-diverging path for navigate-time refreshing.
/// </para>
/// <para>
/// <b>Deliberately not implemented by <see cref="GeneratorViewModel"/> or <see cref="SettingsViewModel"/>.</b>
/// Neither shows a list of campaign-owned data that another screen's action could make stale --
/// <see cref="GeneratorViewModel"/> already refreshes its own suggestion data as part of its own save
/// flow, and <see cref="SettingsViewModel"/> isn't campaign-scoped at all.
/// </para>
/// </remarks>
public interface IRefreshable
{
    /// <summary>
    /// Re-loads this screen's data from its repository -- implementations should simply delegate to
    /// whatever private <c>LoadAsync</c>-equivalent method their constructor's own fire-and-forget
    /// initial load already calls, rather than introducing a second, possibly-diverging reload path.
    /// That method must already preserve any in-progress user state (search text, filter selections,
    /// sort order) the same way each of these view models' own <c>HandleActiveCampaignChanged</c>
    /// already does when the active campaign changes out from under a showing screen.
    /// </summary>
    Task RefreshAsync();
}