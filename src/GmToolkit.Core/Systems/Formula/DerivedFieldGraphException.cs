namespace GmToolkit.Core.Systems.Formula;

/// <summary>
/// Thrown by <see cref="DerivedFieldGraph.Build"/> when a scope's <c>derived</c> fields form a
/// cycle (including a self-reference) or their longest dependency chain exceeds
/// <see cref="DerivedFieldGraph.MaxChainDepth"/>. Always a load-time rejection — see
/// <see cref="CharacterSystemLoader"/> — never a runtime failure or an infinite evaluation.
/// </summary>
public sealed class DerivedFieldGraphException : Exception
{
    public DerivedFieldGraphException(string message)
        : base(message)
    {
    }

    public DerivedFieldGraphException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}