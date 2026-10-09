using System.Text;
using MyReplayQuery.Events;

namespace MyReplayQuery.Query;

public static class ResultFormatter {
    /// <summary>Game time as m:ss.f (replay time; the in-game clock agrees to within a second or two).</summary>
    public static string Time(double seconds) {
        var whole = (int)Math.Floor(seconds);
        var tenths = (int)Math.Floor((seconds - whole) * 10);
        return $"{whole / 60}:{whole % 60:00}.{tenths}";
    }

    /// <summary>Parses m:ss, m:ss.f or plain seconds.</summary>
    public static double ParseTime(string text) {
        var parts = text.Split(':');
        return parts.Length switch {
            1 => double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
            2 => int.Parse(parts[0]) * 60 + double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new FormatException($"Bad time '{text}', expected m:ss"),
        };
    }

    public static string Header(ReplayTimeline timeline) =>
        $"{Path.GetFileName(timeline.Source)} — {timeline.Map}, build {timeline.Build}, {Time(timeline.Length)}"
        + (timeline.Me is { } me ? $", me = {me}" : ", me unknown");

    public static string Event(ReplayEvent e) => $"{Time(e.T),9}  {e.Describe()}";

    public static string Match(Match m) {
        var sb = new StringBuilder();
        var span = m.Start == m.End ? Time(m.Start) : $"{Time(m.Start)} – {Time(m.End)}";
        var bindings = m.Bindings.Count == 0 ? "" : "  " + string.Join(" ", m.Bindings.OrderBy(kv => kv.Key).Select(kv => $"${kv.Key}={kv.Value}"));
        var notes = m.Notes.Count == 0 ? "" : $"  [{string.Join("; ", m.Notes)}]";
        sb.AppendLine($"  {span}{bindings}{notes}");
        foreach (var e in m.Events) {
            sb.AppendLine($"    {Event(e)}");
        }

        return sb.ToString();
    }

    public static string Result(FindResult r) {
        var sb = new StringBuilder();
        sb.AppendLine($"find {r.Find.Text}  →  {r.Matches.Count} match{(r.Matches.Count == 1 ? "" : "es")}"
                      + (r.Truncated > 0 ? $" ({r.Truncated} undecided: the game ended inside the window)" : ""));
        foreach (var m in r.Matches) {
            sb.Append(Match(m));
        }

        return sb.ToString();
    }
}
