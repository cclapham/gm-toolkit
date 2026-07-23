using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// The M1 exit criterion, written as a test: a full campaign aggregate (4 PCs, 6 NPCs) survives
/// a genuine dispose-and-reconnect cycle against a real on-disk SQLite file, cascade delete
/// removes every child row, and schema creation applies cleanly to a brand-new file.
/// </summary>
/// <remarks>
/// Deliberately does not hold a shared, always-open <see cref="GmToolkitDatabase"/> across the
/// whole test the way the per-repository test fixtures do — each test here owns its own
/// connection lifecycle explicitly, since proving data survives a real dispose-then-reconnect
/// cycle (rather than just living in one open connection's cache) is the entire point.
/// </remarks>
public sealed class CampaignRoundTripTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Full_campaign_round_trips_through_a_fresh_connection_and_cascade_deletes()
    {
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            GameSystem = "D&D 5e",
            Description = "A haunted coastal town slowly being reclaimed by the tide.",
            CreatedUtc = new DateTime(2024, 3, 1, 9, 0, 0, DateTimeKind.Utc),
            LastOpenedUtc = new DateTime(2024, 6, 15, 21, 30, 0, DateTimeKind.Utc),
        };

        var playerCharacters = BuildPlayerCharacters(campaign.Id);
        var npcs = BuildNpcs(campaign.Id);

        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();

            var campaignRepository = new CampaignRepository(writeDatabase);
            var pcRepository = new PlayerCharacterRepository(writeDatabase);
            var npcRepository = new NpcRepository(writeDatabase);

            await campaignRepository.AddAsync(campaign);
            foreach (var pc in playerCharacters)
            {
                await pcRepository.AddAsync(pc);
            }

            foreach (var npc in npcs)
            {
                await npcRepository.AddAsync(npc);
            }
        }

        // The `await using` block above disposed the connection entirely. A fresh connection
        // against the same file path proves the data actually persisted to disk rather than
        // merely living in the first connection's cache.
        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();

        var freshCampaignRepository = new CampaignRepository(readDatabase);
        var freshPcRepository = new PlayerCharacterRepository(readDatabase);
        var freshNpcRepository = new NpcRepository(readDatabase);

        var fetchedCampaign = await freshCampaignRepository.GetAsync(campaign.Id);

        Assert.NotNull(fetchedCampaign);
        Assert.Equal(campaign.Id, fetchedCampaign.Id);
        Assert.Equal(campaign.Name, fetchedCampaign.Name);
        Assert.Equal(campaign.GameSystem, fetchedCampaign.GameSystem);
        Assert.Equal(campaign.Description, fetchedCampaign.Description);
        Assert.Equal(campaign.CreatedUtc, fetchedCampaign.CreatedUtc);
        Assert.Equal(campaign.LastOpenedUtc, fetchedCampaign.LastOpenedUtc);

        Assert.Equal(playerCharacters.Count, fetchedCampaign.PlayerCharacters.Count);
        Assert.Equal(npcs.Count, fetchedCampaign.Npcs.Count);

        // Order is not guaranteed by GetAsync — match children back to their expected values by
        // Id rather than assuming list order.
        var fetchedPcsById = fetchedCampaign.PlayerCharacters.ToDictionary(pc => pc.Id);
        foreach (var expected in playerCharacters)
        {
            Assert.True(fetchedPcsById.TryGetValue(expected.Id, out var actual), $"Missing PC {expected.Id} ({expected.CharacterName}) after round trip.");
            AssertPlayerCharacterEqual(expected, actual!);
        }

        var fetchedNpcsById = fetchedCampaign.Npcs.ToDictionary(npc => npc.Id);
        foreach (var expected in npcs)
        {
            Assert.True(fetchedNpcsById.TryGetValue(expected.Id, out var actual), $"Missing NPC {expected.Id} ({expected.Name}) after round trip.");
            AssertNpcEqual(expected, actual!);
        }

        // Also verify the same round trip through the per-aggregate repositories directly,
        // independent of Campaign.GetAsync's own child-loading.
        var fetchedPcsDirect = await freshPcRepository.GetByCampaignAsync(campaign.Id);
        var fetchedNpcsDirect = await freshNpcRepository.GetByCampaignAsync(campaign.Id);
        Assert.Equal(playerCharacters.Count, fetchedPcsDirect.Count);
        Assert.Equal(npcs.Count, fetchedNpcsDirect.Count);

        // Cascade delete: removing the campaign must remove every PC and NPC that belongs to it.
        await freshCampaignRepository.DeleteAsync(campaign.Id);

        Assert.Null(await freshCampaignRepository.GetAsync(campaign.Id));
        Assert.Empty(await freshPcRepository.GetByCampaignAsync(campaign.Id));
        Assert.Empty(await freshNpcRepository.GetByCampaignAsync(campaign.Id));

        foreach (var pc in playerCharacters)
        {
            Assert.Null(await freshPcRepository.GetAsync(pc.Id));
        }

        foreach (var npc in npcs)
        {
            Assert.Null(await freshNpcRepository.GetAsync(npc.Id));
        }
    }

    [Fact]
    public async Task InitializeAsync_applies_cleanly_to_a_brand_new_file_and_is_immediately_usable()
    {
        var freshPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        Assert.False(File.Exists(freshPath));

        await using var freshDatabase = new GmToolkitDatabase(freshPath);
        try
        {
            var exception = await Record.ExceptionAsync(freshDatabase.InitializeAsync);
            Assert.Null(exception);

            var campaignRepository = new CampaignRepository(freshDatabase);
            var campaign = new Campaign { Name = "Freshly Initialized Campaign" };

            await campaignRepository.AddAsync(campaign);
            var fetched = await campaignRepository.GetAsync(campaign.Id);

            Assert.NotNull(fetched);
            Assert.Equal(campaign.Name, fetched.Name);
        }
        finally
        {
            if (File.Exists(freshPath))
            {
                File.Delete(freshPath);
            }
        }
    }

    private static List<PlayerCharacter> BuildPlayerCharacters(Guid campaignId) =>
    [
        new()
        {
            CampaignId = campaignId,
            CharacterName = "Brannigan Frost",
            PlayerName = "Alex",
            Ancestry = "Half-Orc",
            Class = "Barbarian",
            Level = 5,
            Notes = "Distrustful of magic.",
            Stats = new Dictionary<string, string>
            {
                ["STR"] = "18",
                ["DEX"] = "12",
                ["HP"] = "52",
                ["Passive Perception"] = "13",
            },
        },
        new()
        {
            CampaignId = campaignId,
            CharacterName = "Sera Nightingale",
            PlayerName = "Jamie",
            Ancestry = "Elf",
            Class = "Wizard",
            Level = 4,
            Notes = "Keeps a journal of every spell she's seen cast.",
            Stats = new Dictionary<string, string>
            {
                ["INT"] = "17",
                ["HP"] = "24",
                ["Spell Save DC"] = "14",
            },
        },
        new()
        {
            CampaignId = campaignId,
            CharacterName = "Doctor Wren Halloway",
            PlayerName = "Priya",
            Ancestry = "Human",
            Class = "Investigator",
            Level = 1,
            Notes = "Call of Cthulhu character — SAN loss so far: 12.",
            Stats = new Dictionary<string, string>
            {
                ["SAN"] = "58",
                ["Idea"] = "70",
                ["Luck"] = "45",
            },
        },
        new()
        {
            CampaignId = campaignId,
            CharacterName = "Six",
            PlayerName = "Morgan",
            Ancestry = string.Empty,
            Class = "Cutter",
            Level = 0,
            Notes = "Blades in the Dark — homebrew stress track at 6/9.",
            Stats = new Dictionary<string, string>
            {
                ["Insight"] = "2",
                ["Prowess"] = "3",
                ["Resolve"] = "1",
                ["Stress"] = "6",
            },
        },
    ];

    private static List<Npc> BuildNpcs(Guid campaignId) =>
    [
        new()
        {
            CampaignId = campaignId,
            Name = "Old Marta",
            Role = "Innkeeper",
            Faction = "Independent",
            Location = "The Salt & Anchor Inn",
            Appearance = "Stooped, silver-haired, missing two fingers on her left hand.",
            Mannerism = "Taps the bar three times before speaking.",
            Motivation = "Protect the town's secret from outsiders.",
            Secret = "She is the last surviving member of the original founding families.",
            Notes = "Recurring — likely to be a long-term contact.",
            KnownToPlayers = true,
            CreatedUtc = new DateTime(2024, 3, 2, 10, 0, 0, DateTimeKind.Utc),
            WasGenerated = false,
        },
        new()
        {
            CampaignId = campaignId,
            Name = "Captain Reyes",
            Role = "Harbor Master",
            Faction = "Town Council",
            Location = "The docks",
            Appearance = "Weathered, tattooed forearms, always chewing something.",
            Mannerism = "Never makes eye contact.",
            Motivation = "Cover up the smuggling operation he half-runs.",
            Secret = "He's been paying off the town watch for years.",
            Notes = string.Empty,
            KnownToPlayers = true,
            CreatedUtc = new DateTime(2024, 3, 3, 14, 15, 0, DateTimeKind.Utc),
            WasGenerated = true,
        },
        new()
        {
            CampaignId = campaignId,
            Name = "The Pale Fisherman",
            Role = "Cult Leader",
            Faction = "Cult of the Drowned",
            Location = "Unknown — sighted near the old lighthouse",
            Appearance = "Gaunt, waterlogged clothing that never quite dries.",
            Mannerism = "Speaks only in tidal metaphors.",
            Motivation = "Summon something from beneath the bay.",
            Secret = "He drowned years ago and doesn't know it.",
            Notes = "Do not reveal ancestry to players before session 5.",
            KnownToPlayers = false,
            CreatedUtc = new DateTime(2024, 3, 4, 23, 45, 0, DateTimeKind.Utc),
            WasGenerated = true,
        },
        new()
        {
            CampaignId = campaignId,
            Name = "Deputy Osei",
            Role = "Town Watch",
            Faction = "Town Council",
            Location = "Watch house",
            Appearance = "Young, earnest, still wears his uniform a size too big.",
            Mannerism = "Fidgets with his badge when nervous.",
            Motivation = "Prove himself after his predecessor vanished.",
            Secret = "He suspects Captain Reyes but has no proof.",
            Notes = string.Empty,
            KnownToPlayers = true,
            CreatedUtc = new DateTime(2024, 3, 5, 8, 30, 0, DateTimeKind.Utc),
            WasGenerated = false,
        },
        new()
        {
            CampaignId = campaignId,
            Name = "Widow Ashgrove",
            Role = "Herbalist",
            Faction = "Independent",
            Location = "Cottage on the hill",
            Appearance = "Small, sharp-eyed, always smells of dried lavender.",
            Mannerism = "Answers questions with questions.",
            Motivation = "Keep her late husband's research hidden.",
            Secret = "Her husband was experimenting with the same tide-magic the cult seeks.",
            Notes = "Potential ally if approached carefully.",
            KnownToPlayers = false,
            CreatedUtc = new DateTime(2024, 3, 6, 17, 0, 0, DateTimeKind.Utc),
            WasGenerated = false,
        },
        new()
        {
            CampaignId = campaignId,
            Name = "Finn the Ferryman",
            Role = "Transport",
            Faction = "Independent",
            Location = "River crossing, north road",
            Appearance = "Cheerful, missing an eye, hums constantly.",
            Mannerism = "Overcharges strangers, undercharges regulars.",
            Motivation = "Save enough coin to leave town before the tide rises.",
            Secret = "He's seen the Pale Fisherman and said nothing.",
            Notes = "Comic relief NPC.",
            KnownToPlayers = true,
            CreatedUtc = new DateTime(2024, 3, 7, 12, 0, 0, DateTimeKind.Utc),
            WasGenerated = true,
        },
    ];

    private static void AssertPlayerCharacterEqual(PlayerCharacter expected, PlayerCharacter actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CampaignId, actual.CampaignId);
        Assert.Equal(expected.CharacterName, actual.CharacterName);
        Assert.Equal(expected.PlayerName, actual.PlayerName);
        Assert.Equal(expected.Ancestry, actual.Ancestry);
        Assert.Equal(expected.Class, actual.Class);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.Stats, actual.Stats);
    }

    private static void AssertNpcEqual(Npc expected, Npc actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CampaignId, actual.CampaignId);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.Faction, actual.Faction);
        Assert.Equal(expected.Location, actual.Location);
        Assert.Equal(expected.Appearance, actual.Appearance);
        Assert.Equal(expected.Mannerism, actual.Mannerism);
        Assert.Equal(expected.Motivation, actual.Motivation);
        Assert.Equal(expected.Secret, actual.Secret);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.KnownToPlayers, actual.KnownToPlayers);
        Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
        Assert.Equal(expected.WasGenerated, actual.WasGenerated);
    }
}