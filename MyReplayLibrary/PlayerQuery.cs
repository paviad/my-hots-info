using Heroes.ReplayParser;
using Microsoft.EntityFrameworkCore;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;

namespace MyReplayLibrary;

public class PlayerQuery(ReplayDbContext dc) {
    private string? _gameMode;
    private GameMode? _gm;

    public string? GameMode {
        get => _gameMode;
        set {
            _gameMode = value;
            _gm = ParseGameMode(value);
        }
    }

    public async Task<List<(PlayerEntry, int)>> GetMostMet(int most) {
        var gmJoin = _gm is null ? "" : "inner join replays on replays.id=replaycharacters.replayid";
        var gmClause = _gm is null ? "" : $"and replays.gamemode='{_gm.Value}'";
        var q = $"""
                 with t as (
                    select playerid, count(*) cnt
                    from replaycharacters
                    {gmJoin}
                    where isme=0 {gmClause}
                    group by playerid)
                 select p.* from t
                 inner join players p on p.id=t.playerid
                 order by t.cnt desc
                 limit {most}
                 """;
        var q2 = await dc.Players.FromSqlRaw(q).ToListAsync();
        var pids = q2.ToDictionary(z => z.Id);
        var q3 = await dc.ReplayCharacters.Where(r => pids.Keys.Contains(r.PlayerId)).GroupBy(r => r.PlayerId)
            .ToListAsync();
        var q4 = q3.Select(r => (pids[r.Key], r.Count())).OrderByDescending(z => z.Item2).ToList();
        return q4;
    }

