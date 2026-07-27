namespace GmToolkit.Core.Generator;

/// <summary>
/// Generates a single value for one field/category (e.g. one occupation, one appearance quirk).
/// Generic in the result type rather than fixed to <see cref="string"/> so a future category
/// (e.g. a hypothetical "loot" table producing a structured item, not just a display string)
/// isn't blocked by this interface — every current implementation happens to produce
/// <see cref="string"/>, since every existing <see cref="GeneratorTable"/> entry's payload is one.
/// </summary>
public interface IGenerator<out TResult>
{
    /// <summary>Produces one value, drawing randomness from <paramref name="random"/>.</summary>
    TResult Generate(IRandomSource random);
}