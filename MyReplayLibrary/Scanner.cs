using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.RegularExpressions;
using Heroes.ReplayParser;
using Heroes.ReplayParser.MPQFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary;

public partial class Scanner(
    IDbContextFactory<ReplayDbContext> dcFactory,
    TimeProvider timeProvider,
    ILogger<Scanner> logger,
    Ocr ocr,
    ScannedFileList scannedFileList) : IDisposable {
    private readonly GameMode[] _validGameModes = [
        GameMode.StormLeague,
        GameMode.ARAM,
        GameMode.QuickMatch,
        GameMode.UnrankedDraft,
    ];

    private FileSystemWatcher? _fswReplays;

    private FileSystemWatcher? _fswScreenshots;

    public void Dispose() {
        _fswReplays?.Dispose();
        _fswScreenshots?.Dispose();
    }

    public static string GetReplaySummary(ReplayEntry replay) {
        var mvp = replay.ReplayCharacters
            .Single(r => r.ReplayCharacterMatchAwards.Any(z => z.MatchAwardType == MatchAwardType.MVP)).Player;

        var sb = new StringBuilder();
        sb.AppendLine($"""
                       Id: {replay.Id}
                       Game Time: {replay.TimestampReplay}
                       Game Mode: {replay.GameMode}
                       Map: {replay.MapId}
                       Mvp: {mvp.Name}#{mvp.BattleTag}
                       """);
        sb.AppendLine("Winning Team:");
        sb.AppendLine("-----------------");
        foreach (var rc in replay.ReplayCharacters.Where(r => r.IsWinner)) {
            var btag = $"{rc.Player.Name}#{rc.Player.BattleTag}";
            sb.AppendLine($"   {btag,-20} - {rc.CharacterId}");
        }

        sb.AppendLine("Losing Team:");
        sb.AppendLine("-----------------");
        foreach (var rc in replay.ReplayCharacters.Where(r => !r.IsWinner)) {
            var btag = $"{rc.Player.Name}#{rc.Player.BattleTag}";
            sb.AppendLine($"   {btag,-20} - {rc.CharacterId}");
        }

        var message = sb.ToString();
        return message;
    }

    public List<(string Account, int Region, int NumReplays)> GetAllFolders() {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] hots = ["Heroes of the Storm", "Accounts"];
        var intPath = Path.Combine([basePath, .. hots]);
        var dirs = Directory.GetDirectories(intPath).Select(Path.GetFileName);
        var pairs =
            from d in dirs
            let pidPath = Path.Combine([basePath, .. hots, d])
            let pidsFull = Directory.GetDirectories(pidPath, "*-*")
            from pid1 in pidsFull
            let pid = Path.GetFileName(pid1)
            let path = Path.Combine(pid1, "Replays", "Multiplayer")
            let reg = int.Parse(pid.Split('-')[0])
            let num = Directory.GetFiles(path, "*.StormReplay").Length
            select (Account: d, Region: reg, NumReplays: num);
        return pairs.ToList();
    }

    public async Task Scan(string accountId, int region, bool watch, Func<int, Task>? replayCallback = null,
        Func<List<string>, Task>? screenShotCallback = null, CancellationToken cancellationToken = default) {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] hots = ["Heroes of the Storm", "Accounts", accountId];
        var intPath = Path.Combine([basePath, .. hots]);
        var dirs = Directory.GetDirectories(intPath);
        var euDir = dirs.Single(r => Path.GetFileName(r).StartsWith($"{region}-"));
        string[] endPaths = ["Replays", "Multiplayer"];
        var finalPath = Path.Combine([euDir, .. endPaths]);

        var replays = Directory.GetFiles(finalPath, "*.StormReplay");

        int count = 0, max = replays.Length;
        string? processed = null;

        foreach (var replay in replays.Reverse()) {
            count++;
            if (scannedFileList.Contains(replay)) {
                continue;
            }

            if (processed is not null) {
                scannedFileList.Add(processed);
            }

            processed = replay;

            logger.LogInformation("Scanning {replay} ({count}/{max})", replay, count, max);

            await ScanOneReplay(replay, cancellationToken);
        }

        // One last time
        if (processed is not null) {
            scannedFileList.Add(processed);
        }

        if (watch) {
            logger.LogInformation("Watching for new replays...");

            var t2 = WatchScreenshots(screenShotCallback ?? NoOp, cancellationToken);
            var t1 = Watch(finalPath, replayCallback ?? NoOp, cancellationToken);

            await Task.WhenAll(t1, t2);
        }
    }

    private static Task NoOp<T>(T _) => Task.CompletedTask;

    [GeneratedRegex(@"[/\\]\d+-Hero-\d+-(?<pid>\d+)[/\\]")]
    private static partial Regex PlayerIdRegex();


    private static (bool, List<int>) SanityDupCheck(ReplayDbContext dc, ReplayEntry replay) {
        var sw = new Stopwatch();
        sw.Start();
        var dt1 = replay.TimestampReplay.AddSeconds(-15);
        var dt2 = replay.TimestampReplay.AddSeconds(15);
        var sanityDupCheck = (
                from r in dc.Replays
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

    private async Task<int?> AddReplay(ReplayDbContext dc, Replay replayParseData, int myPlayerId) {
        var replayHash = replayParseData.HashReplay();

        if (!_validGameModes.Contains(replayParseData.GameMode)) {
            return null;
        }

        if (dc.Replays.Any(r => r.ReplayHash == replayHash)) {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

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
                IsMe = plr.BattleNetId == myPlayerId,
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

        await UpdateTakedowns(dc, replayParseData, replay);

        await UpdateChats(dc, replayParseData, replay);

        return replay.Id;
    }

    private async Task RetroUpdateChat(
        ReplayDbContext dc,
        string replayFile,
        Guid replayHash,
        CancellationToken cancellationToken) //
    {
        var replayEntry = await dc.Replays
            .Include(r => r.ReplayCharacters)
            .ThenInclude(r => r.Player)
            .SingleAsync(r => r.ReplayHash == replayHash, cancellationToken: cancellationToken);
        var dp2 = DataParser.ParseReplay(replayFile, false, ParseOptions.FullParsing);

        //await dc.Takedowns.Where(r => r.ReplayId == replayEntry.Id).ExecuteDeleteAsync(cancellationToken: cancellationToken);

        var chatsExist =
            await dc.Chats.AnyAsync(r => r.ReplayId == replayEntry.Id, cancellationToken: cancellationToken);
        if (!chatsExist) {
            logger.LogInformation("Retroactively updating chats for replay id {id}", replayEntry.Id);
            await UpdateChats(dc, dp2.Item2, replayEntry);
        }
    }

    private async Task RetroUpdateTakedowns(
        ReplayDbContext dc,
        string replayFile,
        Guid replayHash,
        CancellationToken cancellationToken) //
    {
        var replayEntry = await dc.Replays
            .Include(r => r.ReplayCharacters)
            .ThenInclude(r => r.Player)
            .SingleAsync(r => r.ReplayHash == replayHash, cancellationToken: cancellationToken);
        var dp2 = DataParser.ParseReplay(replayFile, false, ParseOptions.FullParsing);

        //await dc.Takedowns.Where(r => r.ReplayId == replayEntry.Id).ExecuteDeleteAsync(cancellationToken: cancellationToken);

        var takedownsExist =
            await dc.Takedowns.AnyAsync(r => r.ReplayId == replayEntry.Id, cancellationToken: cancellationToken);
        if (!takedownsExist) {
            logger.LogInformation("Retroactively updating takedowns for replay id {id}", replayEntry.Id);
            await UpdateTakedowns(dc, dp2.Item2, replayEntry);
        }
    }

    private async Task<int?> ScanOneReplay(string replay, CancellationToken cancellationToken) {
        var mdp = DataParser.ParseReplay(replay, false, ParseOptions.MinimalParsing);

        if (mdp.Item1 != DataParser.ReplayParseResult.Success) {
            logger.LogInformation("... failed to parse");
            return null;
        }

        var replayHash = mdp.Item2.HashReplay();

        if (!_validGameModes.Contains(mdp.Item2.GameMode)) {
            logger.LogInformation("... game mode {mode}", mdp.Item2.GameMode);
            return null;
        }

        await using var dc = await dcFactory.CreateDbContextAsync(cancellationToken);

        //var deleteExistingForRescan = await dc.Replays.SingleOrDefaultAsync(r => r.ReplayHash == replayHash, cancellationToken: cancellationToken);
        //if (deleteExistingForRescan is not null) {
        //    dc.Replays.Remove(deleteExistingForRescan);
        //    await dc.SaveChangesAsync(cancellationToken);
        //}

        if (dc.Replays.Any(r => r.ReplayHash == replayHash)) {
            logger.LogInformation("... already scanned");

            await RetroUpdateTakedowns(dc, replay, replayHash, cancellationToken);
            await RetroUpdateChat(dc, replay, replayHash, cancellationToken);

            return null;
        }

        var dp = DataParser.ParseReplay(replay, false, ParseOptions.FullParsing);

        if (dp.Item1 != DataParser.ReplayParseResult.Success) {
            logger.LogInformation("... failed to parse (fully)");
            return null;
        }

        await using var transaction = await dc.Database.BeginTransactionAsync(cancellationToken);
        try {
            var match = PlayerIdRegex().Match(replay);
            var myPlayerId = match.Success ? int.Parse(match.Groups["pid"].Value) : -1;
            var replayId = await AddReplay(dc, dp.Item2, myPlayerId);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("... done");
            return replayId;
        }
        catch (Exception e) {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(e, "... failed");
        }

        return null;
    }

    private async Task UpdateChats(ReplayDbContext dc, Replay replayParseData, ReplayEntry replay) {
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

        var chatMessages = replayParseData.Messages.Where(r =>
            r.MessageEventType == ReplayMessageEvents.MessageEventType.SChatMessage);

        var seqId = 0;

        foreach (var message in chatMessages) {
            //// var playerId = (int)death.Data.dictionary[2].optionalData.array[i].dictionary[1].vInt!.Value;
            //if (playerId > players.Length || playerId < 1 || players[playerId - 1] is null) {
            //    logger.LogWarning(
            //        "Killer ID ({killerId}) invalid for Victim ID ({victimId}) in replay ID {replayId}", killerId,
            //        victimId, replay.Id);
            //    continue;
            //}

            var sender = players[message.PlayerIndex];
            var timestamp = message.Timestamp;
            var text = message.ChatMessage.Message;

            var dbChat = new Chat {
                ReplayId = replay.Id,
                PlayerId = sender!.Id,
                SeqId = seqId++,
                TimeSpan = timestamp,
                Text = text,
            };

            await dc.Chats.AddAsync(dbChat);
        }

        await dc.SaveChangesAsync();
    }

    private async Task UpdateTakedowns(ReplayDbContext dc, Replay replayParseData, ReplayEntry replay) {
        var trackerDeaths = replayParseData.TrackerEvents
            .Where(r => r.TrackerEventType == ReplayTrackerEvents.TrackerEventType.StatGameEvent &&
                        r.Data.dictionary[0].blobText == "PlayerDeath").ToList();

        var seqId = 0;

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

        foreach (var death in trackerDeaths) {
            var victimId = (int)death.Data.dictionary[2].optionalData.array[0].dictionary[1].vInt!.Value;
            if (victimId > players.Length || victimId < 1 || players[victimId - 1] is null) {
                logger.LogWarning("Victim ID ({victimId}) invalid in replay ID {replayId}", victimId, replay.Id);
                continue;
            }

            var victim = players[victimId - 1]!;
            var numKillers = death.Data.dictionary[2].optionalData.array.Length - 1;
            var any = false;
            for (var i = 1; i <= numKillers; i++) {
                var killerId = (int)death.Data.dictionary[2].optionalData.array[i].dictionary[1].vInt!.Value;
                if (killerId > players.Length || killerId < 1 || players[killerId - 1] is null) {
                    logger.LogWarning(
                        "Killer ID ({killerId}) invalid for Victim ID ({victimId}) in replay ID {replayId}", killerId,
                        victimId, replay.Id);
                    continue;
                }

                var killer = players[killerId - 1]!;

                var dbDeath = new Takedown {
                    ReplayId = replay.Id,
                    SeqId = seqId,
                    TimeSpan = death.TimeSpan,
                    KillerId = killer.Id,
                    VictimId = victim.Id,
                    KillingBlow = null,
                };

                await dc.Takedowns.AddAsync(dbDeath);
                any = true;
            }

            if (any) {
                seqId++;
            }
        }

        await dc.SaveChangesAsync();
    }

    private async Task Watch(string s, Func<int, Task> callBack, CancellationToken cancellationToken) {
        var subj = new Subject<string>();
        var inp = subj.GroupBy(z => z)
            .SelectMany(z => z.Throttle(TimeSpan.FromSeconds(1)));

        _fswReplays = new FileSystemWatcher(s, "*.StormReplay");

        var subs = inp.SelectMany(async fn => {
            scannedFileList.Add(fn);
            logger.LogInformation("Scanning {replay} {fsw}", fn, _fswReplays.EnableRaisingEvents);
            try {
                var replayId = await ScanOneReplay(fn, cancellationToken);
                if (replayId is not null) {
                    await callBack(replayId.Value);
                    //await LogReplay(replayId.Value);
                }
            }
            catch (Exception x) {
                logger.LogWarning(x, "Unable to parse new replay {path}", fn);
            }

            return true;
        }).Subscribe();

        logger.LogInformation("Setting up file system watch on {path}", s);

        _fswReplays.Created += FswCreated;
        _fswReplays.Changed += FswChanged;
        _fswReplays.Renamed += FswRenamed;
        _fswReplays.Error += FswError;
        _fswReplays.EnableRaisingEvents = true;
        _fswReplays.Disposed += FswDisposed;
        _fswReplays.Deleted += FswDeleted;

        try {
            await Task.Delay(-1, cancellationToken);
        }
        finally {
            logger.LogWarning("Exited Watch");

            subs.Dispose();
        }

        return;

        void FswDisposed(object? sender, EventArgs e) {
            logger.LogWarning("File system watcher disposed");
        }

        void FswCreated(object sender, FileSystemEventArgs e) {
            try {
                logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
                subj.OnNext(e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswChanged(object sender, FileSystemEventArgs e) {
            try {
                logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
                subj.OnNext(e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswRenamed(object sender, RenamedEventArgs e) {
            try {
                subj.OnNext(e.FullPath);
                logger.LogInformation("{changeType} {oldPath} -> {path}", e.ChangeType, e.OldFullPath, e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswError(object sender, ErrorEventArgs e) {
            logger.LogWarning(e.GetException(), "Error event from file system watcher");
        }

        void FswDeleted(object sender, FileSystemEventArgs e) {
            logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
        }
    }


    private async Task WatchScreenshots(Func<List<string>, Task> callBack, CancellationToken cancellationToken) {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string[] hots = ["Heroes of the Storm", "Screenshots"];
        var s = Path.Combine([basePath, .. hots]);
        var subj = new Subject<string>();
        var inp = subj.GroupBy(z => z)
            .SelectMany(z => z.Throttle(TimeSpan.FromSeconds(1)));

        _fswScreenshots = new FileSystemWatcher(s, "*.jpg");

        var subs = inp.SelectMany(async fn => {
            logger.LogInformation("Scanning screenshot {fileName}", fn);
            try {
                var rc1 = await ocr.OcrScreenshot(fn, ScreenShotKind.Draft);
                var rc2 = await ocr.OcrScreenshot(fn, ScreenShotKind.Loading);
                var rc = ((List<string>[]) [rc1, rc2]).FirstOrDefault(z => z.Contains("Skywalker")) ?? [];
                await callBack(rc);
                //var msg = string.Join("\n", rc.Select(z => $"   {z}"));
                //logger.LogInformation("Players in this game:\n{msg}", msg);
            }
            catch (Exception x) {
                logger.LogWarning(x, "Unable to read screenshot {path}", fn);
            }

            return true;
        }).Subscribe();

        logger.LogInformation("Setting up file system watch on {path}", s);

        _fswScreenshots.Created += FswCreated;
        _fswScreenshots.Changed += FswChanged;
        _fswScreenshots.Renamed += FswRenamed;
        _fswScreenshots.Error += FswError;
        _fswScreenshots.EnableRaisingEvents = true;
        _fswScreenshots.Disposed += FswDisposed;
        _fswScreenshots.Deleted += FswDeleted;

        try {
            await Task.Delay(-1, cancellationToken);
        }
        finally {
            logger.LogWarning("Exited WatchScreenshots");

            subs.Dispose();
        }

        return;

        void FswDisposed(object? sender, EventArgs e) {
            logger.LogWarning("File system watcher disposed");
        }

        void FswCreated(object sender, FileSystemEventArgs e) {
            try {
                logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
                subj.OnNext(e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswChanged(object sender, FileSystemEventArgs e) {
            try {
                logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
                subj.OnNext(e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswRenamed(object sender, RenamedEventArgs e) {
            try {
                subj.OnNext(e.FullPath);
                logger.LogInformation("{changeType} {oldPath} -> {path}", e.ChangeType, e.OldFullPath, e.FullPath);
            }
            catch (Exception exception) {
                logger.LogWarning(exception, "Error in file system watcher");
            }
        }

        void FswError(object sender, ErrorEventArgs e) {
            logger.LogWarning(e.GetException(), "Error event from file system watcher");
        }

        void FswDeleted(object sender, FileSystemEventArgs e) {
            logger.LogInformation("{changeType} {path}", e.ChangeType, e.FullPath);
        }
    }
}
