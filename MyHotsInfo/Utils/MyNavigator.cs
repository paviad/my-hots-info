using Heroes.ReplayParser;

namespace MyHotsInfo.Utils;

public class MyNavigator {
    private readonly SynchronizationContext? _sync = SynchronizationContext.Current;

    public void GoToReplay(int replayId, bool replace = false) {
        var pref = replace ? "../" : "";
        Do(async () => {
            await Shell.Current.GoToAsync($"{pref}Replay?id={replayId}");
        });
    }

    public void GoToPrematch(List<string> names, bool replace = false) {
        var pref = replace ? "../" : "";
        Do(async () => {
            ShellNavigationQueryParameters navParams = new() {
                { "names", names },
            };
            await Shell.Current.GoToAsync($"{pref}Prematch", navParams);
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
