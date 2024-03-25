using System.CommandLine;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyReplayLibrary;
using MyReplayLibrary.Data;

namespace MyHotsCli;

public class Program : IDesignTimeDbContextFactory<ReplayDbContext> {

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
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json"))
            .ConfigureServices((context, services) => {
                services.AddDbContext<ReplayDbContext>(opts => {
                    var dir = @"c:\myprojects\myhotsinfo";
                    var fn = Path.Combine(dir, "my.db");
                    var connectionString = $"Data Source={fn};foreign keys=true;";
                    opts.UseSqlite(connectionString);
                });

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
        //await MigrateDb(svcp);

        var rootCommand = new RootCommand();
        SetupScanCommand(rootCommand, svcp);
        SetupQplayerCommand(rootCommand, svcp);

        await rootCommand.InvokeAsync(args);
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
        scanCommand.AddOption(listOption);
        scanCommand.AddOption(accountOption);
        scanCommand.AddOption(regionOption);
        scanCommand.AddOption(seqOption);
        rootCommand.AddCommand(scanCommand);

        scanCommand.SetHandler(async (bool list, string? account, int? region, int? seq) => {
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
                await scanner.Scan(acct, reg);
            }
            else {
                Console.WriteLine("Scanning account {0}, region {1}", account!, region!.Value);
                await scanner.Scan(account, region.Value);
            }
        }, listOption, accountOption, regionOption, seqOption);

        scanCommand.AddValidator(cr => {
            var lst = cr.GetValueForOption(listOption);
            var acct = cr.GetValueForOption(accountOption);
            var reg = cr.GetValueForOption(regionOption);
            var seq = cr.GetValueForOption(seqOption);
            var flg1 = lst;
            var flg2 = !(acct is null || reg is null);
            var flg3 = seq is not null;
            var ok = ((bool[])[flg1, flg2, flg3]).Count(r => r) == 1;
            if (!ok) {
                cr.ErrorMessage = "Must specify either --list or --seq or both --account and --region";
            }
        });
    }

    private static void SetupQplayerCommand(RootCommand rootCommand, IServiceProvider svcp) {
        var qplayerCommand = new Command("qp", "Query player info");
        var nameArgument = new Argument<string>("name");
        qplayerCommand.AddArgument(nameArgument);
        rootCommand.AddCommand(qplayerCommand);

        qplayerCommand.SetHandler(async (string name) => {
            using var scope = svcp.CreateScope();
            var playerQuery = scope.ServiceProvider.GetRequiredService<PlayerQuery>();
            var results = await playerQuery.QueryByName(name);
        }, nameArgument);
    }

    private static async Task MigrateDb(IServiceProvider svcp) {
        using var scope = svcp.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        await dc.Database.MigrateAsync();
    }
}
