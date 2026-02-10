using FFXIV.Application.Profile;
using FFXIV.Domain.Constants;
using Microsoft.Extensions.Logging;
using NetStone;
using NetStone.Model.Parseables.Character.ClassJob;
using NetStone.Search.Character;

namespace FFXIV.Infrastructure.ExternalApis;

public sealed class LodestoneService : ILodestoneService
{
    private readonly LodestoneClient _client;
    private readonly ILogger<LodestoneService> _logger;

    public LodestoneService(LodestoneClient client, ILogger<LodestoneService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<LodestoneCharacterResult?> SearchAndFetchCharacterAsync(
        string characterName, string serverName, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Searching Lodestone for character {Name} on {Server}",
                characterName, serverName);

            var searchResponse = await _client.SearchCharacter(new CharacterSearchQuery
            {
                CharacterName = characterName,
                World = serverName
            });

            if (searchResponse is null)
            {
                _logger.LogWarning("Lodestone search returned no response for {Name} on {Server}",
                    characterName, serverName);
                return null;
            }

            var match = searchResponse.Results
                .FirstOrDefault(entry =>
                    entry.Name.Equals(characterName, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                _logger.LogInformation("No exact match found for {Name} on {Server}", characterName, serverName);
                return null;
            }

            var lodestoneId = match.Id;
            if (string.IsNullOrEmpty(lodestoneId))
            {
                _logger.LogWarning("Search match for {Name} had no Lodestone ID", characterName);
                return null;
            }

            return await FetchCharacterByIdAsync(lodestoneId, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error searching Lodestone for {Name} on {Server}",
                characterName, serverName);
            return null;
        }
    }

    public async Task<LodestoneCharacterResult?> FetchCharacterByIdAsync(
        string lodestoneId, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Fetching Lodestone character {LodestoneId}", lodestoneId);

            var character = await _client.GetCharacter(lodestoneId);
            if (character is null)
            {
                _logger.LogWarning("Character {LodestoneId} not found on Lodestone", lodestoneId);
                return null;
            }

            var result = new LodestoneCharacterResult
            {
                LodestoneId = lodestoneId,
                CharacterName = character.Name,
                ActiveJobLevel = character.ActiveClassJobLevel,
            };

            // Parse server and data center from the Server property
            // Lodestone format: "Adamantoise [Aether]" or just "Adamantoise"
            ParseServerInfo(character.Server, result);

            // Avatar
            result.AvatarUrl = character.Avatar?.ToString();

            // Extract attributes (only meaningful for the active job type)
            ExtractAttributes(character, result);

            // Extract equipped gear
            ExtractGear(character, result);

            // Fetch class/job levels
            var classJobInfo = await character.GetClassJobInfo();
            if (classJobInfo is not null)
            {
                ExtractJobLevels(classJobInfo, result);
            }

            _logger.LogInformation(
                "Fetched character {Name} on {Server}: {JobCount} jobs, {GearCount} gear pieces",
                result.CharacterName, result.ServerName,
                result.JobLevels.Count, result.EquippedGear.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching Lodestone character {LodestoneId}", lodestoneId);
            return null;
        }
    }

    private static void ParseServerInfo(string serverText, LodestoneCharacterResult result)
    {
        // Format may be "Adamantoise [Aether]" or "Adamantoise"
        var bracketIdx = serverText.IndexOf('[');
        if (bracketIdx > 0)
        {
            result.ServerName = serverText[..bracketIdx].Trim();
            var endBracket = serverText.IndexOf(']', bracketIdx);
            if (endBracket > bracketIdx)
                result.DataCenter = serverText[(bracketIdx + 1)..endBracket].Trim();
        }
        else
        {
            result.ServerName = serverText.Trim();
            result.DataCenter = FfxivServers.GetDataCenter(result.ServerName);
        }
    }

