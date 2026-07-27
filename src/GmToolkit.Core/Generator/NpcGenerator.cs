namespace GmToolkit.Core.Generator;

/// <summary>
/// Default <see cref="INpcGenerator"/>, composing an <see cref="IGeneratorRegistry"/>'s per-category
/// generators into a <see cref="GeneratedNpc"/>. Maps each <see cref="NpcField"/> to the
/// <see cref="GeneratorTable.Category"/> it's drawn from: <see cref="NpcField.Name"/> → "names",
/// <see cref="NpcField.Role"/> → "occupation", and the rest 1:1 by name. <see cref="Models.Npc"/>'s
/// <c>Faction</c>, <c>Location</c>, <c>Notes</c>, <c>KnownToPlayers</c>, <c>CampaignId</c> and
/// <c>WasGenerated</c> are deliberately not generated — see <see cref="GeneratedNpc"/>'s remarks.
/// </summary>
/// <remarks>
/// <b>#27:</b> <see cref="GenerateField(NpcField, IRandomSource, GeneratorConstraints)"/> is the
/// constraint-aware sibling of <see cref="GenerateField(NpcField, IRandomSource)"/>, deliberately
/// added at the per-field level rather than as a whole-NPC <c>Generate(IRandomSource,
/// GeneratorConstraints)</c> overload: only two of six fields (Name, Role) have a meaningful
/// constraint at all, and the GM-facing workflow this plugs into — picking a culture/category, then
/// generating or rerolling that one field (#28) — already operates field-by-field via this same
/// method. A whole-NPC constrained overload can be added later without changing this design if #28
/// needs one.
/// </remarks>
public sealed class NpcGenerator : INpcGenerator
{
    private readonly IGeneratorRegistry _registry;

    public NpcGenerator(IGeneratorRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    public GeneratedNpc Generate(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        // Every field is drawn from the same `random` in this fixed order, so a given seed always
        // produces the same NPC.
        return new GeneratedNpc
        {
            Name = GenerateField(NpcField.Name, random),
            Role = GenerateField(NpcField.Role, random),
            Appearance = GenerateField(NpcField.Appearance, random),
            Mannerism = GenerateField(NpcField.Mannerism, random),
            Motivation = GenerateField(NpcField.Motivation, random),
            Secret = GenerateField(NpcField.Secret, random),
        };
    }

    public string GenerateField(NpcField field, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var category = CategoryFor(field);
        return _registry.GetGenerator(category).Generate(random);
    }

    public GenerationResult GenerateField(NpcField field, IRandomSource random, GeneratorConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(constraints);

        // Only Name (culture) and Role (occupation category) have a constraint defined today (see
        // GeneratorConstraints's remarks) — every other field falls through to the same
        // unconstrained draw as GenerateField(NpcField, IRandomSource), wrapped so the return type
        // still matches.
        return field switch
        {
            NpcField.Name => _registry.GetNameGenerator().GenerateWithNotice(random, constraints.NameCulture),
            NpcField.Role => _registry.GetTableGenerator(CategoryFor(field)).GenerateWithNotice(random, constraints.OccupationCategory),
            _ => GenerationResult.Unconstrained(_registry.GetGenerator(CategoryFor(field)).Generate(random)),
        };
    }

    private static string CategoryFor(NpcField field) => field switch
    {
        NpcField.Name => "names",
        NpcField.Role => "occupation",
        NpcField.Appearance => "appearance",
        NpcField.Mannerism => "mannerism",
        NpcField.Motivation => "motivation",
        NpcField.Secret => "secret",
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown NPC field."),
    };
}