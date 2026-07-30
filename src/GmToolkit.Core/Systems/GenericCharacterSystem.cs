namespace GmToolkit.Core.Systems;

/// <summary>
/// The built-in "no schema" system — zero <see cref="CharacterSystem.PcFields"/>/
/// <see cref="CharacterSystem.NpcFields"/>, matching today's fully freeform
/// <c>PlayerCharacter.Stats</c>/<c>Npc.Stats</c> behavior exactly. See SYSTEMS.md's "Attachment
/// point": a <c>Campaign</c> with a <c>null</c> <c>CharacterSystemId</c> keeps today's plain
/// <c>Dictionary&lt;string, string&gt;</c> behavior unchanged; this type exists so that "no system
/// attached" also has a normal, nameable <see cref="CharacterSystem"/> of its own rather than being
/// a special case bolted on top of <see cref="ICharacterSystemRegistry"/> everywhere it's consulted.
/// </summary>
public static class GenericCharacterSystem
{
    /// <summary>The generic system's own <see cref="CharacterSystem.Id"/>.</summary>
    public const string Id = "generic";

    /// <summary>The single, shared instance — always present in <see cref="CharacterSystemRegistry.FromEmbeddedSystems"/>'s result.</summary>
    public static readonly CharacterSystem Instance = new()
    {
        FormatVersion = CharacterSystemLoader.SupportedFormatVersion,
        Id = Id,
        Name = "Generic (freeform)",
        Version = "1.0.0",
        Author = "GM Toolkit",
        Description = "No schema constraints -- stats are a plain, freeform key/value bag, exactly like a campaign with no CharacterSystemId attached.",
        PcFields = [],
        NpcFields = [],
    };
}