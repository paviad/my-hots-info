using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyHotsInfo.Pages;
using MyHotsInfo.Utils;
using MyReplayLibrary;
using MyReplayLibrary.Data;
using MyReplayLibrary.Obs;

namespace MyHotsInfo;

public static class MauiProgram {
    public static MauiApp CreateMauiApp() {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif


        builder.Services.AddDbContextFactory<ReplayDbContext>((svcp, opts) => {
            var prefs = Preferences.Default;
            var connectionString = prefs.Get("DefaultConnection", AppPaths.GetDefaultConnectionString());
            opts.UseSqlite(connectionString);
        }, ServiceLifetime.Scoped);

        builder.Services.AddSingleton<ReplayList>();
        builder.Services.AddSingleton<ReplayPage>();
        builder.Services.AddSingleton<Prematch>();
        builder.Services.AddSingleton<PlayerStats>();
        builder.Services.AddScoped<Scanner>();
        builder.Services.AddScoped<PlayerQuery>();
        builder.Services.AddSingleton<Ocr>();
        builder.Services.AddSingleton<ScannedFileList>();
        builder.Services.AddSingleton(_ => TimeProvider.System);
        builder.Services.AddSingleton<MyNavigator>();
        builder.Services.AddSingleton(new GameRecorderOptions());
        builder.Services.AddSingleton<GameRecorder>();

        var mauiApp = builder.Build();

        try {
            if (Preferences.Get("ConnectionStringSet", false)) {
                MigrateDb(mauiApp);
            }
        }
        catch {
            Preferences.Remove("ConnectionStringSet");
        }

        return builder.Build();
    }

    private static void MigrateDb(MauiApp mauiApp) {
        if (!Preferences.Default.ContainsKey("ConnectionStringSet")) {
            return;
        }

        using var scope = mauiApp.Services.CreateScope();
        var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        dc.Database.Migrate();
    }
}
