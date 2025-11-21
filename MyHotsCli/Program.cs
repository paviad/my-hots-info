using System.CommandLine;
using System.Text.RegularExpressions;
using Heroes.ReplayParser;
using JetBrains.Annotations;
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
using SqlLikeToRegex;

namespace MyHotsCli;

[UsedImplicitly]
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

                services.AddDbContextFactory<ReplayDbContext>(_ => { }, ServiceLifetime.Scoped);

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

    private static async Task<int> Main(string[] args) {
        using var host = BuildHost();
        var svcp = host.Services;
        await MigrateDb(svcp);

        var rootCommand = new RootCommand();

        _gameModeOption = new Option<string?>("--gamemode");
        _gameModeOption.Aliases.Add("-gm");
        _gameModeOption.AcceptOnlyFromAmong("sl", "qm", "aram", "ud");
        rootCommand.Options.Add(_gameModeOption);

        SetupScanCommand(rootCommand, svcp);
        SetupQplayerCommand(rootCommand, svcp);
        SetupQheroCommand(rootCommand, svcp);
        SetupQallCommand(rootCommand, svcp);
        SetupScrapeCommand(rootCommand, svcp);
        SetupQreplayCommand(rootCommand, svcp);
        SetupQTalentCommand(rootCommand, svcp);
        SetupQChatCommand(rootCommand, svcp);

        var parseResult = rootCommand.Parse(args);

        return await parseResult.InvokeAsync();
    }

    private static ReplayCharacter Me(ReplayEntry z) {
        return z.ReplayCharacters.Single(y => y.IsMe);
    }

    private static async Task MigrateDb(IServiceProvider svcp) {
        using var scope = svcp.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        await dc.Database.MigrateAsync();
    }

    private static void SetupQallCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qallCommand = new Command("q", "Query all db for info");
        var mostOption = new Option<int?>("--most", "-m") {
            Description = "Most <num> seen players, default 10",
            DefaultValueFactory = _ => 10,
        };
        qallCommand.Options.Add(mostOption);
        rootCommand.Subcommands.Add(qallCommand);

        qallCommand.Validators.Add(_ => { });

        qallCommand.SetAction(async parseResult => {
            var gm = parseResult.GetValue(_gameModeOption);
            var most = parseResult.GetValue(mostOption);
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
        });
    }

    private static void SetupQChatCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qchatCommand = new Command("qc", "Query replays by chat message");

        var messageExpressionArgument = new Argument<string>("message expression");
        qchatCommand.Arguments.Add(messageExpressionArgument);

        rootCommand.Subcommands.Add(qchatCommand);

        qchatCommand.SetAction(async parseResult => {
            var gm = parseResult.GetValue(_gameModeOption);
            var msg = parseResult.GetRequiredValue(messageExpressionArgument);
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;

            var results = await playerQuery.QueryByChat(msg);

            Console.WriteLine("""
                              ----------------------------------------------------------------------------------------------------------
                              Id     | Date/Time          | Mode | Map         | Hero        | Length    | Win? | MVP? | Replay Time
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
                var msgRegex = new Regex(SqlLikeTranspiler.ToRegEx(msg));
                var chatTime = z.Chats.FirstOrDefault(r => msgRegex.IsMatch(r.Text))?.TimeSpan;

                Console.WriteLine(
                    $"{z.Id,-7}| {dateTime,-19}| {gameMode,-5}| {map,-12}| {hero,-12}| {length,-10}| {win,-5}| {mvp,-5}| {chatTime:mm\\:ss}");
            }
        });
    }


    private static void SetupQheroCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qheroCommand = new Command("qh", "Query by hero");
        var heroArgument = new Argument<string>("hero");
        qheroCommand.Arguments.Add(heroArgument);
        rootCommand.Subcommands.Add(qheroCommand);

        qheroCommand.Validators.Add(_ => { });

        qheroCommand.SetAction(async parseResult => {
            var gm = parseResult.GetValue(_gameModeOption);
            var hero = parseResult.GetRequiredValue(heroArgument);
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
        });
    }

    private static void SetupQplayerCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qplayerCommand = new Command("qp", "Query player info");
        var nameArgument = new Argument<string>("name");
        qplayerCommand.Arguments.Add(nameArgument);
        rootCommand.Subcommands.Add(qplayerCommand);

        qplayerCommand.SetAction(async parseResult => {
            var gm = parseResult.GetValue(_gameModeOption);
            var name = parseResult.GetRequiredValue(nameArgument);
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            playerQuery.GameMode = gm;
            var results = await playerQuery.QueryByName(name);
            ShowNameQueryResults(results);
        });
    }

    private static void SetupQreplayCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qreplayCommand = new Command("qr", "Query replay info");

        var qreplayListCommand = new Command("list", "List replays");
        var sinceOption = new Option<string?>("--since", "-s");
        qreplayListCommand.Options.Add(sinceOption);
        var toOption = new Option<string?>("--to", "-t");
        qreplayListCommand.Options.Add(toOption);
        var skipOption = new Option<int?>("--skip");
        qreplayListCommand.Options.Add(skipOption);
        var takeOption = new Option<int?>("--take");
        qreplayListCommand.Options.Add(takeOption);
        var heroOption = new Option<string?>("--hero");
        qreplayListCommand.Options.Add(heroOption);
        var mapOption = new Option<string?>("--map");
        qreplayListCommand.Options.Add(mapOption);
        var winOption = new Option<bool?>("--win");
        qreplayListCommand.Options.Add(winOption);
        qreplayCommand.Add(qreplayListCommand);

        var qreplayShowCommand = new Command("show", "Show details of a single replay");
        var idArgument = new Argument<int>("id");
        qreplayShowCommand.Arguments.Add(idArgument);
        qreplayCommand.Add(qreplayShowCommand);

        rootCommand.Subcommands.Add(qreplayCommand);

        qreplayListCommand.SetAction(async parseResult => {
            var gm = parseResult.GetValue(_gameModeOption);
            var since = parseResult.GetValue(sinceOption);
            var to = parseResult.GetValue(toOption);
            var skip = parseResult.GetValue(skipOption);
            var take = parseResult.GetValue(takeOption);
            var hero1 = parseResult.GetValue(heroOption);
            var map1 = parseResult.GetValue(mapOption);
            var win1 = parseResult.GetValue(winOption);

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
        });

        qreplayShowCommand.SetAction(async parseResult => {
            var id = parseResult.GetRequiredValue(idArgument);
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var replay = await playerQuery.GetReplay(id);
            var summary = Scanner.GetReplaySummary(replay);
            Console.WriteLine($"{summary}");
        });
    }

    private static void SetupQTalentCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qtalentCommand = new Command("qt", "Query talent info");
        var buildOption = new Option<string>("--build") {
            DefaultValueFactory = _ => "latest",
            Description = "Specify build number or 'latest' or 'all'",
        };

        qtalentCommand.Options.Add(buildOption);
        var keywordArgument = new Argument<string>("keyword");
        qtalentCommand.Arguments.Add(keywordArgument);

        rootCommand.Subcommands.Add(qtalentCommand);

        qtalentCommand.SetAction(async parseResult => {
            var buildNumber = parseResult.GetValue(buildOption);
            var keyword = parseResult.GetRequiredValue(keywordArgument);
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            int? bn = buildNumber switch {
                "all" => null,
                "latest" => 1000000,
                _ when int.TryParse(buildNumber, out var bnt) => bnt,
                _ => throw new Exception("Bad build number"),
            };
            var talents = await playerQuery.QueryTalent(keyword, bn);

            foreach (var talentInfo in talents) {
                PrintTalent(talentInfo);
                continue;

                static void PrintTalent(TalentRecord talentInfo1) {
                    Console.WriteLine($"Hero: {talentInfo1.HeroName}");
                    Console.WriteLine(
                        $"Build: {talentInfo1.BuildNumberFirst}-{talentInfo1.BuildNumberLast switch { 1000000 => "latest", var z => z }}");
                    Console.WriteLine($"Talent Name: {talentInfo1.TalentName}");
                    Console.WriteLine($"Talent ID: {talentInfo1.TalentIdString} ({talentInfo1.TalentId})");
                    Console.WriteLine($"Description: {talentInfo1.TalentDescription}");
                    Console.WriteLine($"Tier: {talentInfo1.Tier}");
                    Console.WriteLine(new string('-', 40));
                }
            }
        });
    }

    private static void SetupScanCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var scanCommand = new Command("scan", "Scan replay folder");
        var listOption = new Option<bool>("--list", "-l");
        var accountOption = new Option<string?>("--account", "-a");
        var regionOption = new Option<int?>("--region", "-r");
        var seqOption = new Option<int?>("--seq", "-s");
        var watchOption = new Option<bool>("--watch", "-w");
        var rescanOption = new Option<bool>("--rescan") {
            Description = "Clear scan cache, will rescan all files",
        };
        scanCommand.Options.Add(listOption);
        scanCommand.Options.Add(accountOption);
        scanCommand.Options.Add(regionOption);
        scanCommand.Options.Add(seqOption);
        scanCommand.Options.Add(watchOption);
        scanCommand.Options.Add(rescanOption);
        rootCommand.Subcommands.Add(scanCommand);

        scanCommand.SetAction(async parseResult => {
            var list = parseResult.GetValue(listOption);
            var account = parseResult.GetValue(accountOption);
            var region = parseResult.GetValue(regionOption);
            var seq = parseResult.GetValue(seqOption);
            var watch = parseResult.GetValue(watchOption);
            var rescan = parseResult.GetValue(rescanOption);

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
                    Console.WriteLine("{2}. Account {0}, Region {1} ({3} replays)", pair.Account, pair.Region, count++,
                        pair.NumReplays);
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
                Console.WriteLine("Scanning account {0}, region {1}", account, region!.Value);
                var cts = new CancellationTokenSource();
                var token = cts.Token;
                var t1 = scanner.Scan(account!, region.Value, watch, replayCallback: ReplayCallback,
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
        });

        scanCommand.Validators.Add(cr => {
            var lst = cr.GetValue(listOption);
            var acct = cr.GetValue(accountOption);
            var reg = cr.GetValue(regionOption);
            var seq = cr.GetValue(seqOption);
            var watch = cr.GetValue(watchOption);
            var flg1 = lst;
            var flg2 = !(acct is null || reg is null);
            var flg3 = seq is not null;
            var ok = ((bool[])[flg1, flg2, flg3]).Count(r => r) == 1;
            if (!ok) {
                cr.AddError("Must specify either --list or --seq or both --account and --region");
            }

            if (lst && watch) {
                cr.AddError("Can't specify --list together with --watch");
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
        var importOption = new Option<bool>("--import", "-i");
        scrapeCommand.Options.Add(importOption);
        rootCommand.Subcommands.Add(scrapeCommand);

        scrapeCommand.SetAction(async parseResult => {
            var imprt = parseResult.GetValue(importOption);
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
        });
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
