using GmToolkit.Core.Models;

namespace GmToolkit.Core.Import;

/// <summary>
/// The outcome of <c>GmToolkit.Data.Repositories.CampaignRepository.ImportCampaignAsync</c>: either
/// the newly-created <see cref="Campaign"/> (with its imported <see cref="Campaign.PlayerCharacters"/>/
/// <see cref="Campaign.Npcs"/> populated), or <see cref="Validation"/> explaining why nothing was
/// written — e.g. an invalid DTO, or a name conflict when <c>overwrite</c> was <c>false</c>. A
/// whole-campaign import is all-or-nothing: it either fully succeeds, or the database is left exactly
/// as it was before the call, never partially imported.
/// </summary>
public sealed record CampaignImportResult(Campaign? Campaign, ValidationResult Validation)
{
    public bool Succeeded => Campaign is not null;

    public static CampaignImportResult Success(Campaign campaign) => new(campaign, ValidationResult.Success());

    public static CampaignImportResult Failure(ValidationResult validation) => new(null, validation);

    public static CampaignImportResult Failure(string error) => new(null, ValidationResult.Failure(error));
}