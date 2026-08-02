using System.Runtime.CompilerServices;

// Lets GmToolkit.Data.Mapping.NpcMapper/PlayerCharacterMapper set Npc.MalformedStatsJson/
// PlayerCharacter.MalformedStatsJson (see those properties' remarks) when a row's StatsJson fails
// to deserialize, without making the setter -- which exists purely so the write path can preserve
// the original corrupted bytes instead of destroying them -- part of this model's public API.
[assembly: InternalsVisibleTo("GmToolkit.Data")]