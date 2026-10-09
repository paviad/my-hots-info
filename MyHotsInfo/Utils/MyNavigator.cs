using Heroes.ReplayParser;

namespace MyHotsInfo.Utils;

public class MyNavigator {
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;

    public void GoToReplay(int replayId, bool replace = false) {
        var pref = replace ? "//Replays/" : "//Replays/";
        Do(async () => {
            await Shell.Current.GoToAsync($"{pref}Replay?id={replayId}");
        });
    }

    public void GoToPrematch(List<string> names, bool replace = false) {
        var pref = replace ? "//Replays/" : "//Replays/";
        Do(async () => {
            ShellNavigationQueryParameters navParams = new() {
                { "names", names },
            };
            await Shell.Current.GoToAsync($"{pref}Prematch", navParams);
        });
    }

    // Pushed onto the current stack so Back returns to the page the name was clicked on.
    // The battle tag goes in the parameters, not the query string, because of its '#'.
    public void GoToPlayer(string battleTag) {
        Do(async () => {
            ShellNavigationQueryParameters navParams = new() {
                { "battleTag", battleTag },
            };
            await Shell.Current.GoToAsync("Player", navParams);
        });
    }

    private void Do(Func<Task> action) {
        _sync?.Post(Nav, null);

        return;

        async void Nav(object? _) {
            try {
                await action();
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
