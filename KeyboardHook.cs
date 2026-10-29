using System.Runtime.InteropServices;
using static QuickTranslator.NativeMethods;

namespace QuickTranslator;

internal enum TriggerMode
{
    DoubleCtrl,
    DoubleAlt,
    CtrlAltT,
    Custom,
}

/// <summary>
/// Global low-level keyboard hook that raises <see cref="Triggered"/> when the configured
/// shortcut is used (double-tap of Ctrl/Alt, Ctrl+Alt+T, or a custom combination). Only the key of a
/// custom combination is swallowed, so the foreground app does not also react to it.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int DoubleTapWindowMs = 400;

    private readonly Control _uiInvoker;
    private readonly LowLevelKeyboardProc _proc; // kept in a field so the GC does not collect the delegate
    private IntPtr _hookId;

    private bool _modifierDown;
    private bool _otherKeyDuringTap;
    private long _lastTapTick;
    private int _swallowedKey; // custom shortcut key currently held down (its key-up is swallowed too)

    public TriggerMode Mode { get; set; } = TriggerMode.DoubleCtrl;

    public Hotkey? CustomHotkey { get; set; }

    /// <summary>Set to false to pause the shortcut, e.g. while the user is recording a new one.</summary>
    public bool Enabled { get; set; } = true;

    public event Action? Triggered;

    public KeyboardHook(Control uiInvoker)
    {
        _uiInvoker = uiInvoker;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
            throw new InvalidOperationException("Could not install keyboard hook (error " + Marshal.GetLastWin32Error() + ").");
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Enabled)
        {
            var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            // Ignore keys we synthesised ourselves (the Ctrl+C used to grab the selection).
            if ((info.flags & LLKHF_INJECTED) == 0)
            {
                int msg = (int)wParam;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool up = msg is WM_KEYUP or WM_SYSKEYUP;
                if (down || up)
                {
                    bool fired = Process((int)info.vkCode, down, out bool swallow);
                    if (fired)
                    {
                        if (Mode == TriggerMode.Custom && CustomHotkey is { Alt: true }) TapUnassignedKey();
                        _uiInvoker.BeginInvoke(() => Triggered?.Invoke());
                    }
                    if (swallow) return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    /// <summary>Returns true when the shortcut has just been completed; <paramref name="swallow"/> hides the key from other apps.</summary>
    private bool Process(int vk, bool down, out bool swallow)
    {
        swallow = false;

        if (Mode == TriggerMode.Custom)
            return ProcessCustom(vk, down, out swallow);

        if (Mode == TriggerMode.CtrlAltT)
            return down && vk == 'T' && IsKeyDown(VK_CONTROL) && IsKeyDown(VK_MENU);

        bool isTapKey = Mode == TriggerMode.DoubleCtrl
            ? vk is VK_CONTROL or 0xA2 or 0xA3
            : vk is VK_MENU or 0xA4 or 0xA5;

        if (!isTapKey)
        {
            // Any other key cancels the tap sequence (e.g. Ctrl+C, Alt+Tab).
            if (down)
            {
                _otherKeyDuringTap = true;
                _lastTapTick = 0;
            }
            return false;
        }

        if (down)
        {
            if (!_modifierDown) // ignore auto-repeat
            {
                _modifierDown = true;
                _otherKeyDuringTap = false;
            }
            return false;
        }

        bool cleanTap = _modifierDown && !_otherKeyDuringTap;
        _modifierDown = false;
        if (!cleanTap)
        {
            _lastTapTick = 0;
            return false;
        }

        long now = Environment.TickCount64;
        if (_lastTapTick != 0 && now - _lastTapTick <= DoubleTapWindowMs)
        {
            _lastTapTick = 0;
            return true;
        }
        _lastTapTick = now;
        return false;
    }

    private bool ProcessCustom(int vk, bool down, out bool swallow)
    {
        swallow = false;
        var hotkey = CustomHotkey;
        if (hotkey == null || !hotkey.IsValid) return false;

        if (vk == _swallowedKey)
        {
            swallow = true; // auto-repeat or key-up of the shortcut key we already handled
            if (!down) _swallowedKey = 0;
            return false;
        }

        if (down && vk == hotkey.Key
            && IsKeyDown(VK_CONTROL) == hotkey.Ctrl
            && IsKeyDown(VK_MENU) == hotkey.Alt
            && IsKeyDown(VK_SHIFT) == hotkey.Shift)
        {
            _swallowedKey = vk;
            swallow = true;
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
