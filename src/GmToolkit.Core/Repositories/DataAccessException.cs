namespace GmToolkit.Core.Repositories;

/// <summary>
/// Thrown by a <c>GmToolkit.Data</c> repository when a call into the underlying SQLite database
/// fails for a reason a GM can actually act on (the file went missing, the disk is full, another
/// process has it locked, the file is corrupt, etc.) -- see
/// <c>GmToolkit.Data.DatabaseExceptionTranslator</c>, the only place that constructs this type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lives in <c>GmToolkit.Core</c>, not <c>GmToolkit.Data</c>.</b> <c>GmToolkit.UI</c>'s view
/// models (<c>CampaignFormViewModel</c>, <c>CampaignsViewModel</c>, etc.) already catch and surface
/// repository failures as inline error text (<c>SaveError</c>/<c>LoadError</c>/<c>DeleteError</c>),
/// but <c>GmToolkit.UI</c> only references <c>GmToolkit.Core</c>, not <c>GmToolkit.Data</c> (see
/// CONTRIBUTING.md's dependency-direction rule) -- so a friendly, catchable exception type shared
/// between the two has to live somewhere both can see it. It carries no SQLite-specific type
/// (<c>SQLite.SQLiteException</c>, etc.) on its public surface, keeping <c>Core</c> free of any
/// package reference, per that same rule.
/// </para>
/// <para>
/// <b><see cref="Exception.Message"/> is always written to be shown to a GM as-is</b> -- e.g. "The
/// database file is missing..." or "There's no space left on disk...", never a raw driver message
/// like "SQLite Error 14: 'unable to open database file'". Callers that already interpolate a
/// caught exception's <see cref="Exception.Message"/> into their own inline error text (every
/// existing <c>catch (Exception ex)</c> block in <c>GmToolkit.UI.ViewModels</c>) get this friendly
/// wording for free, with no extra type-checking needed at the call site.
/// </para>
/// </remarks>
public sealed class DataAccessException : Exception
{
    public DataAccessException(string message)
        : base(message)
    {
    }

    public DataAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}