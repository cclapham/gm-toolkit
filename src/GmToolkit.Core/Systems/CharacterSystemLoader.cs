using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using GmToolkit.Core.Systems.Formula;

namespace GmToolkit.Core.Systems;

/// <summary>
/// Loads and validates <see cref="CharacterSystem"/> packs, both from embedded JSON resources
/// (under <c>Resources/CharacterSystems/*.json</c> — see the <c>GmToolkit.Core.csproj</c>
/// <c>EmbeddedResource</c> items, mirroring <c>GeneratorTableLoader</c>'s convention) and, via
/// <see cref="Validate"/>, for any in-memory pack (e.g. <see cref="GenericCharacterSystem.Instance"/>).
/// Implements SYSTEMS.md's "Load-time validation checklist" in full — every one of its nine
/// numbered items — so a hostile or malformed pack is rejected with a clear
/// <see cref="CharacterSystemLoadException"/> rather than ever reaching a character's runtime
/// evaluation path.
/// </summary>
public static class CharacterSystemLoader
{
    /// <summary>The logical resource name prefix assigned to every embedded character system file.</summary>
    public const string ResourceNamePrefix = "GmToolkit.Core.CharacterSystems.";

    /// <summary>The only <c>formatVersion</c> this engine currently recognizes.</summary>
    public const int SupportedFormatVersion = 1;

    /// <summary>Checklist item 2: max length of a <see cref="CharacterSystem.Id"/>.</summary>
    public const int MaxSystemIdLength = 64;

    /// <summary>Checklist item 5/6: hard ceiling on any <c>text</c>/<c>free-text-block</c> <c>maxLength</c>, regardless of what a pack declares.</summary>
    public const int MaxTextMaxLength = 10_000;

    /// <summary>SYSTEMS.md's <c>text</c> field type: default <c>maxLength</c> when unset.</summary>
    public const int DefaultTextMaxLength = 500;

    /// <summary>SYSTEMS.md's <c>free-text-block</c> field type: default <c>maxLength</c> when unset.</summary>
    public const int DefaultFreeTextBlockMaxLength = 4000;

    /// <summary>Checklist item 5: hard ceiling on a <c>pattern</c> string's own length.</summary>
    public const int MaxPatternLength = 200;

    /// <summary>Checklist item 6: hard ceiling on a <c>repeating-group</c>'s <c>itemFields</c> count.</summary>
    public const int MaxItemFieldsPerGroup = 50;

    /// <summary>Checklist item 6: hard ceiling on a <c>repeating-group</c>'s row count, regardless of what a pack's <c>maxItems</c> declares.</summary>
    public const int MaxRepeatingGroupRows = 1_000;

    /// <summary>SYSTEMS.md's <c>repeating-group</c> field type: engine default row-count ceiling when <c>maxItems</c> is unset.</summary>
    public const int DefaultMaxItems = 100;

    /// <summary>Checklist item 6: hard ceiling on top-level field *definitions* per <c>pcFields</c>/<c>npcFields</c>.</summary>
    public const int MaxTopLevelFieldDefinitions = 200;

    /// <summary>Checklist item 6: hard ceiling on aggregate field *instances* across <c>pcFields</c> + <c>npcFields</c> combined.</summary>
    public const int MaxAggregateFieldInstances = 10_000;

