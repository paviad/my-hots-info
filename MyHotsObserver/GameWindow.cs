using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ModelContextProtocol;

namespace MyHotsObserver;

/// <summary>The running HotS client's window.</summary>
public sealed partial class GameWindow {
    public const string ProcessName = "HeroesOfTheStorm_x64";

    static GameWindow() {
        // Without this, window sizes come back scaled on monitors with display scaling.
        SetProcessDpiAwarenessContext(-4 /* DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 */);
    }

    /// <summary>
    /// The client's window and its client-area size; throws when the game isn't running, or runs
    /// with the user's own settings rather than the observer's (<see cref="ClientSession"/>).
    /// </summary>
    public (IntPtr Handle, int Width, int Height) Find() {
        var process = Process.GetProcessesByName(ProcessName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero)
                      ?? throw new McpException(
                          "Heroes of the Storm isn't running. open_replay (or record_clip with a replay) starts it.");
        if (!ClientSession.Swapped) {
            throw new McpException(ClientSession.UsersClientMessage);
        }

        var handle = process.MainWindowHandle;
        if (IsIconic(handle)) {
            throw new McpException(
                "The game window is minimized. Use Windowed (Fullscreen) display mode so it keeps drawing in the background.");
        }

        GetClientRect(handle, out var rect);
        return (handle, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    /// <summary>
    /// The build of the client that's running, from its install folder (Versions\Base98348);
    /// null when no client with a window is running or its path can't be read.
    /// </summary>
    public static int? RunningBuild() {
        foreach (var process in Process.GetProcessesByName(ProcessName).Where(p => p.MainWindowHandle != IntPtr.Zero)) {
            try {
                if (BuildFolder().Match(process.MainModule?.FileName ?? "") is { Success: true } m) {
                    return int.Parse(m.Groups[1].Value);
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) {
                // Exited, or not ours to read.
            }
        }

        return null;
    }

    /// <summary>The id of the running client with a window; null when none runs.</summary>
    public static int? RunningProcessId() =>
        Process.GetProcessesByName(ProcessName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero)?.Id;

    [GeneratedRegex(@"\\Base(\d+)\\", RegexOptions.IgnoreCase)]
    private static partial Regex BuildFolder();

    public ObserverLayout Layout() {
        var (_, width, height) = Find();
        try {
            return new ObserverLayout(width, height);
        }
        catch (NotSupportedException e) {
            throw new McpException(e.Message);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect {
        public int Left, Top, Right, Bottom;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(IntPtr hWnd, out Rect rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessDpiAwarenessContext(nint value);
}
