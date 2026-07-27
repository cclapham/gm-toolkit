namespace GmToolkit.Core.Generator;

/// <summary>
/// Composes the per-category <see cref="IGenerator{TResult}"/>s in an <see cref="IGeneratorRegistry"/>
/// into a whole <see cref="GeneratedNpc"/>. Also exposes <see cref="GenerateField"/> so a single
/// named field can be regenerated in isolation — the seam #28's per-field reroll button needs —
/// without this type's design changing when that issue lands.
/// </summary>
public interface INpcGenerator
{
    /// <summary>
    /// Generates every field of a new <see cref="GeneratedNpc"/>, drawing all randomness from the
    /// same <paramref name="random"/> source in a fixed field order — so given the same seeded
    /// source, two calls (or two <see cref="INpcGenerator"/> instances over the same registry)
    /// produce identical output.
    /// </summary>
    GeneratedNpc Generate(IRandomSource random);

    /// <summary>
    /// Generates a value for exactly one <paramref name="field"/>, independent of any other field.
    /// A caller regenerating one field of an existing <see cref="GeneratedNpc"/> (#28's "reroll"
    /// button) assigns the result directly to that field's property and leaves the rest untouched.
    /// </summary>
    string GenerateField(NpcField field, IRandomSource random);
}