    private static readonly Regex SystemIdPattern = new("^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FieldKeyPattern = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads and validates every embedded character system pack found in <paramref name="assembly"/>
    /// (defaults to the assembly containing this loader). Unlike <c>GeneratorTableLoader.LoadAll</c>,
    /// it is not an error for zero packs to be embedded — #83 ships the engine with no in-box
    /// system content (that's #84-#87); <see cref="CharacterSystemRegistry.FromEmbeddedSystems"/>
    /// always has at least <see cref="GenericCharacterSystem.Instance"/> regardless.
    /// </summary>
    public static IReadOnlyList<CharacterSystem> LoadAll(Assembly? assembly = null)
    {
        assembly ??= typeof(CharacterSystemLoader).Assembly;

        var resourceNames = GetSystemResourceNames(assembly);
        var systems = new List<CharacterSystem>(resourceNames.Count);
        foreach (var resourceName in resourceNames)
        {
            systems.Add(LoadResource(assembly, resourceName));
        }

        return systems;
    }

    /// <summary>Returns the logical names of every embedded character system resource, sorted for deterministic ordering.</summary>
    public static IReadOnlyList<string> GetSystemResourceNames(Assembly assembly)
    {
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourceNamePrefix, StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Loads and validates a single embedded character system pack by its logical resource name.</summary>
    public static CharacterSystem LoadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new CharacterSystemLoadException(
                $"Embedded character system resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.");

        CharacterSystem? system;
        try
        {
            system = JsonSerializer.Deserialize<CharacterSystem>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new CharacterSystemLoadException($"Character system '{resourceName}' contains malformed JSON: {ex.Message}", ex);
        }

        if (system is null)
        {
            throw new CharacterSystemLoadException($"Character system '{resourceName}' deserialized to nothing (JSON 'null').");
        }

        Validate(system, resourceName);
        return system;
    }

    /// <summary>
    /// Runs SYSTEMS.md's complete "Load-time validation checklist" against <paramref name="system"/>.
    /// Throws <see cref="CharacterSystemLoadException"/> — never any other exception type — on the
    /// first rule violated. <paramref name="context"/> is a human-readable identifier (a resource
    /// name, or e.g. "generic (built-in)") included in every failure message.
    /// </summary>
    /// <remarks>
    /// Does not check <see cref="CharacterSystem.Id"/> collisions against other installed systems —
    /// that requires knowing the full installed set, which only <see cref="CharacterSystemRegistry"/>
    /// has (see its constructor).
    /// </remarks>
    public static void Validate(CharacterSystem system, string context)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        // Checklist item 1: formatVersion recognition.
        if (system.FormatVersion != SupportedFormatVersion)
        {
            throw new CharacterSystemLoadException(
                $"{context}: unrecognized formatVersion {system.FormatVersion} (this client only recognizes formatVersion {SupportedFormatVersion}).");
        }

        // Checklist item 2: id format (collision is checked by CharacterSystemRegistry, which alone
        // knows the full installed set).
        if (string.IsNullOrEmpty(system.Id) || system.Id.Length > MaxSystemIdLength || !SystemIdPattern.IsMatch(system.Id))
        {
            throw new CharacterSystemLoadException(
                $"{context}: 'id' \"{system.Id}\" is invalid; it must match ^[a-z0-9][a-z0-9-]*$ and be at most {MaxSystemIdLength} characters.");
        }

        // Checklist items 3-6 (field key format/uniqueness, enum options, maxLength/pattern
        // ceilings, repeating-group nesting/ceilings, top-level field-definition count).
        var pcInstanceCount = ValidateFieldDefinitions(system.PcFields, "pcFields", context);
        var npcInstanceCount = ValidateFieldDefinitions(system.NpcFields, "npcFields", context);

        // Checklist item 6 (continued): aggregate field-instance ceiling, combined across scopes.
        var aggregateInstanceCount = pcInstanceCount + npcInstanceCount;
        if (aggregateInstanceCount > MaxAggregateFieldInstances)
        {
            throw new CharacterSystemLoadException(
                $"{context}: pcFields + npcFields combined have an aggregate field-instance count of {aggregateInstanceCount}, exceeding the {MaxAggregateFieldInstances} hard ceiling.");
        }

        // Checklist item 7 was already enforced per-field inside ValidateFieldDefinitions (every
        // `derived` field's formula is parsed there, which itself enforces the 500-character/
        // 32-level bounds and full-parse completeness).

        // Checklist item 8: formula scope-reference validity, checked per scope.
        ValidateFormulaReferences(system.PcFields, "pcFields", context);
        ValidateFormulaReferences(system.NpcFields, "npcFields", context);

        // Checklist item 9: dependency-cycle/chain-depth check, checked per scope.
        ValidateDependencyGraph(system.PcFields, "pcFields", context);
        ValidateDependencyGraph(system.NpcFields, "npcFields", context);
    }

    private static long ValidateFieldDefinitions(IReadOnlyList<StatFieldDefinition> fields, string scopeName, string context)
    {
        if (fields.Count > MaxTopLevelFieldDefinitions)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} has {fields.Count} top-level field definitions, exceeding the {MaxTopLevelFieldDefinitions}-definition hard ceiling.");
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        long total = 0;

        foreach (var field in fields)
        {
            if (!seenKeys.Add(field.Key))
            {
                throw new CharacterSystemLoadException($"{context}: {scopeName} has a duplicate top-level field key '{field.Key}'.");
            }

            ValidateField(field, scopeName, context, isItemField: false);
            total += ComputeInstanceCount(field);
        }

