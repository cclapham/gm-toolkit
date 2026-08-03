using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Tests.Import;

public class ImportValidatorTests
{
    private static CharacterSystem MakeSystemWithNumberField(string key, decimal min, decimal max) => new()
    {
        FormatVersion = 1,
        Id = "test-system",
        Name = "Test System",
        PcFields = [new StatFieldDefinition { Key = key, Label = key, Type = StatFieldTypes.Number, Min = min, Max = max }],
        NpcFields = [new StatFieldDefinition { Key = key, Label = key, Type = StatFieldTypes.Number, Min = min, Max = max }],
    };

    private static ICharacterSystemRegistry MakeRegistry(params CharacterSystem[] systems) =>
        new CharacterSystemRegistry([GenericCharacterSystem.Instance, .. systems]);

    [Fact]
    public void ValidateCampaign_with_valid_dto_is_valid()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls" };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateCampaign_with_missing_name_is_invalid()
    {
        var dto = new CampaignExportDto { Name = string.Empty };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCampaign_with_name_over_max_length_is_invalid()
    {
        var dto = new CampaignExportDto { Name = new string('x', Campaign.NameMaxLength + 1) };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cannot exceed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCampaign_with_unrecognized_format_version_is_invalid()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls", FormatVersion = 999 };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("format version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCampaign_with_unknown_character_system_id_is_invalid_when_a_registry_is_supplied()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls", CharacterSystemId = "does-not-exist" };
        var registry = MakeRegistry();

        var result = ImportValidator.ValidateCampaign(dto, registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unknown character system", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCampaign_with_unknown_character_system_id_is_not_checked_without_a_registry()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls", CharacterSystemId = "does-not-exist" };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCampaign_with_known_character_system_id_is_valid()
    {
        var dto = new CampaignExportDto { Name = "Wandering Souls", CharacterSystemId = GenericCharacterSystem.Id };
        var registry = MakeRegistry();

        var result = ImportValidator.ValidateCampaign(dto, registry);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCampaign_with_out_of_range_ability_score_is_invalid_when_a_matching_system_is_attached()
    {
        var system = MakeSystemWithNumberField("strength", 1, 20);
        var registry = MakeRegistry(system);
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            CharacterSystemId = system.Id,
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan", Stats = { ["strength"] = "99" } }],
        };

        var result = ImportValidator.ValidateCampaign(dto, registry);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("strength", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateCampaign_with_in_range_ability_score_is_valid()
    {
        var system = MakeSystemWithNumberField("strength", 1, 20);
        var registry = MakeRegistry(system);
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            CharacterSystemId = system.Id,
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan", Stats = { ["strength"] = "14" } }],
        };

        var result = ImportValidator.ValidateCampaign(dto, registry);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCampaign_with_unrelated_stat_key_not_in_the_schema_is_ignored()
    {
        var system = MakeSystemWithNumberField("strength", 1, 20);
        var registry = MakeRegistry(system);
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            CharacterSystemId = system.Id,
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan", Stats = { ["homebrew-luck"] = "anything goes" } }],
        };

        var result = ImportValidator.ValidateCampaign(dto, registry);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateCampaign_collects_errors_from_every_nested_player_character_and_npc()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = string.Empty }],
            Npcs = [new NpcExportDto { Name = string.Empty }],
        };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void ValidateCampaign_warns_on_duplicate_player_character_names_but_stays_valid()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters =
            [
                new PlayerCharacterExportDto { CharacterName = "Brannigan" },
                new PlayerCharacterExportDto { CharacterName = "Brannigan" },
            ],
        };

        var result = ImportValidator.ValidateCampaign(dto);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Brannigan", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatePlayerCharacter_with_missing_name_is_invalid()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = "  " };

        var result = ImportValidator.ValidatePlayerCharacter(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidatePlayerCharacter_with_name_over_max_length_is_invalid()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = new string('x', PlayerCharacter.NameMaxLength + 1) };

        var result = ImportValidator.ValidatePlayerCharacter(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePlayerCharacter_with_negative_level_is_invalid()
    {
        var dto = new PlayerCharacterExportDto { CharacterName = "Brannigan", Level = -1 };

        var result = ImportValidator.ValidatePlayerCharacter(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("negative level", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateNpc_with_missing_name_is_invalid()
    {
        var dto = new NpcExportDto { Name = string.Empty };

        var result = ImportValidator.ValidateNpc(dto);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateNpc_with_name_over_max_length_is_invalid()
    {
        var dto = new NpcExportDto { Name = new string('x', Npc.NameMaxLength + 1) };

        var result = ImportValidator.ValidateNpc(dto);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateNpc_with_valid_name_is_valid()
    {
        var dto = new NpcExportDto { Name = "Old Marta" };

        var result = ImportValidator.ValidateNpc(dto);

        Assert.True(result.IsValid);
    }
}