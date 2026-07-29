namespace GmToolkit.UI.ViewModels;

/// <summary>
/// Implemented by screen view models that show campaign-owned data (<see cref="CampaignsViewModel"/>,
/// <see cref="CharactersViewModel"/>, <see cref="NpcsViewModel"/>, <see cref="GeneratorViewModel"/>)
/// which some other screen's action could make stale while this one sits cached forever in
/// <see cref="Services.NavigationService"/> (issue #68) -- e.g. an NPC added via
/// <see cref="NpcsViewModel"/>'s own <see cref="NpcsViewModel.Form"/> going unseen by
/// <see cref="GeneratorViewModel"/>'s <see cref="GeneratorViewModel.FactionSuggestions"/>/
/// <see cref="GeneratorViewModel.LocationSuggestions"/>, which raises nothing
/// <see cref="GeneratorViewModel"/> was ever listening for. "Shows campaign-owned data" is
/// deliberately broader than "shows a list" here -- <see cref="GeneratorViewModel"/> has no list of
/// its own, but its faction/location suggestions are exactly the same class of repository-backed,
/// another-screen-can-mutate-it data the other three implementations reload.
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
/// <b>Deliberately not implemented by <see cref="SettingsViewModel"/>.</b> It isn't campaign-scoped
/// at all, so there's no campaign-owned data on that screen for another screen's action to make
/// stale in the first place -- unlike <see cref="GeneratorViewModel"/> (see this interface's summary),
/// which does implement this despite having no list, precisely because its suggestion data *is*
/// campaign-owned and repository-backed.
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