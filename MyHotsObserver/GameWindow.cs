using System.Diagnostics;
using System.Runtime.InteropServices;
using ModelContextProtocol;

namespace MyHotsObserver;

/// <summary>The running HotS client's window.</summary>
public sealed partial class GameWindow {
    public const string ProcessName = "HeroesOfTheStorm_x64";

    static GameWindow() {
        // Without this, window sizes come back scaled on monitors with display scaling.
        SetProcessDpiAwarenessContext(-4 /* DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 */);
    }

    /// <summary>The client's window and its client-area size; throws when the game isn't running.</summary>
    public (IntPtr Handle, int Width, int Height) Find() {
        var process = Process.GetProcessesByName(ProcessName).FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero)
                      ?? throw new McpException(
                          "Heroes of the Storm isn't running. Start it from the Battle.net launcher and wait for the home screen.");
        var handle = process.MainWindowHandle;
        if (IsIconic(handle)) {
            throw new McpException(
                "The game window is minimized. Use Windowed (Fullscreen) display mode so it keeps drawing in the background.");
        }

        GetClientRect(handle, out var rect);
        return (handle, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

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
