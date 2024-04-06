using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MyReplayLibrary.Data.Models;

namespace MyHotsInfo;

public sealed class ReplaysPageViewModel : INotifyPropertyChanged {
    private string? _mapName;
    private ReplayEntry? _selectedReplay;

    private readonly Dictionary<string, string> _mapDic = new() {
        ["Blackheart's Bay"] = "map_blackheartsbay.png",
        ["Cursed Hollow"] = "map_cursedhollow.png",
        ["Dragon Shire"] = "map_dragonshire.png",
        ["Haunted Mines"] = "map_hauntedmines.png",
        ["Lost Cavern"] = "map_lostcavern.png",
        ["Towers of Doom"] = "map_towersofdoom.png",
        ["Tomb of the Spider Queen"] = "map_tombspiderqueen.png",
        ["Sky Temple"] = "map_skytemple.png",
        ["Infernal Shrines"] = "map_shrines.png",
        ["Garden of Terror"] = "map_gardenofterror.png",
        ["Battlefield of Eternity"] = "map_boe.png",
        ["Braxis Holdout"] = "map_braxisholdout.png",
        ["Braxis Holdout"] = "map_braxisoutpost.png",
        ["Warhead Junction"] = "map_warheadjunction.png",
        ["Infernal Shrines"] = "map_shrinessimple.png",
        ["Sky Temple"] = "map_skytemplesimple.png",
        ["Garden of Terror"] = "map_gardensimple.png",
        ["Blackheart's Revenge"] = "map_blackheartssimple.png",
        ["Silver City"] = "map_silvercity.png",
        ["Hanamura"] = "map_hanamura.png",
        ["Dodge Brawl"] = "map_dodgebrawl.png",
        ["Volskaya Foundry"] = "map_volskaya.png",
        ["Trial Grounds"] = "map_trialgrounds.png",
        ["Industrial District"] = "map_industrialdistrict.png",
        ["Escape From Braxis"] = "map_efb.png",
        ["Escape From Braxis (Heroic)"] = "map_efbh.png",
        ["Deadman's Stand (Heroic)"] = "map_dmsn.png",
        ["Deadman's Stand (Heroic)"] = "map_dmsh.png",
        ["Lunar Festival"] = "map_lunarfestival.png",
        ["Alterac Pass"] = "map_alteracpass.png",
    };

    public ObservableCollection<ReplayEntry> Replays { get; set; } = [];

    public ReplayEntry? SelectedReplay {
        get => _selectedReplay;
        set {
            if (Equals(value, _selectedReplay)) {
                return;
            }

            _selectedReplay = value;
            MapName = _mapDic.GetValueOrDefault(value!.MapId);
            OnPropertyChanged();
        }
    }

    public string? MapName {
        get => _mapName;
        set {
            if (value == _mapName) {
                return;
            }

            _mapName = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
