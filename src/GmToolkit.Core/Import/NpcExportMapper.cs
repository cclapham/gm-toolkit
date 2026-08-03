using GmToolkit.Core.Models;

namespace GmToolkit.Core.Import;

/// <summary>Maps between <see cref="Npc"/> and its export DTO.</summary>
public static class NpcExportMapper
{
    public static NpcExportDto ToDto(Npc npc)
    {
        ArgumentNullException.ThrowIfNull(npc);

        return new NpcExportDto
        {
            Name = npc.Name,
            Role = npc.Role,
            Faction = npc.Faction,
            Location = npc.Location,
            Appearance = npc.Appearance,
            Mannerism = npc.Mannerism,
            Motivation = npc.Motivation,
            Secret = npc.Secret,
            Notes = npc.Notes,
            KnownToPlayers = npc.KnownToPlayers,
            WasGenerated = npc.WasGenerated,
            Stats = new Dictionary<string, string>(npc.Stats),
        };
    }

    /// <summary>
    /// Builds an <see cref="Npc"/> from an import DTO under <paramref name="campaignId"/>. Mints a
    /// fresh <see cref="Npc.Id"/> and <see cref="Npc.CreatedUtc"/> unless <paramref name="id"/> is
    /// supplied — see <see cref="PlayerCharacterExportMapper.ToModel"/>'s remarks, which this mirrors.
    /// An overwrite-by-id still gets a new <see cref="Npc.CreatedUtc"/>, since
    /// <see cref="NpcExportDto"/> deliberately doesn't carry the original one (see its remarks).
    /// </summary>
    /// <remarks>
    /// Assumes <paramref name="dto"/> already passed <see cref="ImportValidator.ValidateNpc"/> — an
    /// invalid <see cref="NpcExportDto.Name"/> still throws here, via <see cref="Npc.Name"/>'s own
    /// validating setter.
    /// </remarks>
    public static Npc ToModel(NpcExportDto dto, Guid campaignId, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Npc
        {
            Id = id ?? Guid.NewGuid(),
            CampaignId = campaignId,
            Name = dto.Name,
            Role = dto.Role,
            Faction = dto.Faction,
            Location = dto.Location,
            Appearance = dto.Appearance,
            Mannerism = dto.Mannerism,
            Motivation = dto.Motivation,
            Secret = dto.Secret,
            Notes = dto.Notes,
            KnownToPlayers = dto.KnownToPlayers,
            WasGenerated = dto.WasGenerated,
            Stats = new Dictionary<string, string>(dto.Stats),
        };
    }
}