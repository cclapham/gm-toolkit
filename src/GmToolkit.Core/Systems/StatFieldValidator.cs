using System.Globalization;
using System.Text.RegularExpressions;

namespace GmToolkit.Core.Systems;

/// <summary>
/// Validates a single stat field's raw (already-trimmed-on-read) string value against its own
/// <see cref="StatFieldDefinition"/> -- the runtime counterpart to <see cref="CharacterSystemLoader"/>'s
/// load-time checks (issue #89/#90's "client-side validation against each field's min/max/regex
/// before save"). Reused by <c>GmToolkit.UI.ViewModels.Stats.StatFieldViewModel</c> subtypes so the
/// UI never re-implements what "a valid value for this field type" means -- same "one place owns the
/// rule" idiom as <c>PlayerCharacter.CharacterName</c>'s setter being the single source of truth for
/// <c>CharacterFormViewModel</c>'s own name validation.
/// </summary>
/// <remarks>
/// Deliberately does not validate <see cref="StatFieldTypes.Derived"/> (never user-entered -- see
/// SYSTEMS.md's "a `derived` field's value is never persisted") or, by itself,
/// <see cref="StatFieldTypes.RepeatingGroup"/> (a group's own rows are validated per-cell by calling
/// this method once per row/item-field pair; its row-*count* bound is a separate concern -- see
/// <see cref="ValidateRepeatingGroupRowCount"/>). Every rule here treats an empty/whitespace-only
/// value as valid: the schema format (SYSTEMS.md) has no "required field" concept, so a blank field
/// is simply "not yet filled in," not invalid input -- min/max/pattern/enum-membership only apply
/// once a value is actually present.
/// </remarks>
public static class StatFieldValidator
{
    /// <summary>
    /// Validates <paramref name="rawValue"/> against <paramref name="field"/>'s type-specific rules.
    /// Returns a user-facing error message, or <c>null</c> if <paramref name="rawValue"/> is valid
    /// (including empty/whitespace-only, per this class's remarks).
    /// </summary>
    public static string? ValidateValue(StatFieldDefinition field, string? rawValue)
    {
        ArgumentNullException.ThrowIfNull(field);

        var trimmed = rawValue?.Trim() ?? string.Empty;

        return field.Type switch
        {
            StatFieldTypes.Number => ValidateNumber(field, trimmed),
            StatFieldTypes.Text => ValidateText(field, trimmed, CharacterSystemLoader.DefaultTextMaxLength, allowPattern: true),
            StatFieldTypes.FreeTextBlock => ValidateText(field, trimmed, CharacterSystemLoader.DefaultFreeTextBlockMaxLength, allowPattern: false),
            StatFieldTypes.Boolean => ValidateBoolean(trimmed),
            StatFieldTypes.Enum => ValidateEnum(field, trimmed),

            // `derived` is never user-entered (see this class's remarks); `repeating-group` has no
            // single scalar raw value to validate here -- both are simply "nothing to reject."
            _ => null,
        };
    }

    /// <summary>
    /// Validates a <see cref="StatFieldTypes.RepeatingGroup"/> field's current row count against its
    /// own <see cref="StatFieldDefinition.MinItems"/>/<see cref="StatFieldDefinition.MaxItems"/>
    /// (falling back to <see cref="CharacterSystemLoader.DefaultMaxItems"/> when unset, exactly like
    /// <see cref="CharacterSystemLoader"/>'s own load-time check). Per-row/per-cell values are
    /// validated separately via repeated <see cref="ValidateValue"/> calls, one per item field.
    /// </summary>
    public static string? ValidateRepeatingGroupRowCount(StatFieldDefinition field, int rowCount)
    {
        ArgumentNullException.ThrowIfNull(field);

        var maxItems = field.MaxItems ?? CharacterSystemLoader.DefaultMaxItems;
        var minItems = field.MinItems ?? 0;

        if (rowCount < minItems)
        {
            return $"{field.Label} needs at least {minItems} row{(minItems == 1 ? string.Empty : "s")}.";
        }

        if (rowCount > maxItems)
        {
            return $"{field.Label} allows at most {maxItems} row{(maxItems == 1 ? string.Empty : "s")}.";
        }

        return null;
    }

    private static string? ValidateNumber(StatFieldDefinition field, string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            return $"{field.Label} must be a number.";
        }

        if (field.Min.HasValue && value < field.Min.Value)
        {
            return $"{field.Label} must be at least {field.Min.Value}.";
        }

        if (field.Max.HasValue && value > field.Max.Value)
        {
            return $"{field.Label} must be at most {field.Max.Value}.";
        }

        return null;
    }

    private static string? ValidateText(StatFieldDefinition field, string trimmed, int defaultMaxLength, bool allowPattern)
    {
        var maxLength = field.MaxLength ?? defaultMaxLength;
        if (trimmed.Length > maxLength)
        {
            return $"{field.Label} must be at most {maxLength} characters.";
        }

        // `free-text-block` never sets `pattern` (CharacterSystemLoader rejects a pack that tries),
        // but `allowPattern: false` is defense in depth against that invariant ever slipping.
        if (!allowPattern || field.Pattern is null || trimmed.Length == 0)
        {
            return null;
        }

        // Same NonBacktracking dialect CharacterSystemLoader already validated this pattern compiles
        // under at pack-load time -- see SYSTEMS.md's "Resource limits" for why.
        var regex = new Regex(field.Pattern, RegexOptions.NonBacktracking);
        return regex.IsMatch(trimmed) ? null : $"{field.Label} doesn't match the required format.";
    }

    private static string? ValidateBoolean(string trimmed)
    {
        // The UI only ever produces bool.TrueString/FalseString via a CheckBox, so this branch is
        // unreachable in practice through the schema-aware form -- defense in depth for a Stats
        // value that arrived some other way (hand-edited JSON, an older/different client).
        return trimmed.Length == 0 || bool.TryParse(trimmed, out _) ? null : "Must be true or false.";
    }

    private static string? ValidateEnum(StatFieldDefinition field, string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (field.Options is null || !field.Options.Contains(trimmed, StringComparer.Ordinal))
        {
            return $"{field.Label} must be one of: {string.Join(", ", field.Options ?? [])}.";
        }

        return null;
    }
}