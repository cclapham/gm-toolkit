namespace GmToolkit.Core.Import;

/// <summary>
/// The full-fidelity, system-agnostic JSON export shape for a <see cref="Models.Campaign"/> and
/// everything nested under it (M5 — see ROADMAP.md). This is the type that gets serialized to and
/// deserialized from a <c>.json</c> campaign export file; its shape is documented for humans at
/// <c>Resources/Schemas/campaign-export-v1.schema.json</c>, and <see cref="CampaignExportJsonContext"/>
/// is the source-generated (reflection-free) serializer for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No database <see cref="Guid"/> anywhere in this type or its children.</b> A campaign's
/// <see cref="Name"/> (and each PC's/NPC's own name) is the stable identifier export/import uses
/// instead — re-importing always mints brand-new ids via <see cref="CampaignExportMapper"/>, so an
/// exported file can be freely copied to another machine, or re-imported into the same one, without
/// ever colliding with a live row's identity. See CONTRIBUTING.md's cascade-delete note for why this
/// project already treats "no database identity leaking outside the Data layer" as a settled
/// convention, not a new decision.
/// </para>
/// <para>
/// <b>No <c>CreatedUtc</c>/<c>LastOpenedUtc</c> either.</b> Those are bookkeeping about *this*
/// database's copy of the campaign, not part of the campaign's own content — <see cref="ExportedUtc"/>
/// is the one timestamp this format does carry, and it describes the export file itself (when it was
/// produced), not any entity inside it. It defaults to <see langword="default"/>(<see cref="DateTime"/>)
/// when not explicitly supplied (a compile-time-constant default is all a primary-constructor
/// parameter can have — see this type's remarks on why this is a primary-constructor record);
/// <see cref="CampaignExportMapper.ToDto"/> always sets it to <see cref="DateTime.UtcNow"/> for a
/// real export.
/// </para>
/// <para>
/// Every property is a plain, unvalidated value — see <see cref="PlayerCharacterExportDto"/>'s
/// remarks for why DTOs never route through the domain models' own validating setters, and for why
/// this is a primary-constructor <c>record</c> (every parameter defaulted) rather than init-only
/// auto-properties with field initializers — the latter silently loses its defaults under
/// source-generated JSON deserialization. Run <see cref="ImportValidator.ValidateCampaign"/> before
/// mapping a deserialized instance of this type with <see cref="CampaignExportMapper.ToModel"/>.
/// </para>
/// </remarks>
public sealed record CampaignExportDto(
    string Name = "",
    string GameSystem = "",
    string? CharacterSystemId = null,
    string Description = "",
    int FormatVersion = CampaignExportDto.CurrentFormatVersion,
    DateTime ExportedUtc = default,
    List<PlayerCharacterExportDto>? PlayerCharacters = null,
    List<NpcExportDto>? Npcs = null)
{
    /// <summary>
    /// The only <c>formatVersion</c> this build of GM Toolkit produces or accepts. Bumped whenever
    /// this export shape changes in a way an older reader couldn't handle — mirrors
    /// <see cref="Systems.CharacterSystem.FormatVersion"/>'s convention.
    /// </summary>
    public const int CurrentFormatVersion = 1;

    public List<PlayerCharacterExportDto> PlayerCharacters { get; init; } = PlayerCharacters ?? [];

    public List<NpcExportDto> Npcs { get; init; } = Npcs ?? [];
}