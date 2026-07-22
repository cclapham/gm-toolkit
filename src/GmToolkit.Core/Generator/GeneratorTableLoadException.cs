namespace GmToolkit.Core.Generator;

/// <summary>
/// Thrown when a generator table embedded resource is missing, malformed, or fails schema
/// validation (e.g. a missing/empty <c>value</c>, a non-positive <c>weight</c>, or no entries).
/// The message always identifies the offending resource so a bad table is easy to find and fix.
/// </summary>
public sealed class GeneratorTableLoadException : Exception
{
    public GeneratorTableLoadException(string message)
        : base(message)
    {
    }

    public GeneratorTableLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}