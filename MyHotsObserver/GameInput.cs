using System.Runtime.InteropServices;
using ModelContextProtocol;

namespace MyHotsObserver;

/// <summary>Keys the observer UI uses, as keyboard scan codes (the game reads scan codes).</summary>
public enum Key : ushort {
    D1 = 0x02, D2, D3, D4, D5, D6, D7, D8, D9, D0,
    Minus = 0x0C,
    Equals = 0x0D,
    Tab = 0x0F,
    Q = 0x10,
    E = 0x12,
    T = 0x14,
    P = 0x19,
    Control = 0x1D,
    F = 0x21,
    Alt = 0x38,
    F10 = 0x44,
}

/// <summary>
/// Sends keys and clicks to the game. Input only reaches the foreground window, so each
/// <see cref="Begin"/> brings the game to the front and its dispose gives the front back to
/// whatever window had it.
/// </summary>
public sealed partial class GameInput(GameWindow gameWindow) {
    public Session Begin() {
        var (game, _, _) = gameWindow.Find();
        var previous = GetForegroundWindow();
        if (previous != game && !BringToFront(game)) {
            throw new McpException("Couldn't bring the game window to the front to send it input.");
        }

        // A just-activated window can drop the first keys it gets.
        Thread.Sleep(previous == game ? 0 : 300);
        return new Session(game, previous);
    }

    public sealed class Session(IntPtr game, IntPtr previous) : IDisposable {
        /// <summary>Presses and releases <paramref name="key"/> while holding <paramref name="modifiers"/>.</summary>
        public void Press(Key key, params Key[] modifiers) {
            foreach (var m in modifiers) {
                SendKey(m, up: false);
            }

            SendKey(key, up: false);
            Thread.Sleep(40);
            SendKey(key, up: true);
            foreach (var m in modifiers.Reverse()) {
                SendKey(m, up: true);
            }

            Thread.Sleep(60);
        }

        /// <summary>Left-clicks at a point of the game's client area and puts the cursor back.</summary>
        public void Click(int x, int y) {
            GetCursorPos(out var cursor);
            var point = new Point { X = x, Y = y };
            ClientToScreen(game, ref point);
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(60);
            SendMouse(MouseLeftDown);
            Thread.Sleep(40);
            SendMouse(MouseLeftUp);
            Thread.Sleep(60);
            SetCursorPos(cursor.X, cursor.Y);
        }

        public void Dispose() {
            if (previous != IntPtr.Zero && previous != game && GetForegroundWindow() == game) {
                BringToFront(previous);
            }
        }
    }

    /// <summary>
    /// Windows lets a background process take the foreground only right after keyboard input, so
    /// tap Alt first.
    /// </summary>
    private static bool BringToFront(IntPtr window) {
        for (var attempt = 0; attempt < 3; attempt++) {
            SendKey(Key.Alt, up: false);
            SendKey(Key.Alt, up: true);
            SetForegroundWindow(window);
            Thread.Sleep(100);
            if (GetForegroundWindow() == window) {
                return true;
            }
        }

        return false;
    }

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint ScanCode = 0x0008;
    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;

    private static void SendKey(Key key, bool up) {
        var input = new Input {
            Type = InputKeyboard,
            U = new InputUnion { Ki = new KeybdInput { Scan = (ushort)key, Flags = ScanCode | (up ? KeyUp : 0) } },
        };
        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendMouse(uint flags) {
        var input = new Input { Type = InputMouse, U = new InputUnion { Mi = new MouseInput { Flags = flags } } };
        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input {
        public uint Type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion {
        [FieldOffset(0)] public MouseInput Mi;
        [FieldOffset(0)] public KeybdInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput {
        public int Dx, Dy;
        public uint MouseData, Flags, Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeybdInput {
        public ushort Vk, Scan;
        public uint Flags, Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point {
        public int X, Y;
    }

    [LibraryImport("user32.dll")]
    private static partial uint SendInput(uint count, [In] Input[] inputs, int size);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetCursorPos(int x, int y);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hWnd, ref Point point);
}
