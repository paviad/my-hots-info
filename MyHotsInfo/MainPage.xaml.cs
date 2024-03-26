using Microsoft.EntityFrameworkCore;
using MyReplayLibrary.Data;

namespace MyHotsInfo {
    public partial class MainPage : ContentPage {
        private readonly IServiceProvider _svcp;
        private readonly IPreferences _prefs;
        int count = 0;

        public MainPage(IServiceProvider svcp) {
            _svcp = svcp;
            _prefs = Preferences.Default;
            InitializeComponent();
        }

        public async Task<FileResult?> PickAndShow(PickOptions options) {
            try {
                var result = await FilePicker.Default.PickAsync(options);

                return result;
            }
            catch (Exception ex) {
                // The user canceled or something went wrong
            }

            return null;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args) {
            if (_prefs.ContainsKey("ConnectionStringSet")) {
                return;
            }

            var result = await PickAndShow(new() { PickerTitle = "Pick db file" });
            if (result is null) {
                return;
            }

            Preferences.Default.Set("DefaultConnection", $"Data Source={result.FullPath};foreign keys=true;");
            try {
                await using var dc = _svcp.GetRequiredService<ReplayDbContext>();
                await dc.Database.MigrateAsync();
                Preferences.Default.Set("ConnectionStringSet", true);
                await DisplayAlert("Database Set", "Database Set Successfully", "Dismiss");
            }
            catch {
                await DisplayAlert("Database Error", "Selected database doesn't belong to this application", "Dismiss");
            }
        }

        private async void OnCounterClicked(object sender, EventArgs e) {
            await Shell.Current.GoToAsync("//Replays");
        }
    }

}
