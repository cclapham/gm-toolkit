namespace GmToolkit.Core.Generator;

/// <summary>
/// The set of <see cref="GeneratedNpc"/> fields that <see cref="INpcGenerator"/> can produce,
/// individually or as part of a whole NPC. Exists so a caller (e.g. #28's per-field reroll button)
/// can name exactly one field to regenerate via <see cref="INpcGenerator.GenerateField"/> without
/// re-running the other five.
/// </summary>
public enum NpcField
{
    Name,
    Role,
    Appearance,
    Mannerism,
    Motivation,
    Secret,
}