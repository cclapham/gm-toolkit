using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Import;

/// <summary>
/// Validates deserialized import DTOs before they're mapped into domain models and written to the
/// database — the boundary between "arbitrary bytes from a file a GM double-clicked" and "trusted
/// enough to hand to <see cref="CampaignExportMapper.ToModel"/>/<see cref="PlayerCharacterExportMapper.ToModel"/>/
/// <see cref="NpcExportMapper.ToModel"/>", whose own validating setters would otherwise be the first
/// (exception-throwing, not readable-error-returning) line of defense.
/// </summary>
/// <remarks>
/// <para>
/// <b>Name/length rules mirror the domain models' own setters exactly</b> (<see cref="Campaign.NameMaxLength"/>,
/// <see cref="PlayerCharacter.NameMaxLength"/>, <see cref="Npc.NameMaxLength"/>) — this is
/// deliberately the same rule stated twice, not a new one: <see cref="Campaign.Name"/>'s setter is
/// still the single source of truth for what makes a name valid once an entity actually exists in
/// memory, but a validator that only threw <see cref="ArgumentException"/> couldn't collect every
/// problem with a whole imported campaign (name too long *and* a bad ability score *and* an unknown
/// system id) into one readable report — it could only ever report the first one, and would abort
/// mapping before even reaching the rest.
/// </para>
/// <para>
/// <b><paramref name="systemRegistry"/> is optional everywhere it appears.</b> Passing <c>null</c>
/// simply skips the checks that need it (an attached <see cref="Models.Campaign.CharacterSystemId"/>
/// actually existing, and each stat value's min/max/pattern/enum-membership against that system's
/// field definitions) rather than failing closed — the repository layer (<c>GmToolkit.Data</c>) has
/// no <see cref="ICharacterSystemRegistry"/> to inject (see CONTRIBUTING.md's dependency-direction
/// rule: <c>Data</c> doesn't reach into <c>Core.Systems</c> beyond what it already needs), so its own
/// import methods call these overloads without one; a caller that does have a registry (the eventual
/// #130 import UI, already DI-wired to one) gets the deeper check for free by passing it in.
/// </para>
/// </remarks>
public static class ImportValidator
{
    /// <summary>
    /// Validates a whole campaign export, including every nested player character and NPC. See
    /// <see cref="ValidatePlayerCharacter"/>/<see cref="ValidateNpc"/> for what's checked on each.
    /// </summary>
    public static ValidationResult ValidateCampaign(CampaignExportDto dto, ICharacterSystemRegistry? systemRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errors = new List<string>();
        var warnings = new List<string>();

        if (dto.FormatVersion != CampaignExportDto.CurrentFormatVersion)
        {
            errors.Add(
                $"Unsupported export format version {dto.FormatVersion} (this build reads version " +
                $"{CampaignExportDto.CurrentFormatVersion}).");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("Campaign name is required.");
        }
        else if (dto.Name.Length > Campaign.NameMaxLength)
        {
            errors.Add($"Campaign name cannot exceed {Campaign.NameMaxLength} characters.");
        }

        CharacterSystem? system = null;
        if (dto.CharacterSystemId is not null && systemRegistry is not null
            && !systemRegistry.TryGetById(dto.CharacterSystemId, out system))
        {
            errors.Add($"Campaign references unknown character system '{dto.CharacterSystemId}'.");
        }

        AddDuplicateNameWarnings(dto.PlayerCharacters.Select(pc => pc.CharacterName), "player character", warnings);
        AddDuplicateNameWarnings(dto.Npcs.Select(npc => npc.Name), "NPC", warnings);

        var results = new List<ValidationResult> { new(errors, warnings) };
        results.AddRange(dto.PlayerCharacters.Select(pc => ValidatePlayerCharacter(pc, system)));
        results.AddRange(dto.Npcs.Select(npc => ValidateNpc(npc, system)));

        return ValidationResult.Merge(results);
    }

    /// <summary>
    /// Validates a single player character export: name required/length, a non-negative level, and
    /// (when <paramref name="system"/> is supplied) every stat value against that system's
    /// <see cref="CharacterSystem.PcFields"/> — this is where an out-of-range ability score (a
    /// <c>number</c> field's <see cref="StatFieldDefinition.Min"/>/<see cref="StatFieldDefinition.Max"/>)
    /// gets caught. Stat keys with no matching field definition are left alone: the stats bag is
    /// system-agnostic by design (see <see cref="PlayerCharacter.Stats"/>'s remarks), so an extra key
    /// a schema doesn't know about is not itself an error.
    /// </summary>
    public static ValidationResult ValidatePlayerCharacter(PlayerCharacterExportDto dto, CharacterSystem? system = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.CharacterName))
        {
            errors.Add("Player character name is required.");
        }
        else if (dto.CharacterName.Length > PlayerCharacter.NameMaxLength)
        {
            errors.Add($"Player character '{dto.CharacterName}' name cannot exceed {PlayerCharacter.NameMaxLength} characters.");
        }

        if (dto.Level < 0)
        {
            errors.Add($"Player character '{dto.CharacterName}' has a negative level ({dto.Level}).");
        }

        ValidateStats(dto.Stats, system?.PcFields, dto.CharacterName, errors);

        return new ValidationResult(errors, []);
    }

    /// <summary>
    /// Validates a single NPC export: name required/length, and (when <paramref name="system"/> is
    /// supplied) every stat value against that system's <see cref="CharacterSystem.NpcFields"/> —
    /// see <see cref="ValidatePlayerCharacter"/>'s remarks, which this mirrors.
    /// </summary>
    public static ValidationResult ValidateNpc(NpcExportDto dto, CharacterSystem? system = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add("NPC name is required.");
        }
        else if (dto.Name.Length > Npc.NameMaxLength)
        {
            errors.Add($"NPC '{dto.Name}' name cannot exceed {Npc.NameMaxLength} characters.");
        }

        ValidateStats(dto.Stats, system?.NpcFields, dto.Name, errors);

        return new ValidationResult(errors, []);
    }

    private static void ValidateStats(
        IReadOnlyDictionary<string, string> stats,
        IReadOnlyList<StatFieldDefinition>? fields,
        string entityName,
        List<string> errors)
    {
        if (fields is null || fields.Count == 0)
        {
            return;
        }

        var fieldsByKey = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        foreach (var (key, value) in stats)
        {
            if (!fieldsByKey.TryGetValue(key, out var field))
            {
                // No matching field definition for this key -- not an error; see this method's
                // remarks on the ValidatePlayerCharacter/ValidateNpc doc comments.
                continue;
            }

            var error = StatFieldValidator.ValidateValue(field, value);
            if (error is not null)
            {
                errors.Add(string.IsNullOrWhiteSpace(entityName) ? error : $"{entityName}: {error}");
            }
        }
    }

    private static void AddDuplicateNameWarnings(IEnumerable<string> names, string kind, List<string> warnings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!seen.Add(name))
            {
                warnings.Add($"More than one {kind} is named '{name}'.");
            }
        }
    }
}