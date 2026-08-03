using System.Text.Json.Serialization;

namespace GmToolkit.Core.Import;

/// <summary>
/// Source-generated <see cref="JsonSerializerContext"/> for the campaign export DTOs — reflection-
/// free (de)serialization, matching how <c>Systems.CharacterSystemLoader</c>'s embedded system packs
/// use camelCase property names for a portable, hand-editable JSON file. Deliberately not reflection-
/// based <see cref="System.Text.Json.JsonSerializer"/> usage: the Android head runs under Mono, where
/// reflection-based serialization works but is slower and allocates more than a source-generated
/// contract, and this is the one Core type intended to round-trip through an actual file a GM might
/// hand-edit or send to another machine, so it's worth getting right from the start.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CampaignExportDto))]
[JsonSerializable(typeof(PlayerCharacterExportDto))]
[JsonSerializable(typeof(NpcExportDto))]
public partial class CampaignExportJsonContext : JsonSerializerContext
{
}