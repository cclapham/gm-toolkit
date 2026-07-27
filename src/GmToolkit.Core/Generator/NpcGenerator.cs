namespace GmToolkit.Core.Generator;

/// <summary>
/// Default <see cref="INpcGenerator"/>, composing an <see cref="IGeneratorRegistry"/>'s per-category
/// generators into a <see cref="GeneratedNpc"/>. Maps each <see cref="NpcField"/> to the
/// <see cref="GeneratorTable.Category"/> it's drawn from: <see cref="NpcField.Name"/> → "names",
/// <see cref="NpcField.Role"/> → "occupation", and the rest 1:1 by name. <see cref="Models.Npc"/>'s
/// <c>Faction</c>, <c>Location</c>, <c>Notes</c>, <c>KnownToPlayers</c>, <c>CampaignId</c> and
/// <c>WasGenerated</c> are deliberately not generated — see <see cref="GeneratedNpc"/>'s remarks.
/// </summary>
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