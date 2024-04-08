using System.CommandLine;
using Heroes.ReplayParser;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyReplayLibrary;
using MyReplayLibrary.Data;
using MyReplayLibrary.Data.Models;
using MyReplayLibrary.Talents;
using MyReplayLibrary.Talents.Options;

namespace MyHotsCli;

public class Program : IDesignTimeDbContextFactory<ReplayDbContext> {
    //private static readonly string BasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    private static readonly string BasePath = AppDomain.CurrentDomain.BaseDirectory;
    private static Option<string?> _gameModeOption = null!;

    public ReplayDbContext CreateDbContext(string[] args) {
        var host = BuildHost();
        var scope = host.Services.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        return dc;
    }

    private static IHost BuildHost() {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.AddConsole())
            .ConfigureHostConfiguration(config => config
                .SetBasePath(BasePath)
                .AddJsonFile("appsettings.json", optional: true))
            .ConfigureServices((context, services) => {
                services.AddDbContext<ReplayDbContext>(opts => {
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection");
                    opts.UseSqlite(connectionString);
                });

                services.AddDbContextFactory<ReplayDbContext>(opts => { }, ServiceLifetime.Scoped);

                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<ScannedFileList>();
                services.AddScoped<Scanner>();
                services.AddScoped<PlayerQuery>();
                services.AddSingleton<Ocr>();
            });

