﻿using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyReplayLibrary;
using MyReplayLibrary.Data;
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
            });

        var host = builder.Build();
        return host;
    }

    private static async Task Main(string[] args) {
        var host = BuildHost();
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

        await rootCommand.InvokeAsync(args);
    }

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
                                   Hero        | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
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
            var space = false;
            foreach (var playerRecord in results) {
                if (space) {
                    Console.WriteLine();
                }

                space = true;
                Console.WriteLine($"""
                                   Stats for {playerRecord.BattleTag}
                                   ----------------------------------------------------------------------------------------------------------
                                   Hero        | Games      | They Won     | We Won Together | We Lost Together | We Beat Them | They Beat Us
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
        }, _gameModeOption, nameArgument);
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
        scanCommand.AddOption(listOption);
        scanCommand.AddOption(accountOption);
        scanCommand.AddOption(regionOption);
        scanCommand.AddOption(seqOption);
        scanCommand.AddOption(watchOption);
        rootCommand.AddCommand(scanCommand);

        scanCommand.SetHandler(async (list, account, region, seq, watch) => {
            using var scope = svcp.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<Scanner>();
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
                await scanner.Scan(acct, reg, watch);
            }
            else {
                Console.WriteLine("Scanning account {0}, region {1}", account!, region!.Value);
                await scanner.Scan(account, region.Value, watch);
            }
        }, listOption, accountOption, regionOption, seqOption, watchOption);

        scanCommand.AddValidator(cr => {
            var lst = cr.GetValueForOption(listOption);
            var acct = cr.GetValueForOption(accountOption);
            var reg = cr.GetValueForOption(regionOption);
            var seq = cr.GetValueForOption(seqOption);
            var watch = cr.GetValueForOption(watchOption);
            var flg1 = lst;
            var flg2 = !(acct is null || reg is null);
            var flg3 = seq is not null;
            var ok = ((bool[])[flg1, flg2, flg3]).Count(r => r) == 1;
            if (!ok) {
                cr.ErrorMessage = "Must specify either --list or --seq or both --account and --region";
            }

            if (lst && watch) {
                cr.ErrorMessage = "Can't specify --list together with --watch";
            }
        });
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
                await scraper.Scrape(BasePath);
            }
        }, importOption);
    }
}
