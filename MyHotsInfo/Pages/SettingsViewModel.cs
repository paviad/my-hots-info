using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyReplayLibrary;

namespace MyHotsInfo.Pages;

/// <summary>The settings page: whose replay folders are the owner's, and the database file.</summary>
public class SettingsViewModel : ObservableBase {
    public List<SettingsFolder> Folders { get; private set => SetField(ref field, value); } = [];

    /// <summary>Set once the folders were listed and there were none.</summary>
    public bool HasNoFolders { get; private set => SetField(ref field, value); }

    /// <summary>Whether any folder's choice was set by hand rather than left to the default.</summary>
    public bool HasChoices { get; private set => SetField(ref field, value); }

    public string? DatabasePath { get; set => SetField(ref field, value); }

    public string? SettingsPath { get; set => SetField(ref field, value); }

    public void SetFolders(List<SettingsFolder> folders) {
        Folders = folders;
        HasNoFolders = folders.Count == 0;
        HasChoices = folders.Any(f => f.Folder.Chosen is not null);
    }
}

/// <summary>One toon folder on the settings page.</summary>
public class SettingsFolder(ReplayFolder folder, Action<SettingsFolder, bool> onChanged) : ObservableBase {
    public ReplayFolder Folder { get; private set; } = folder;

    /// <summary>The checkbox: whether its games are imported as the owner's.</summary>
    public bool IsMine {
        get => Folder.IsMine;
        set {
            if (value != Folder.IsMine) {
                onChanged(this, value);
            }
        }
    }

    /// <summary>The player name from its newest replay, once read.</summary>
    public string? PlayerName {
        get;
        set {
            if (SetField(ref field, value)) {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public string Title => PlayerName ?? $"Account {Folder.Account}";

    public string Details =>
        $"{Folder.RegionName} · account {Folder.Account} · {Folder.NumReplays:N0} {(Folder.NumReplays == 1 ? "replay" : "replays")}";

    /// <summary>Says why it's checked or not: the default, or the owner's choice and how it differs.</summary>
    public string Source => Folder.Chosen is null
        ? "Default"
        : Folder.Chosen == Folder.MineByDefault
            ? "Your choice (same as default)"
            : "Your choice";

    public bool IsChosen => Folder.Chosen is not null;

    public void Update(ReplayFolder folder) {
        Folder = folder;
        OnPropertyChanged(nameof(IsMine));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Source));
        OnPropertyChanged(nameof(IsChosen));
    }
}

public abstract class ObservableBase : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