    public async Task<ReplayEntry> GetReplay(int id) {
        var replay = await dc.Replays
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.Player)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterTalents)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterMatchAwards)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterDraftOrder)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterScoreResult)
            .AsSplitQuery()
            .SingleAsync(r => r.Id == id);

        return replay;
    }

    public async Task<IEnumerable<ReplayEntry>> ListReplays(string? since, string? to, int? skip, int? take,
        string? hero, string? map, bool? win) {
        IQueryable<ReplayEntry> q = dc.Replays
            .OrderByDescending(z => z.TimestampReplay)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.Player)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterTalents)
            .Include(r => r.ReplayCharacters).ThenInclude(r => r.ReplayCharacterMatchAwards);

        if (_gm is not null) {
            q = q.Where(r => r.GameMode == _gm.Value);
        }

        if (skip.HasValue) {
            q = q.Skip(skip.Value);
        }

        if (take.HasValue) {
            q = q.Take(take.Value);
        }

        if (hero is not null) {
            q = q.Where(z => z.ReplayCharacters.Any(r => r.IsMe && EF.Functions.Like(r.CharacterId, hero)));
        }

        if (map is not null) {
            q = q.Where(z => EF.Functions.Like(z.MapId, map));
        }

        if (win is not null) {
            q = q.Where(z => z.ReplayCharacters.Any(r => r.IsMe && r.IsWinner == win.Value));
        }

        var replays = await q.AsSplitQuery().ToListAsync();

        return replays;
    }

    public async Task<List<HeroRecord>> QueryByHero(string hero) {
        var heros = await dc.ReplayCharacters
            .Where(z => EF.Functions.Like(z.CharacterId, hero))
            .Select(r => r.CharacterId)
            .Distinct()
            .ToListAsync();

        var replays = await dc.ReplayCharacters
            .Include(r => r.Replay)
            .ThenInclude(r => r.ReplayCharacters)
            .ThenInclude(r => r.Player)
            .Where(r => _gm == null || r.Replay.GameMode == _gm)
            .Where(z => EF.Functions.Like(z.CharacterId, hero))
            .Select(r => r.Replay)
            .ToListAsync();

        var rc =
            from h in heros
            let anal = AnalyzeHero(h)
            select new HeroRecord(h, anal.total, anal.byHero);

        return [.. rc];

        (ResultRecord total, Dictionary<string, ResultRecord> byHero) AnalyzeHero(string h1) {
            var rpls = replays!.Where(r => r.ReplayCharacters.Any(z => z.CharacterId == h1 && !z.IsMe)).ToList();
            var heroes = rpls.GroupBy(r => Me(r).CharacterId);
            var byHero = heroes.ToDictionary(r => r.Key, r => AnalyzeReplays([.. r]));

            var total = AnalyzeReplays(rpls);

            return (total, byHero);

            ResultRecord AnalyzeReplays(List<ReplayEntry> rpls1) {
                var numGames = rpls1.Count;
                var wins = rpls1.Count(z => Them(z).Any(y => y.IsWinner));
                var weWon = rpls1.Count(z => Them(z).Any(y => y.IsWinner) && Me(z).IsWinner);
                var weLost = rpls1.Count(z => !Them(z).Any(y => y.IsWinner) && !Me(z).IsWinner);
                var weBeatThem = rpls1.Count(z => !Them(z).Any(y => y.IsWinner) && Me(z).IsWinner);
                var theyBeatUs = rpls1.Count(z => Them(z).Any(y => y.IsWinner) && !Me(z).IsWinner);
                ResultRecord rc1 = new(numGames, wins, weWon, weLost, weBeatThem, theyBeatUs);
                return rc1;
            }

            IEnumerable<ReplayCharacter> Them(ReplayEntry z) => z.ReplayCharacters.Where(y => y.CharacterId == h1);
        }
    }

    public async Task<List<PlayerRecord>> QueryByName(string name, bool caseSensitive = false) {
        List<PlayerEntry> players;
        if (name.Contains('#')) {
            var parts = name.Split('#');
            name = parts[0];
            var battleTag = int.Parse(parts[1]);
            players = await dc.Players
                .Where(r => EF.Functions.Like(r.Name, name) && r.BattleTag == battleTag)
                .ToListAsync();
        }
        else {
            players = await dc.Players.Where(r => EF.Functions.Like(r.Name, name)).ToListAsync();
            if (caseSensitive) {
                players = players.Where(r => r.Name.StartsWith(name)).ToList();
            }
        }

        var pids = players.Select(r => r.Id).ToList();
        var replays = await dc.ReplayCharacters
            .Include(r => r.Replay)
            .ThenInclude(r => r.ReplayCharacters)
            .ThenInclude(r => r.Player)
            .Where(r => _gm == null || r.Replay.GameMode == _gm)
            .Where(r => pids.Contains(r.PlayerId) && !r.IsMe)
            .Select(r => r.Replay)
            .ToListAsync();

        var rc =
            from p in players
            let tag = $"{p.Name}#{p.BattleTag}"
            let anal = AnalyzePid(p.Id)
            select new PlayerRecord(tag, anal.total, anal.byHero);

        return [.. rc];

        (ResultRecord total, Dictionary<string, ResultRecord> byHero) AnalyzePid(int pid) {
            var rpls = replays!.Where(r => r.ReplayCharacters.Any(z => z.PlayerId == pid)).ToList();
            var heroes = rpls.GroupBy(r => Them(r).CharacterId);
            var byHero = heroes.ToDictionary(r => r.Key, r => AnalyzeReplays([.. r]));

            var total = AnalyzeReplays(rpls);

            return (total, byHero);

            ResultRecord AnalyzeReplays(List<ReplayEntry> rpls1) {
                var numGames = rpls1.Count;
                var wins = rpls1.Count(z => Them(z).IsWinner);
                var weWon = rpls1.Count(z => Them(z).IsWinner && Me(z).IsWinner);
                var weLost = rpls1.Count(z => !Them(z).IsWinner && !Me(z).IsWinner);
                var weBeatThem = rpls1.Count(z => !Them(z).IsWinner && Me(z).IsWinner);
                var theyBeatUs = rpls1.Count(z => Them(z).IsWinner && !Me(z).IsWinner);
                ResultRecord rc1 = new(numGames, wins, weWon, weLost, weBeatThem, theyBeatUs);
                return rc1;
            }

            ReplayCharacter Them(ReplayEntry z) => z.ReplayCharacters.Single(y => y.PlayerId == pid);
        }
    }

    private ReplayCharacter Me(ReplayEntry z) => z.ReplayCharacters.Single(y => y.IsMe);

    private GameMode? ParseGameMode(string? value) {
        if (value is null) {
            return null;
        }

        if (value.Equals("sl", StringComparison.InvariantCultureIgnoreCase)) {
            return Heroes.ReplayParser.GameMode.StormLeague;
        }

        if (value.Equals("qm", StringComparison.InvariantCultureIgnoreCase)) {
            return Heroes.ReplayParser.GameMode.QuickMatch;
        }

        if (value.Equals("aram", StringComparison.InvariantCultureIgnoreCase)) {
            return Heroes.ReplayParser.GameMode.ARAM;
        }

        if (value.Equals("ud", StringComparison.InvariantCultureIgnoreCase)) {
            return Heroes.ReplayParser.GameMode.UnrankedDraft;
        }

        return null;
    }

    public record ResultRecord(int NumGames, int Wins, int WeWon, int WeLost, int WeBeatThem, int TheyBeatUs);

    public record PlayerRecord(string? BattleTag, ResultRecord Totals, Dictionary<string, ResultRecord> ByHero);

    public record HeroRecord(string Hero, ResultRecord Totals, Dictionary<string, ResultRecord> ByHero);
}
