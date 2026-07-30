namespace GmToolkit.Core.Systems;

/// <summary>
/// The seven <see cref="StatFieldDefinition.Type"/> discriminator string values SYSTEMS.md's
/// "Field types" section defines. Centralized here so <see cref="CharacterSystemLoader"/>
/// validation, the formula engine, and any future consumer (e.g. #89's form generator) don't each
/// hardcode their own copy of these literals.
/// </summary>
public static class StatFieldTypes
{
    public const string Number = "number";
    public const string Text = "text";
    public const string Boolean = "boolean";
    public const string Enum = "enum";
    public const string Derived = "derived";
    public const string RepeatingGroup = "repeating-group";
    public const string FreeTextBlock = "free-text-block";

    /// <summary>Every recognized field type discriminator, for "is this a known type" checks.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Number,
        Text,
        Boolean,
        Enum,
        Derived,
        RepeatingGroup,
        FreeTextBlock,
    };
}