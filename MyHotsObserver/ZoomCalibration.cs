using System.Text.Json;
using MyReplayQuery.Abilities;

namespace MyHotsObserver;

/// <summary>
/// Each map's minimap outline width at the default zoom (see <see cref="ZoomMeter"/>), measured the
/// first time a clip on that map sets the zoom, kept in <c>cache\minimap-zoom.json</c>.
/// </summary>
public static class ZoomCalibration {
    private static readonly string File = Path.Combine(AbilitySlotTable.DataDirectory, "minimap-zoom.json");
    private static readonly Lock Gate = new();

    public static double? DefaultWidth(string map) {
        lock (Gate) {
            return Load().GetValueOrDefault(map) is var w and > 0 ? w : null;
        }
    }

    public static void Save(string map, double defaultWidth) {
        lock (Gate) {
            var widths = Load();
            widths[map] = Math.Round(defaultWidth, 1);
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            System.IO.File.WriteAllText(File, JsonSerializer.Serialize(widths, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static Dictionary<string, double> Load() =>
        System.IO.File.Exists(File)
            ? new(JsonSerializer.Deserialize<Dictionary<string, double>>(System.IO.File.ReadAllText(File)) ?? [], StringComparer.OrdinalIgnoreCase)
            : new(StringComparer.OrdinalIgnoreCase);
}