        var host = builder.Build();
        return host;
    }

    private static Task CancelOnQPress(CancellationTokenSource cts, CancellationToken token) {
        while (!token.IsCancellationRequested) {
            var key = Console.ReadKey(true);
            if (key.KeyChar == 'q') {
                cts.Cancel();
            }
        }

        return Task.CompletedTask;
    }

    private static async Task Main(string[] args) {
        using var host = BuildHost();
        var svcp = host.Services;
        await MigrateDb(svcp);

        var rootCommand = new RootCommand();

        _gameModeOption = new Option<string?>("--gamemode");
        _gameModeOption.AddAlias("-gm");
        _gameModeOption.FromAmong("sl", "qm", "aram", "ud");
        rootCommand.AddGlobalOption(_gameModeOption);

        SetupScanCommand(rootCommand, svcp);
        SetupQplayerCommand(rootCommand, svcp);
        SetupQheroCommand(rootCommand, svcp);
        SetupQallCommand(rootCommand, svcp);
        SetupScrapeCommand(rootCommand, svcp);
        SetupQreplayCommand(rootCommand, svcp);

        await rootCommand.InvokeAsync(args);
    }

    private static ReplayCharacter Me(ReplayEntry z) => z.ReplayCharacters.Single(y => y.IsMe);

    private static async Task MigrateDb(IServiceProvider svcp) {
        using var scope = svcp.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        await dc.Database.MigrateAsync();
    }

    private static void SetupQallCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qallCommand = new Command("q", "Query all db for info");
        var mostOption = new Option<int?>("--most", () => 10, "Most <num> seen players, default 10");
        mostOption.AddAlias("-m");
        qallCommand.AddOption(mostOption);
        rootCommand.AddCommand(qallCommand);

        qallCommand.AddValidator(cr => { });

        qallCommand.SetHandler(async (gm, most) => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;
            if (most.HasValue) {
                var results = await playerQuery.GetMostMet(most.Value);
                Console.WriteLine("""
                                  -----------------------------------------
                                  Player              | Played with/against
                                  -----------------------------------------
                                  """);
                foreach (var result in results) {
                    var plr = result.Item1;
                    var num = result.Item2;
                    var btag = $"{plr.Name}#{plr.BattleTag}";
                    Console.WriteLine($"{btag,-20}| {num}");
                }
            }
        }, _gameModeOption, mostOption);
    }

    private static void SetupQheroCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qheroCommand = new Command("qh", "Query by hero");
        var heroArgument = new Argument<string>("hero");
        qheroCommand.AddArgument(heroArgument);
        rootCommand.AddCommand(qheroCommand);

        qheroCommand.AddValidator(cr => { });

        qheroCommand.SetHandler(async (gm, hero) => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;
            var results = await playerQuery.QueryByHero(hero);
            var space = false;
            foreach (var playerRecord in results) {
                if (space) {
                    Console.WriteLine();
                }

                space = true;
                Console.WriteLine($"""
                                   Stats for {playerRecord.Hero}
                                   ----------------------------------------------------------------------------------------------------------
                                   We Played   | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
                                   ----------------------------------------------------------------------------------------------------------
                                   """);
                var t = playerRecord.Totals;
                Console.WriteLine(
                    $"{"Overall",-12}| {t.NumGames,-11}| {t.Wins,-13}| {t.WeWon,-16}| {t.WeLost,-17}| {t.WeBeatThem,-13}| {t.TheyBeatUs,-13}");
                foreach (var resultRecord in playerRecord.ByHero.OrderByDescending(z => z.Value.NumGames)) {
                    var z = resultRecord.Value;
                    var hero2 = resultRecord.Key;
                    Console.WriteLine(
                        $"{hero2,-12}| {z.NumGames,-11}| {z.Wins,-13}| {z.WeWon,-16}| {z.WeLost,-17}| {z.WeBeatThem,-13}| {z.TheyBeatUs,-13}");
                }
            }
        }, _gameModeOption, heroArgument);
    }

    private static void SetupQplayerCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qplayerCommand = new Command("qp", "Query player info");
        var nameArgument = new Argument<string>("name");
        qplayerCommand.AddArgument(nameArgument);
        rootCommand.AddCommand(qplayerCommand);

        qplayerCommand.AddValidator(cr => { });

        qplayerCommand.SetHandler(async (gm, name) => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;
            var results = await playerQuery.QueryByName(name);
            ShowNameQueryResults(results);
        }, _gameModeOption, nameArgument);
    }

    private static void SetupQreplayCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qreplayCommand = new Command("qr", "Query replay info");

        var qreplayListCommand = new Command("list", "List replays");
        var sinceOption = new Option<string?>("--since");
        sinceOption.AddAlias("-s");
        qreplayListCommand.AddOption(sinceOption);
        var toOption = new Option<string?>("--to");
        toOption.AddAlias("-t");
        qreplayListCommand.AddOption(toOption);
        var skipOption = new Option<int?>("--skip");
        qreplayListCommand.AddOption(skipOption);
        var takeOption = new Option<int?>("--take");
        qreplayListCommand.AddOption(takeOption);
        var heroOption = new Option<string?>("--hero");
        qreplayListCommand.AddOption(heroOption);
        var mapOption = new Option<string?>("--map");
        qreplayListCommand.AddOption(mapOption);
        var winOption = new Option<bool?>("--win");
        qreplayListCommand.AddOption(winOption);
        qreplayCommand.Add(qreplayListCommand);

        var qreplayShowCommand = new Command("show", "Show details of a single replay");
        var idArgument = new Argument<int>("id");
        qreplayShowCommand.AddArgument(idArgument);
        qreplayCommand.Add(qreplayShowCommand);

        rootCommand.AddCommand(qreplayCommand);

        qreplayListCommand.AddValidator(cr => { });
        qreplayShowCommand.AddValidator(cr => { });

        qreplayListCommand.SetHandler(async (gm, since, to, skip, take, hero1, map1, win1) => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;
            var results = await playerQuery.ListReplays(since, to, skip, take, hero1, map1, win1);
            Console.WriteLine("""
                              ----------------------------------------------------------------------------------------------------------
                              Id     | Date/Time          | Mode | Map         | Hero        | Length    | Win? | MVP?
                              ----------------------------------------------------------------------------------------------------------
                              """);
            foreach (var z in results) {
                var dateTime = z.TimestampReplay;
                var gameMode = z.GameMode switch {
                    GameMode.QuickMatch => "QM",
                    GameMode.UnrankedDraft => "UD",
                    GameMode.StormLeague => "SL",
                    GameMode.ARAM => "ARAM",
                    _ => z.GameMode.ToString()[..2],
                };
                var map = z.MapId.Split(' ')[0];
                var me = Me(z);
                var hero = me.CharacterId switch {
                    "The Lost Vikings" => "Vikings",
                    _ => me.CharacterId,
                };
                var length = z.ReplayLength;
                var win = me.IsWinner ? "Yes" : "";
                var mvp = me.ReplayCharacterMatchAwards.Any(r => r.MatchAwardType == MatchAwardType.MVP) ? "Yes" : "";
                Console.WriteLine(
                    $"{z.Id,-7}| {dateTime,-19}| {gameMode,-5}| {map,-12}| {hero,-12}| {length,-10}| {win,-5}| {mvp}");
            }
        }, _gameModeOption, sinceOption, toOption, skipOption, takeOption, heroOption, mapOption, winOption);

        qreplayShowCommand.SetHandler(async id => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var replay = await playerQuery.GetReplay(id);
            var summary = Scanner.GetReplaySummary(replay);
            Console.WriteLine($"{summary}");
        }, idArgument);
    }

    private static void SetupScanCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var scanCommand = new Command("scan", "Scan replay folder");
        var listOption = new Option<bool>("--list");
        listOption.AddAlias("-l");
        var accountOption = new Option<string?>("--account");
        accountOption.AddAlias("-a");
        var regionOption = new Option<int?>("--region");
        regionOption.AddAlias("-r");
        var seqOption = new Option<int?>("--seq");
        seqOption.AddAlias("-s");
        var watchOption = new Option<bool>("--watch");
        watchOption.AddAlias("-w");
        var rescanOption = new Option<bool>("--rescan", "Clear scan cache, will rescan all files");
        scanCommand.AddOption(listOption);
        scanCommand.AddOption(accountOption);
        scanCommand.AddOption(regionOption);
        scanCommand.AddOption(seqOption);
        scanCommand.AddOption(watchOption);
        scanCommand.AddOption(rescanOption);
        rootCommand.AddCommand(scanCommand);

        scanCommand.SetHandler(async (list, account, region, seq, watch, rescan) => {
            using var scope = svcp.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<Scanner>();
            if (rescan) {
                var scannedFileList = scope.ServiceProvider.GetRequiredService<ScannedFileList>();
                scannedFileList.Reset();
            }

            if (list) {
                var pairs = scanner.GetAllFolders();
                var count = 1;
                foreach (var pair in pairs) {
                    Console.WriteLine("{2}. Account {0}, Region {1}", pair.Account, pair.Region, count++);
                }
            }
            else if (seq is not null) {
                var pairs = scanner.GetAllFolders();
                if (seq.Value > pairs.Count || seq.Value < 1) {
                    Console.Error.WriteLine("Sequence must be 1-{0}, use --list to see the list", pairs.Count);
                    return;
                }

                var acct = pairs[seq.Value - 1].Account;
                var reg = pairs[seq.Value - 1].Region;
                Console.WriteLine("Scanning account {0}, region {1}", acct, reg);
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                var t1 = scanner.Scan(acct, reg, watch, replayCallback: ReplayCallback,
                    screenShotCallback: ScreenshotCallback, cancellationToken: token);
                await WatchOrRunOnce(watch, cts, token, t1);
            }
            else {
                Console.WriteLine("Scanning account {0}, region {1}", account!, region!.Value);
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                var t1 = scanner.Scan(account, region.Value, watch, replayCallback: ReplayCallback,
                    screenShotCallback: ScreenshotCallback, cancellationToken: token);
                await WatchOrRunOnce(watch, cts, token, t1);
            }

            return;

            async Task ScreenshotCallback(List<string> names) {
                using var scp2 = svcp.CreateScope();
                var playerQuery = scp2.ServiceProvider.GetRequiredService<PlayerQuery>();
                foreach (var name in names) {
                    var results = await playerQuery.QueryByName(name);
                    ShowNameQueryResultsOneLinePerHero(results);
                }
            }

            async Task ReplayCallback(int replayId) {
                using var scp2 = svcp.CreateScope();
                var playerQuery = scp2.ServiceProvider.GetRequiredService<PlayerQuery>();
                var replay = await playerQuery.GetReplay(replayId);
                var summary = Scanner.GetReplaySummary(replay);
                Console.WriteLine($"{summary}");
            }
        }, listOption, accountOption, regionOption, seqOption, watchOption, rescanOption);

        scanCommand.AddValidator(cr => {
            var lst = cr.GetValueForOption(listOption);
            var acct = cr.GetValueForOption(accountOption);
            var reg = cr.GetValueForOption(regionOption);
            var seq = cr.GetValueForOption(seqOption);
            var watch = cr.GetValueForOption(watchOption);
            var flg1 = lst;
            var flg2 = !(acct is null || reg is null);
            var flg3 = seq is not null;
            var ok = ((bool[]) [flg1, flg2, flg3]).Count(r => r) == 1;
            if (!ok) {
                cr.ErrorMessage = "Must specify either --list or --seq or both --account and --region";
            }

            if (lst && watch) {
                cr.ErrorMessage = "Can't specify --list together with --watch";
            }
        });

        return;

        async Task WatchOrRunOnce(bool watch, CancellationTokenSource cts, CancellationToken token, Task t1) {
            if (watch) {
                Console.WriteLine("Press 'Q' to exit");
                var t2 = CancelOnQPress(cts, token);
                await Task.WhenAll(t1, t2);
            }
            else {
                await t1;
            }
        }
    }

    private static void SetupScrapeCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var scrapeCommand = new Command("scrape", "Scrape hots installation for talents");
        var importOption = new Option<bool>("--import");
        importOption.AddAlias("-i");
        scrapeCommand.AddOption(importOption);
        rootCommand.AddCommand(scrapeCommand);

        scrapeCommand.AddValidator(cr => { });

        scrapeCommand.SetHandler(async imprt => {
            using var scope = svcp.CreateScope();
            var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
            var scraper = new HotsScraper.HotsScraper();
            if (imprt) {
                NewBuildOptions opts = new() {
                    DryRun = false,
                    OutputType = OutputType.Csv,
                    Path = Path.Combine(BasePath, "talentInfo.xml"),
                };
                TalentsLib.NewBuildInternal(opts, dc);
                await dc.SaveChangesAsync();
            }
            else {
                scraper.Scrape(BasePath);
            }
        }, importOption);
    }

    private static void ShowNameQueryResults(List<PlayerQuery.PlayerRecord> results) {
        var space = false;
        foreach (var playerRecord in results) {
            if (space) {
                Console.WriteLine();
            }

            space = true;
            Console.WriteLine($"""
                               Stats for {playerRecord.BattleTag}
                               ----------------------------------------------------------------------------------------------------------
                               They Played | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
                               ----------------------------------------------------------------------------------------------------------
                               """);
            var t = playerRecord.Totals;
            Console.WriteLine(
                $"{"Overall",-12}| {t.NumGames,-11}| {t.Wins,-13}| {t.WeWon,-16}| {t.WeLost,-17}| {t.WeBeatThem,-13}| {t.TheyBeatUs,-13}");
            foreach (var resultRecord in playerRecord.ByHero) {
                var z = resultRecord.Value;
                var hero = resultRecord.Key;
                Console.WriteLine(
                    $"{hero,-12}| {z.NumGames,-11}| {z.Wins,-13}| {z.WeWon,-16}| {z.WeLost,-17}| {z.WeBeatThem,-13}| {z.TheyBeatUs,-13}");
            }
        }
    }

    private static void ShowNameQueryResultsOneLinePerHero(List<PlayerQuery.PlayerRecord> results) {
        foreach (var v in Enumerate()) {
            Console.WriteLine($"{v}");
        }

        IEnumerable<string> Enumerate() {
            foreach (var playerRecord in results) {
                var name = playerRecord.BattleTag;
                var games = playerRecord.Totals.NumGames;
                var wins = playerRecord.Totals.Wins;
                var winRate = 1.0 * wins / games;
                var r = $"{name}:{winRate:P1}";
                var overall = $"{r,-30}";

                var q =
                    from resultRecord in playerRecord.ByHero
                    let hero = resultRecord.Key
                    let z = resultRecord.Value
                    let games1 = z.NumGames
                    let wins1 = z.Wins
                    let winRate1 = 1.0 * wins1 / games1
                    orderby games1 descending
                    let r1 = $"{hero}:{winRate1:P1}"
                    select $"{r1,-30}";

                var m = string.Join("", q.Take(3));

                yield return $"{overall}{m}";
            }
        }
    }
}
