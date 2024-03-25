using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyReplayLibrary.Data;

namespace MyHotsInfo {
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

            builder.Services.AddDbContext<ReplayDbContext>(opts => {
                var dir = @"c:\myprojects\myhotsinfo";
                var fn = Path.Combine(dir, "my.db");
                opts.UseSqlite($"Data Source={fn};foreign keys=true;");
            });

            var mauiApp = builder.Build();

            MigrateDb(mauiApp);

            return mauiApp;
        }

        private static void MigrateDb(MauiApp mauiApp) {
            using var scope = mauiApp.Services.CreateScope();
            var dc = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
            dc.Database.Migrate();
        }
    }
}
