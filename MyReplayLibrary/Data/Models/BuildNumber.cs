using System.ComponentModel.DataAnnotations;

namespace MyReplayLibrary.Data.Models;

public class BuildNumber {
    public int Buildnumber1 { get; init; }
    public DateTime? Builddate { get; init; }
    [MaxLength(450)]
    public string Version { get; init; } = null!;
}
