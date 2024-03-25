using System.Diagnostics;
using Heroes.ReplayParser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary;

public class Scanner(ReplayDbContext dc, TimeProvider timeProvider, ILogger<Scanner> logger, ScannedFileList scannedFileList) {
    public async Task Scan(string accountId, int region) {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] hots = ["Heroes of the Storm", "Accounts", accountId];
        var intPath = Path.Combine([basePath, .. hots]);
        var dirs = Directory.GetDirectories(intPath);
        var euDir = dirs.Single(r => Path.GetFileName(r).StartsWith($"{region}-"));
        string[] endPaths = ["Replays", "Multiplayer"];
        var finalPath = Path.Combine([euDir, .. endPaths]);
        var replays = Directory.GetFiles(finalPath, "*.StormReplay");

        int count = 0, max = replays.Length;

        foreach (var replay in replays.Reverse()) {
            count++;
            if (scannedFileList.Contains(replay)) {
                continue;
            }

            scannedFileList.Add(replay);

            logger.LogInformation("Scanning {replay} ({count}/{max})", replay, count, max);
            var mdp = DataParser.ParseReplay(replay, false, ParseOptions.MinimalParsing);

            if (mdp.Item1 != DataParser.ReplayParseResult.Success) {
                logger.LogInformation("... failed to parse");
                continue;
            }

            var replayHash = mdp.Item2.HashReplay();

            if (!((GameMode[])[GameMode.StormLeague, GameMode.QuickMatch]).Contains(mdp.Item2.GameMode)) {
                logger.LogInformation("... game mode {mode}", mdp.Item2.GameMode);
                continue;
            }

            if (dc.Replays.Any(r => r.ReplayHash == replayHash)) {
                logger.LogInformation("... already scanned");
                continue;
            }

            var dp = DataParser.ParseReplay(replay, false, ParseOptions.FullParsing);

            if (dp.Item1 != DataParser.ReplayParseResult.Success) {
                logger.LogInformation("... failed to parse (fully)");
                continue;
            }

            await using var transaction = await dc.Database.BeginTransactionAsync();
            await AddReplay(dp.Item2);
            await transaction.CommitAsync();
            logger.LogInformation($"... done");
        }
    }

    private static (bool, List<int>) SanityDupCheck(ReplayDbContext dc, ReplayEntry replay) {
        var sw = new Stopwatch();
        sw.Start();
        var dt1 = replay.TimestampReplay.AddSeconds(-15);
        var dt2 = replay.TimestampReplay.AddSeconds(15);
        var sanityDupCheck = (from r in dc.Replays
                              join rc in dc.ReplayCharacters on r.Id equals rc.ReplayId
                              where r.TimestampReplay >= dt1 && r.TimestampReplay <= dt2
                              select new {
                                  rc.PlayerId,
                                  rc.ReplayId,
                              })
            .ToLookup(x => x.PlayerId, x => x.ReplayId);

        var dups = replay.ReplayCharacters
            .Where(x => x.Player != null) // safety, probably not necessary -- Aviad
            .Where(x => sanityDupCheck.Contains(x.Player.Id))
            .SelectMany(x => sanityDupCheck[x.Player.Id])
            .Distinct()
            .ToList();
        var dupDetected = dups.Any();
        sw.Stop();
        var elapsed = sw.ElapsedMilliseconds;
        Debug.Print($"sanity check {elapsed} msec");
        return (dupDetected, dups);
    }

    private async Task AddReplay(Replay replayParseData) {
        var replayHash = replayParseData.HashReplay();

        if (!((GameMode[])[GameMode.StormLeague, GameMode.QuickMatch]).Contains(replayParseData.GameMode)) {
            return;
        }

        if (dc.Replays.Any(r => r.ReplayHash == replayHash)) {
            return;
        }

        var now = timeProvider.GetUtcNow();

        var mapId = replayParseData.Map;

        var replay = new ReplayEntry {
            ReplayBuild = replayParseData.ReplayBuild,
            GameMode = replayParseData.GameMode,
            MapId = mapId,
            ReplayLength = replayParseData.ReplayLength,
            ReplayHash = replayHash,
            TimestampReplay = replayParseData.Timestamp,
            TimestampCreated = now,
        };

        await dc.Replays.AddAsync(replay);

        var playerRelation = replayParseData.Players
            .Select(
                replayPlayer => (replayPlayer, dbPlayer: dc.Players
                    .SingleOrDefault(
                        j =>
                            j.BattleNetRegionId == replayPlayer.BattleNetRegionId &&
                            j.BattleNetSubId == replayPlayer.BattleNetSubId &&
                            j.BattleNetId == replayPlayer.BattleNetId)))
            .ToList();

        var players = playerRelation
            .Select(x => x.dbPlayer)
            .ToArray();

        var draftOrderArray = replayParseData.DraftOrder?
            .Where(x => x.PickType == DraftPickType.Picked)
            .Select(x => x.HeroSelected)
            .ToArray() ?? [];

        var replayCharacters = new ReplayCharacter?[10];
        for (var i = 0; i < players.Length; i++) {
            // Don't save statistics for AI players
            if (replayParseData.Players[i].PlayerType == PlayerType.Computer) {
                continue;
            }

            var plr = players[i];

            if (plr is null) {
                plr = new PlayerEntry {
                    BattleNetRegionId = replayParseData.Players[i].BattleNetRegionId,
                    BattleNetSubId = replayParseData.Players[i].BattleNetSubId,
                    BattleNetId = replayParseData.Players[i].BattleNetId,
                    Name = replayParseData.Players[i].Name,
                    BattleTag = replayParseData.Players[i].BattleTag,
                    TimestampCreated = now,
                };

                players[i] = plr;
            }
            else if (plr.Name != replayParseData.Players[i].Name ||
                     (plr.BattleTag != replayParseData.Players[i].BattleTag &&
                      replayParseData.Players[i].BattleTag != 0)) {
                var latestReplay = dc.ReplayCharacters
                    .Include(x => x.Replay)
                    .Where(x => x.PlayerId == plr.Id)
                    .Select(x => x.Replay.TimestampReplay)
                    .Max();

                if (latestReplay < replay.TimestampReplay) {
                    // Player Name or BattleTag Change
                    plr.Name = replayParseData.Players[i].Name;
                    plr.BattleTag = replayParseData.Players[i].BattleTag;
                }
            }

            if (plr.Id == 0) {
                dc.Players.Add(plr);
            }

            var characterId = replayParseData.Players[i].Character;

            var draftOrder = Array.IndexOf(draftOrderArray, replayParseData.Players[i].HeroId);

            var replayCharacter = new ReplayCharacter {
                Replay = replay,
                Player = plr,
                IsAutoSelect = replayParseData.Players[i].IsAutoSelect,
                CharacterId = characterId,
                CharacterLevel = !replayParseData.Players[i].IsAutoSelect
                    ? replayParseData.Players[i].CharacterLevel
                    : 0,
                IsWinner = replayParseData.Players[i].IsWinner,
            };

            if (draftOrder != -1) {
                replayCharacter.ReplayCharacterDraftOrder = new ReplayCharacterDraftOrder {
                    DraftOrder = draftOrder,
                };
            }

            replayCharacters[i] = replayCharacter;
            replay.ReplayCharacters.Add(replayCharacter);
        }

        if (replayCharacters.Count(x => x?.ReplayCharacterDraftOrder != null) != 10) {
            // All or nothing...
            foreach (var x in replayCharacters.Where(x => x != null)) {
                x!.ReplayCharacterDraftOrder = null;
            }
        }

        var (isDup, dups) = SanityDupCheck(dc, replay);
        if (isDup) {
            var dupIds = string.Join(",", dups);
            throw new InvalidOperationException(
                $"Replay {replayHash} rejected because these replays overlap it and have common players: {dupIds}");
        }

        var teamBlueHeroes = new HashSet<string>(
            replayCharacters
                .Where(x => x is { IsWinner: true })
                .Select(x => x!.CharacterId));
        var teamRedHeroes = new HashSet<string>(
            replayCharacters
                .Where(x => x is { IsWinner: false })
                .Select(x => x!.CharacterId));
        var isMirror = teamBlueHeroes.Intersect(teamRedHeroes).Any();
        var hasDuplicateHeros = replayCharacters
            .Where(x => x != null)
            .GroupBy(x => x!.CharacterId)
            .Any(x => x.Count() > 1);

        await dc.SaveChangesAsync();

        for (var i = 0; i < replayParseData.TeamObjectives.Length; i++) {
            // First, make sure team objectives are not on the same TimeSpan
            foreach (var nonUniqueKeyEvent in replayParseData.TeamObjectives[i]
                         .GroupBy(
                             j => new {
                                 j.TeamObjectiveType,
                                 j.TimeSpan,
                             })
                         .Where(j => j.Count() > 1)
                         .SelectMany(j => j)
                         .ToArray()) {
                while (replayParseData.TeamObjectives[i].Any(
                           j =>
                               j != nonUniqueKeyEvent &&
                               j.TeamObjectiveType == nonUniqueKeyEvent.TeamObjectiveType &&
                               j.TimeSpan == nonUniqueKeyEvent.TimeSpan)) {
                    nonUniqueKeyEvent.TimeSpan = nonUniqueKeyEvent.TimeSpan.Add(TimeSpan.FromSeconds(1));
                }
            }

            var isWinner = replayParseData.Players.First(j => j.Team == i).IsWinner;

            foreach (var teamObjective in replayParseData.TeamObjectives[i]) {
                var rto = new ReplayTeamObjective {
                    ReplayId = replay.Id,
                    IsWinner = isWinner,
                    TeamObjectiveType = teamObjective.TeamObjectiveType,
                    TimeSpan = teamObjective.TimeSpan,
                    Player = teamObjective.Player == null || teamObjective.Player.BattleNetId == 0
                        ? null
                        : players
                            .Where(j => j != null)
                            .Single(
                                j =>
                                    j!.BattleNetRegionId == teamObjective.Player.BattleNetRegionId &&
                                    j.BattleNetId == teamObjective.Player.BattleNetId),
                    Value = teamObjective.Value,
                };
                replay.ReplayTeamObjectives.Add(rto);
                dc.ReplayTeamObjectives.Add(rto);
            }

            await dc.SaveChangesAsync();
        }

        for (var i = 0; i < players.Length; i++) {
            // Don't save statistics for AI players
            if (replayParseData.Players[i].PlayerType == PlayerType.Computer) {
                continue;
            }

            // Add Talent Information
            var thePlayer = players
                .Where(j => j != null)
                .Single(
                    j =>
                        j!.BattleNetRegionId == replayParseData.Players[i].BattleNetRegionId &&
                        j.BattleNetId == replayParseData.Players[i].BattleNetId)!;
            if (replayParseData.Players[i].Talents != null) {
                foreach (var replayCharacterTalent in replayParseData.Players[i].Talents) {
                    // adding replay and player ids so can add to dc more easily later
                    var rpc = new ReplayCharacterTalent {
                        ReplayId = replay.Id,
                        Player = thePlayer,
                        TalentId = replayCharacterTalent.TalentID,
                    };

                    replayCharacters[i]?.ReplayCharacterTalents.Add(rpc);
                    dc.ReplayCharacterTalents.Add(rpc);
                    // replayCharacters[i].ReplayCharacterTalents
                    //     .Add(new ReplayCharacterTalent { TalentID = replayCharacterTalent.TalentID });
                }

                await dc.SaveChangesAsync();
            }

            // Add Player Statistics
            if (replayParseData.IsStatisticsParsedSuccessfully == true) {
                var scoreResult = replayParseData.Players[i].ScoreResult;

                // Adjust for Blizzard bugs they haven't fixed yet
                if (replayParseData.Players[i].Character == "Thrall") {
                    scoreResult.Healing = null;
                }

                var rcsr = new ReplayCharacterScoreResult {
                    ReplayId = replay.Id,
                    PlayerId = thePlayer.Id,
                    Level = scoreResult.Level,
                    Takedowns = scoreResult.Takedowns,
                    SoloKills = scoreResult.SoloKills,
                    Assists = scoreResult.Assists,
                    Deaths = scoreResult.Deaths,
                    HighestKillStreak = scoreResult.HighestKillStreak,
                    HeroDamage = scoreResult.HeroDamage,
                    SiegeDamage = scoreResult.SiegeDamage,
                    StructureDamage = scoreResult.StructureDamage,
                    MinionDamage = scoreResult.MinionDamage,
                    CreepDamage = scoreResult.CreepDamage,
                    SummonDamage = scoreResult.SummonDamage,

                    // TODO: REVISIT THIS NOW THAT THERE IS MATCH AWARD FOR CC
                    // (Currently bugged, Stitches can have hours of CC time: https://github.com/Blizzard/heroprotocol/issues/21)
                    // TimeCCdEnemyHeroes = scoreResult.TimeCCdEnemyHeroes,
                    Healing = scoreResult.Healing,
                    SelfHealing = scoreResult.SelfHealing,
                    DamageTaken = scoreResult.DamageTaken,
                    ExperienceContribution = scoreResult.ExperienceContribution,
                    TownKills = scoreResult.TownKills,
                    TimeSpentDead = scoreResult.TimeSpentDead,
                    MercCampCaptures = scoreResult.MercCampCaptures,
                    WatchTowerCaptures = scoreResult.WatchTowerCaptures,
                    MetaExperience = scoreResult.MetaExperience,
                };

                if (replayCharacters[i] is not null) {
                    replayCharacters[i]!.ReplayCharacterScoreResult = rcsr;
                }

                dc.ReplayCharacterScoreResults.Add(rcsr);

                foreach (var matchAward in scoreResult.MatchAwards) {
                    var rcma = new ReplayCharacterMatchAward {
                        ReplayId = replay.Id,
                        Player = thePlayer,
                        MatchAwardType = matchAward,
                    };
                    if (replayCharacters[i] is not null) {
                        replayCharacters[i]!.ReplayCharacterMatchAwards.Add(rcma);
                    }
                }
            }

            await dc.SaveChangesAsync();
        }
    }

    public List<(string Account, int Region)> GetAllFolders() {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] hots = ["Heroes of the Storm", "Accounts"];
        var intPath = Path.Combine([basePath, .. hots]);
        var dirs = Directory.GetDirectories(intPath).Select(Path.GetFileName);
        var pairs =
            from d in dirs
            let pidPath = Path.Combine([basePath, .. hots, d])
            let pids = Directory.GetDirectories(pidPath, "*-*").Select(Path.GetFileName)
            from pid in pids
            let reg = int.Parse(pid.Split('-')[0])
            select (Account: d, Region: reg);
        return pairs.ToList();
    }
}
