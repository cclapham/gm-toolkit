using System.Text.Json;

namespace GmToolkit.Core.Systems;

/// <summary>
/// Serializes/deserializes a <see cref="StatFieldTypes.RepeatingGroup"/> field's rows to/from the
/// single JSON-array-of-objects string SYSTEMS.md's storage section specifies: "Every field's value,
/// including a `repeating-group`'s rows (serialized as a JSON array under that field's key), is
/// stored as its string-serialized form in that same [Stats] dictionary." One row is a plain
/// <c>Dictionary&lt;string, string&gt;</c> keyed by the group's own <c>itemFields</c> keys -- exactly
/// the same "system-agnostic string bag" shape as <c>PlayerCharacter.Stats</c>/<c>Npc.Stats</c>
/// themselves, just nested one level.
/// </summary>
public static class RepeatingGroupCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Serializes <paramref name="rows"/> to a JSON array of objects, in order.</summary>
    public static string Serialize(IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return JsonSerializer.Serialize(rows, SerializerOptions);
    }

    /// <summary>
    /// Deserializes <paramref name="json"/> back to its rows, in order. Returns an empty list for
    /// <c>null</c>/blank/malformed input rather than throwing -- mirrors
    /// <c>PlayerCharacter.HasMalformedStats</c>'s "never make the whole character inaccessible over
    /// one bad field" philosophy, just scoped to a single repeating-group field instead of the whole
    /// Stats bag.
    /// </summary>
    public static IReadOnlyList<Dictionary<string, string>> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var rows = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json, SerializerOptions);
            return rows ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}