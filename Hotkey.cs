namespace QuickTranslator;

/// <summary>A user-defined shortcut: Ctrl/Alt/Shift modifiers plus one key (a virtual-key code).</summary>
internal sealed class Hotkey
{
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public bool Shift { get; set; }
    public int Key { get; set; }

    /// <summary>Needs a real key and at least one modifier (function keys may be used on their own).</summary>
    public bool IsValid => Key != 0 && !IsModifierKey(Key) && (Ctrl || Alt || Shift || IsFunctionKey(Key));

    public static bool IsModifierKey(int vk) => (Keys)vk is Keys.ControlKey or Keys.ShiftKey or Keys.Menu
        or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu
        or Keys.LWin or Keys.RWin;

    public static bool IsFunctionKey(int vk) => vk is >= (int)Keys.F1 and <= (int)Keys.F24;

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Key != 0) parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    public static string KeyName(int vk)
    {
        var key = (Keys)vk;
        string name = key switch
        {
            Keys.Return => "Enter",
            Keys.Back => "Backspace",
            Keys.Prior => "PageUp",
            Keys.Next => "PageDown",
            Keys.Capital => "CapsLock",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.Oemtilde => "`",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemPipe => "\\",
            _ => key.ToString(),
        };
        // Keys.D1 -> "1"
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) name = name[1..];
        return name;
    }
}
