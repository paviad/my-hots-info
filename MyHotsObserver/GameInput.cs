using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;

namespace MyHotsObserver;

/// <summary>Keys the observer UI uses, as keyboard scan codes (the game reads scan codes).</summary>
public enum Key : ushort {
    D1 = 0x02, D2, D3, D4, D5, D6, D7, D8, D9, D0,
    Minus = 0x0C,
    Backspace = 0x0E,
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
/// <see cref="Begin"/> brings the game to the front, and its dispose gives the front back to the
/// user's window: the last window other than the game that was in front.
/// </summary>
public sealed partial class GameInput(GameWindow gameWindow, ILogger<GameInput> logger) {
    /// <summary>
    /// Remembered across sessions: when one session couldn't give the front back, the next one
    /// finds the game in front and must still know where to return.
    /// </summary>
    private IntPtr _userWindow;

    public Session Begin() {
        var (game, _, _) = gameWindow.Find();
        var foreground = GetForegroundWindow();
        if (foreground != game && foreground != IntPtr.Zero) {
            _userWindow = foreground;
        }

        if (foreground != game && !BringToFront(game)) {
            throw new McpException("Couldn't bring the game window to the front to send it input.");
        }

        // A just-activated window can drop the first keys it gets.
        Thread.Sleep(foreground == game ? 0 : 300);
        return new Session(this, game);
    }

    /// <summary>Gives the front back to the user's window if the game still has it.</summary>
    public void ReturnFocus() {
        if (_userWindow == IntPtr.Zero || !IsWindow(_userWindow)) {
            return;
        }

        var (game, _, _) = gameWindow.Find();
        if (GetForegroundWindow() == game && !BringToFront(_userWindow)) {
            logger.LogWarning("Couldn't give the front back from the game to window {window}", _userWindow);
        }
    }

    public sealed class Session(GameInput owner, IntPtr game) : IDisposable {
        /// <summary>Presses and releases <paramref name="key"/> while holding <paramref name="modifiers"/>.</summary>
        public void Press(Key key, params Key[] modifiers) {
            foreach (var m in modifiers) {
                SendKey(m, up: false);
            }

            // Held across a few frames: the game reads the keyboard once a frame.
            SendKey(key, up: false);
            Thread.Sleep(80);
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
            // Slow enough to span game frames, which a recording makes longer: the cursor rests on
            // the target a moment, and the button is held across a few frames.
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(100);
            SendMouse(MouseLeftDown);
            Thread.Sleep(100);
            SendMouse(MouseLeftUp);
            Thread.Sleep(80);
            SetCursorPos(cursor.X, cursor.Y);
        }

        /// <summary>
        /// Moves the cursor to a point of the game's client area and holds the left button down
        /// there until <see cref="Release"/>. Held on the minimap, the camera stays on that spot
        /// whatever the game would do, e.g. jump to a dead hero's core.
        /// </summary>
        public void Hold(int x, int y) {
            GetCursorPos(out _cursor);
            var point = new Point { X = x, Y = y };
            ClientToScreen(game, ref point);
            SetCursorPos(point.X, point.Y);
            Thread.Sleep(100);
            SendMouse(MouseLeftDown);
            _holding = true;
        }

        /// <summary>Lets go of a <see cref="Hold"/> and puts the cursor back.</summary>
        public void Release() {
            if (!_holding) {
                return;
            }

            SendMouse(MouseLeftUp);
            _holding = false;
            Thread.Sleep(60);
            SetCursorPos(_cursor.X, _cursor.Y);
        }

        private bool _holding;
        private Point _cursor;

        public void Dispose() {
            Release();
            owner.ReturnFocus();
        }
    }

    /// <summary>
    /// Windows lets a background process take the foreground only right after keyboard input, so
    /// tap Alt first.
    /// </summary>
    private static bool BringToFront(IntPtr window) {
        for (var attempt = 0; attempt < 5; attempt++) {
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
    private static partial bool IsWindow(IntPtr hWnd);

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
