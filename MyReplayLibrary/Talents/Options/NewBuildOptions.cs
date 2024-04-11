namespace MyReplayLibrary.Talents.Options;

public class NewBuildOptions : GlobalOptions
{
    public bool? DryRun { get; set; }

    public string? Path { get; set; }

    public string? TargetImagePath { get; set; }
}
