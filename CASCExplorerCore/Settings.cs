using System.Collections.Specialized;
using System.ComponentModel;
using CascLibCore;

namespace CASCExplorerCore;

internal static class Settings {
    public static SettingsCollection Default { get; set; } = new();
}

internal class SettingsCollection {
    public LocaleFlags LocaleFlags { get; set; } = LocaleFlags.enUS;
    public ContentFlags ContentFlags { get; set; } = ContentFlags.None;
    public StringCollection RecentStorages { get; set; } = [];

    public event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

    public void Save() {
    }

    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e) {
        PropertyChanged?.Invoke(this, e);
    }
}
