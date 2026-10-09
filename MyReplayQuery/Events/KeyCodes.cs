namespace MyReplayQuery.Events;

/// <summary>
/// Physical key codes in <c>CTriggerKeyPressedEvent</c>: Shift=0, Ctrl=1, Alt=2, digits 0-9 = 3-12, A-Z = 13-38.
/// Only keys the game registers a trigger for are recorded, and they are physical keys, not ability slots.
/// </summary>
public static class KeyCodes {
    public static string Name(int code) => code switch {
        0 => "Shift",
        1 => "Ctrl",
        2 => "Alt",
        >= 3 and <= 12 => ((char)('0' + code - 3)).ToString(),
        >= 13 and <= 38 => ((char)('A' + code - 13)).ToString(),
        _ => $"Key{code}",
    };

    /// <summary>The ability slot a key maps to under default hotkeys, or null for keys that are not ability slots.</summary>
    public static string? DefaultSlot(int code) => Name(code) switch {
        "Q" or "W" or "E" or "R" or "D" or "Z" or "B" => Name(code),
        "A" => "Attack",
        "1" or "2" or "3" or "4" or "5" => "Active" + Name(code),
        _ => null,
    };
}
