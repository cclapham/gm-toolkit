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

    /// <summary>
    /// Generates a value for exactly one <paramref name="field"/>, honoring whichever of
    /// <paramref name="constraints"/> applies to that field (issue #27: name culture for
    /// <see cref="NpcField.Name"/>, occupation category for <see cref="NpcField.Role"/>; every
    /// other field ignores <paramref name="constraints"/> and behaves exactly like
    /// <see cref="GenerateField(NpcField, IRandomSource)"/>). Returns a <see cref="GenerationResult"/>
    /// rather than a plain <see cref="string"/> so a caller can tell when a requested constraint
    /// didn't match anything and generation fell back to an unconstrained pick instead — the
    /// information a future constraint-selection UI (#28) needs in order to show the GM a notice,
    /// rather than that information being silently discarded here.
    /// </summary>
    GenerationResult GenerateField(NpcField field, IRandomSource random, GeneratorConstraints constraints);
}