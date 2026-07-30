namespace GmToolkit.Core.Systems;

/// <summary>
/// A system-agnostic character/NPC stat schema pack — a typed "view" over the plain
/// <c>Dictionary&lt;string, string&gt;</c> that <c>PlayerCharacter.Stats</c> (and the future
/// <c>Npc.Stats</c>, #88) already store. See SYSTEMS.md's "The <c>CharacterSystem</c> envelope"
/// for the exact shape and every rule below.
/// </summary>
public sealed class CharacterSystem
{
    /// <summary>
    /// Versions this document's own JSON shape, distinct from <see cref="Version"/> (the pack's
    /// content version). A client that doesn't recognize a pack's <see cref="FormatVersion"/>
    /// refuses to load it. This engine currently only recognizes <c>1</c>.
    /// </summary>
    public required int FormatVersion { get; init; }

    /// <summary>
    /// Format <c>^[a-z0-9][a-z0-9-]*$</c>, max 64 characters — deliberately not the same charset as
    /// a field <see cref="StatFieldDefinition.Key"/>, since an <see cref="Id"/> is also (per the
    /// deferred #91 work) a cache filename and part of a URL path. Must be unique among all
    /// installed systems; built-ins always win a collision (see
    /// <see cref="CharacterSystemRegistry"/>).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Display name, e.g. "GURPS Fourth Edition".</summary>
    public required string Name { get; init; }

    /// <summary>The pack's own content version (e.g. semver), bumped when its rules content changes.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Pack author/attribution.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Short human-readable description of the system.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The player-character sheet's field definitions.</summary>
    public IReadOnlyList<StatFieldDefinition> PcFields { get; init; } = [];

    /// <summary>
    /// The NPC/monster-block field definitions — independent of <see cref="PcFields"/> (may share
    /// field <see cref="StatFieldDefinition.Key"/>s with the same meaning, but there's no
    /// requirement that they do).
    /// </summary>
    public IReadOnlyList<StatFieldDefinition> NpcFields { get; init; } = [];
}