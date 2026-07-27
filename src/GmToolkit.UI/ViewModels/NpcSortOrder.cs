namespace GmToolkit.UI.ViewModels;

/// <summary>
/// The two sort orders <see cref="NpcsViewModel"/> supports (issue #24's "sort by name and by
/// recently added" task) -- exposed as an enum rather than a single bool ("sort by recent?") since
/// a third order is plausible later (e.g. by faction), and an enum reads clearly at every call site
/// without an inverted-bool ("<c>!SortByName</c>") anywhere.
/// </summary>
public enum NpcSortOrder
{
    Name,
    RecentlyAdded,
}