namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Thrown by <see cref="FormulaParser.Parse"/> when a formula string doesn't parse cleanly as a
/// full <c>formula := expression EOF</c> production, or exceeds either of SYSTEMS.md's "Resource
/// limits" (500-character length, 32-level nesting depth). Always a load-time rejection — see
/// <see cref="CharacterSystemLoader"/> — never allowed to reach a character's runtime evaluation
/// path uncaught.
/// </summary>
public sealed class FormulaParseException : Exception
{
    public FormulaParseException(string message)
        : base(message)
    {
    }

    public FormulaParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}