        return total;
    }

    private static void ValidateField(StatFieldDefinition field, string scopeName, string context, bool isItemField)
    {
        // Checklist item 3: key format.
        if (string.IsNullOrEmpty(field.Key) || !FieldKeyPattern.IsMatch(field.Key))
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field key \"{field.Key}\" is invalid; keys must match ^[a-zA-Z_][a-zA-Z0-9_]*$.");
        }

        switch (field.Type)
        {
            case StatFieldTypes.Number:
                ValidateNumberField(field, scopeName, context);
                break;

            case StatFieldTypes.Text:
                ValidateTextField(field, scopeName, context);
                break;

            case StatFieldTypes.Boolean:
                break;

            case StatFieldTypes.Enum:
                ValidateEnumField(field, scopeName, context);
                break;

            case StatFieldTypes.Derived:
                // `derived` is top-level-only -- see SYSTEMS.md's "Scope resolution".
                if (isItemField)
                {
                    throw new CharacterSystemLoadException(
                        $"{context}: {scopeName} repeating-group item field '{field.Key}' is type 'derived', which is top-level-only and may never appear inside a repeating-group's itemFields.");
                }

                ValidateDerivedField(field, scopeName, context);
                break;

            case StatFieldTypes.RepeatingGroup:
                if (isItemField)
                {
                    throw new CharacterSystemLoadException(
                        $"{context}: {scopeName} repeating-group item field '{field.Key}' is itself type 'repeating-group'; nested repeating-groups are not allowed.");
                }

                ValidateRepeatingGroupField(field, scopeName, context);
                break;

            case StatFieldTypes.FreeTextBlock:
                ValidateFreeTextBlockField(field, scopeName, context);
                break;

            default:
                throw new CharacterSystemLoadException($"{context}: {scopeName} field '{field.Key}' has unrecognized type \"{field.Type}\".");
        }
    }

    private static void ValidateNumberField(StatFieldDefinition field, string scopeName, string context)
    {
        if (field.Min.HasValue && field.Max.HasValue && field.Min.Value > field.Max.Value)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' has min ({field.Min}) greater than max ({field.Max}).");
        }
    }

    private static void ValidateTextField(StatFieldDefinition field, string scopeName, string context)
    {
        // Checklist item 5: maxLength ceiling (hard ceiling applies regardless of a pack's own default).
        var maxLength = field.MaxLength ?? DefaultTextMaxLength;
        if (maxLength <= 0 || maxLength > MaxTextMaxLength)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' has maxLength {maxLength}, which must be positive and at most {MaxTextMaxLength}.");
        }

        if (field.Pattern is null)
        {
            return;
        }

        // Checklist item 5: a field that sets `pattern` must also set `maxLength` explicitly.
        if (field.MaxLength is null)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' sets 'pattern' but not 'maxLength'; a field that sets 'pattern' must also set 'maxLength'.");
        }

        // Checklist item 5: pattern length ceiling.
        if (field.Pattern.Length > MaxPatternLength)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' has a 'pattern' of {field.Pattern.Length} characters, exceeding the {MaxPatternLength}-character maximum.");
        }

        // Checklist item 5: pattern must compile under RegexOptions.NonBacktracking; a pattern that
        // doesn't throws NotSupportedException specifically, which must be caught here rather than
        // allowed to escape as an unhandled exception.
        try
        {
            _ = new Regex(field.Pattern, RegexOptions.NonBacktracking);
        }
        catch (NotSupportedException ex)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' has a 'pattern' that fails to compile under RegexOptions.NonBacktracking: {ex.Message}", ex);
        }
    }

    private static void ValidateEnumField(StatFieldDefinition field, string scopeName, string context)
    {
        // Checklist item 4.
        if (field.Options is null || field.Options.Count == 0)
        {
            throw new CharacterSystemLoadException($"{context}: {scopeName} enum field '{field.Key}' has an empty (or missing) 'options' list.");
        }
    }

    private static void ValidateDerivedField(StatFieldDefinition field, string scopeName, string context)
    {
        if (string.IsNullOrEmpty(field.Formula))
        {
            throw new CharacterSystemLoadException($"{context}: {scopeName} derived field '{field.Key}' is missing a required 'formula'.");
        }

        // Checklist item 7: formula length/nesting-depth/parse-completeness, all enforced inside
        // FormulaParser.Parse itself.
        try
        {
            FormulaParser.Parse(field.Formula);
        }
        catch (FormulaParseException ex)
        {
            throw new CharacterSystemLoadException($"{context}: {scopeName} derived field '{field.Key}' has an invalid formula: {ex.Message}", ex);
        }

        if (field.Rounding is not null && !RoundingModes.All.Contains(field.Rounding))
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} derived field '{field.Key}' has an unrecognized 'rounding' value \"{field.Rounding}\".");
        }

        if (field.Min.HasValue && field.Max.HasValue && field.Min.Value > field.Max.Value)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} derived field '{field.Key}' has min ({field.Min}) greater than max ({field.Max}).");
        }
    }

    private static void ValidateRepeatingGroupField(StatFieldDefinition field, string scopeName, string context)
    {
        if (field.ItemFields is null || field.ItemFields.Count == 0)
        {
            throw new CharacterSystemLoadException($"{context}: {scopeName} repeating-group field '{field.Key}' has no 'itemFields' (must be non-empty).");
        }

        // Checklist item 6: itemFields-per-group ceiling.
        if (field.ItemFields.Count > MaxItemFieldsPerGroup)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} repeating-group field '{field.Key}' has {field.ItemFields.Count} itemFields, exceeding the {MaxItemFieldsPerGroup}-field-per-group hard ceiling.");
        }

        // Checklist item 6: row-count ceiling, regardless of what the pack declares (or the 100 default).
        var maxItems = field.MaxItems ?? DefaultMaxItems;
        if (maxItems <= 0 || maxItems > MaxRepeatingGroupRows)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} repeating-group field '{field.Key}' has maxItems {maxItems}, which must be positive and at most {MaxRepeatingGroupRows}.");
        }

        if (field.MinItems.HasValue && (field.MinItems.Value < 0 || field.MinItems.Value > maxItems))
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} repeating-group field '{field.Key}' has an invalid minItems ({field.MinItems}) relative to maxItems ({maxItems}).");
        }

        // Checklist item 3 (continued): itemFields keys unique within this group's own scope.
        var itemKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemField in field.ItemFields)
        {
            if (!itemKeys.Add(itemField.Key))
            {
                throw new CharacterSystemLoadException(
                    $"{context}: {scopeName} repeating-group field '{field.Key}' has a duplicate itemFields key '{itemField.Key}'.");
            }

            // Checklist item 6: no nested repeating-group, no derived -- enforced inside ValidateField
            // via isItemField: true.
            ValidateField(itemField, scopeName, context, isItemField: true);
        }
    }

    private static void ValidateFreeTextBlockField(StatFieldDefinition field, string scopeName, string context)
    {
        // Checklist item 5: maxLength ceiling.
        var maxLength = field.MaxLength ?? DefaultFreeTextBlockMaxLength;
        if (maxLength <= 0 || maxLength > MaxTextMaxLength)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' has maxLength {maxLength}, which must be positive and at most {MaxTextMaxLength}.");
        }

        if (field.Pattern is not null)
        {
            throw new CharacterSystemLoadException(
                $"{context}: {scopeName} field '{field.Key}' is type 'free-text-block' and may not set 'pattern' (pattern is only supported on 'text' fields).");
        }
    }

    private static long ComputeInstanceCount(StatFieldDefinition field)
    {
        if (field.Type != StatFieldTypes.RepeatingGroup)
        {
            return 1;
        }

        var maxItems = field.MaxItems ?? DefaultMaxItems;
        var itemFieldCount = field.ItemFields?.Count ?? 0;
        return (long)maxItems * itemFieldCount;
    }

    private static void ValidateFormulaReferences(IReadOnlyList<StatFieldDefinition> fields, string scopeName, string context)
    {
        var validKeys = fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (field.Type != StatFieldTypes.Derived)
            {
                continue;
            }

            FormulaNode ast;
            try
            {
                ast = FormulaParser.Parse(field.Formula ?? string.Empty);
            }
            catch (FormulaParseException ex)
            {
                throw new CharacterSystemLoadException($"{context}: {scopeName} derived field '{field.Key}' has an invalid formula: {ex.Message}", ex);
            }

            foreach (var referencedKey in FormulaNode.CollectFieldReferences(ast))
            {
                if (!validKeys.Contains(referencedKey))
                {
                    // Checklist item 8: an unknown key, or a key that only exists inside a
                    // repeating-group's rows (which is simply never in `validKeys`, since
                    // `validKeys` only contains this scope's *top-level* keys).
                    throw new CharacterSystemLoadException(
                        $"{context}: {scopeName} derived field '{field.Key}' formula references unknown field '{referencedKey}'.");
                }
            }
        }
    }

    private static void ValidateDependencyGraph(IReadOnlyList<StatFieldDefinition> fields, string scopeName, string context)
    {
        try
        {
            DerivedFieldGraph.Build(fields);
        }
        catch (DerivedFieldGraphException ex)
        {
            throw new CharacterSystemLoadException($"{context}: {scopeName} {ex.Message}", ex);
        }
    }
}