    private static void ExtractAttributes(
        NetStone.Model.Parseables.Character.LodestoneCharacter character,
        LodestoneCharacterResult result)
    {
        var attrs = character.Attributes;
        var paramName = attrs.MpGpCpParameterName;

        if (paramName.Equals("CP", StringComparison.OrdinalIgnoreCase))
        {
            // DoH active — Craftsmanship/Control/CP available
            result.Attributes = new LodestoneAttributes
            {
                Craftsmanship = attrs.Craftsmanship ?? 0,
                Control = attrs.Control ?? 0,
                CP = attrs.MpGpCp,
            };
        }
        else if (paramName.Equals("GP", StringComparison.OrdinalIgnoreCase))
        {
            // DoL active — Gathering/Perception/GP available
            result.Attributes = new LodestoneAttributes
            {
                Gathering = attrs.Gathering ?? 0,
                Perception = attrs.Perception ?? 0,
                GP = attrs.MpGpCp,
            };
        }
        // Combat jobs (MP): no DoH/DoL stats available
    }

    private static void ExtractGear(
        NetStone.Model.Parseables.Character.LodestoneCharacter character,
        LodestoneCharacterResult result)
    {
        var gear = character.Gear;
        var slots = new (string SlotKey, NetStone.Model.Parseables.Character.Gear.GearEntry? Entry)[]
        {
            ("MainHand", gear.Mainhand),
            ("OffHand", gear.Offhand),
            ("Head", gear.Head),
            ("Body", gear.Body),
            ("Hands", gear.Hands),
            ("Legs", gear.Legs),
            ("Feet", gear.Feet),
            ("Earring", gear.Earrings),
            ("Necklace", gear.Necklace),
            ("Bracelet", gear.Bracelets),
            ("Ring1", gear.Ring1),
            ("Ring2", gear.Ring2),
        };

        foreach (var (slotKey, entry) in slots)
        {
            if (entry is null || !entry.Exists) continue;

            result.EquippedGear.Add(new LodestoneGearPiece
            {
                SlotName = slotKey,
                ItemName = entry.StrippedItemName,
                IsHQ = entry.IsHq,
                GlamourName = string.IsNullOrEmpty(entry.GlamourName) ? null : entry.GlamourName,
                ItemLevel = entry.ItemLevel,
            });
        }
    }

    private static void ExtractJobLevels(CharacterClassJob classJobInfo, LodestoneCharacterResult result)
    {
        var jobs = new (string PropertyName, ClassJobEntry Entry)[]
        {
            ("Carpenter", classJobInfo.Carpenter),
            ("Blacksmith", classJobInfo.Blacksmith),
            ("Armorer", classJobInfo.Armorer),
            ("Goldsmith", classJobInfo.Goldsmith),
            ("Leatherworker", classJobInfo.Leatherworker),
            ("Weaver", classJobInfo.Weaver),
            ("Alchemist", classJobInfo.Alchemist),
            ("Culinarian", classJobInfo.Culinarian),
            ("Miner", classJobInfo.Miner),
            ("Botanist", classJobInfo.Botanist),
            ("Fisher", classJobInfo.Fisher),
        };

        foreach (var (name, entry) in jobs)
        {
            if (!entry.IsUnlocked) continue;

            var abbr = JobNameMapping.ToAbbreviation(name);
            if (abbr is not null)
            {
                result.JobLevels[abbr] = entry.Level;

                // Determine active job by matching level
                if (entry.Level == result.ActiveJobLevel && result.ActiveJobAbbreviation is null)
                {
                    // If attributes indicate DoH and this is DoH, or DoL and this is DoL, prefer it
                    if ((result.Attributes?.Craftsmanship > 0 && JobClassification.IsDoH(abbr)) ||
                        (result.Attributes?.Gathering > 0 && JobClassification.IsDoL(abbr)))
                    {
                        result.ActiveJobAbbreviation = abbr;
                    }
                }
            }
        }

        // If we didn't match active job via attributes, try any DoH/DoL job with matching level
        if (result.ActiveJobAbbreviation is null)
        {
            var matchingJob = result.JobLevels
                .FirstOrDefault(j => j.Value == result.ActiveJobLevel);
            if (matchingJob.Key is not null)
                result.ActiveJobAbbreviation = matchingJob.Key;
        }

        // Fallback: pick the highest-level DoH/DoL job
        if (result.ActiveJobAbbreviation is null && result.JobLevels.Count > 0)
        {
            result.ActiveJobAbbreviation = result.JobLevels
                .OrderByDescending(j => j.Value)
                .First().Key;
            result.ActiveJobLevel = result.JobLevels[result.ActiveJobAbbreviation];
        }
    }
}
