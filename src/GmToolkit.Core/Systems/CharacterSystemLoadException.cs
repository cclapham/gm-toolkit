namespace GmToolkit.Core.Systems;

/// <summary>
/// Thrown when a <see cref="CharacterSystem"/> pack is missing, malformed, or fails any item of
/// SYSTEMS.md's "Load-time validation checklist" — a formula that doesn't parse, an unknown or
/// duplicate field key, a ceiling exceeded, a dependency cycle, and so on. The message always
/// identifies the offending pack/scope/field so a bad pack is easy to find and fix. Since a
/// downloaded community pack (paused, #91) is untrusted data, every validation path that can throw
/// this must never let a different, unhandled exception escape instead — see
/// <see cref="CharacterSystemLoader"/>.
/// </summary>
public sealed class CharacterSystemLoadException : Exception
{
    public CharacterSystemLoadException(string message)
        : base(message)
    {
    }

    public CharacterSystemLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}