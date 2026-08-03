using GmToolkit.Core.Systems;

namespace GmToolkit.UI.ViewModels.Stats;

/// <summary>Builds the right concrete <see cref="StatFieldViewModel"/> subtype for a
/// <see cref="StatFieldDefinition"/>'s <see cref="StatFieldDefinition.Type"/> discriminator -- shared
/// by <see cref="SchemaStatsFormViewModel"/> (top-level fields) and
/// <see cref="RepeatingGroupRowViewModel"/> (a row's item fields), so there's exactly one place that
/// maps a <see cref="StatFieldTypes"/> constant to its view model type.</summary>
public static class StatFieldViewModelFactory
{
    /// <summary>Throws <see cref="NotSupportedException"/> for an unrecognized
    /// <see cref="StatFieldDefinition.Type"/> -- defense in depth only:
    /// <see cref="CharacterSystemLoader"/> already rejects any pack with a field of an unknown type
    /// before it can ever reach the registry this factory's caller reads
    /// <see cref="StatFieldDefinition"/>s from.</summary>
    public static StatFieldViewModel Create(StatFieldDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Type switch
        {
            StatFieldTypes.Number => new NumberStatFieldViewModel(definition),
            StatFieldTypes.Text => new TextStatFieldViewModel(definition),
            StatFieldTypes.Boolean => new BooleanStatFieldViewModel(definition),
            StatFieldTypes.Enum => new EnumStatFieldViewModel(definition),
            StatFieldTypes.Derived => new DerivedStatFieldViewModel(definition),
            StatFieldTypes.RepeatingGroup => new RepeatingGroupStatFieldViewModel(definition),
            StatFieldTypes.FreeTextBlock => new FreeTextBlockStatFieldViewModel(definition),
            _ => throw new NotSupportedException($"Unrecognized stat field type \"{definition.Type}\" for field '{definition.Key}'."),
        };
    }
}