namespace MyHotsInfo.Pages;

public class PrematchViewModel {
    private PrematchViewModel(bool isLoading, string? error, PrematchTeam? myTeam, PrematchTeam? otherTeam) {
        IsLoading = isLoading;
        Error = error;
        MyTeam = myTeam;
        OtherTeam = otherTeam;
    }

    public bool IsLoading { get; }
    public string? Error { get; }
    public bool HasError => Error is not null;
    public bool HasTeams => MyTeam is not null;

    /// <summary>The team the app's owner is on (or the left side, when that's unknown).</summary>
    public PrematchTeam? MyTeam { get; }

    public PrematchTeam? OtherTeam { get; }

    public static PrematchViewModel Loading() => new(true, null, null, null);
    public static PrematchViewModel Failed(string error) => new(false, error, null, null);
    public static PrematchViewModel Loaded(PrematchTeam myTeam, PrematchTeam otherTeam) => new(false, null, myTeam, otherTeam);
}
