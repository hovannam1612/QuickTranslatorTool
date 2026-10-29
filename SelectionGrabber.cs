using System.Runtime.InteropServices;
using static QuickTranslator.NativeMethods;

namespace QuickTranslator;

/// <summary>Reads the currently selected text of the foreground app by simulating Ctrl+C.</summary>
internal static class SelectionGrabber
{
    /// <summary>Must be called on the UI (STA) thread. Returns an empty string if nothing was selected.</summary>
    public static async Task<string> GrabAsync()
    {
        // Wait until the user lets go of the shortcut keys, otherwise our Ctrl+C would be mixed up with them.
        for (int i = 0; i < 50 && (IsKeyDown(VK_CONTROL) || IsKeyDown(VK_MENU) || IsKeyDown(VK_SHIFT)); i++)
            await Task.Delay(20);

        uint sequenceBefore = GetClipboardSequenceNumber();
        string? previousText = await TryGetTextAsync();

        SendCtrlC();

        bool changed = false;
        for (int i = 0; i < 20 && !changed; i++)
        {
            await Task.Delay(25);
            changed = GetClipboardSequenceNumber() != sequenceBefore;
        }
        if (!changed) return ""; // nothing was copied => no selection, clipboard untouched

        string text = await TryGetTextAsync() ?? "";

        // Put the user's original clipboard text back (non-text contents cannot be restored).
        if (previousText != null)
            await TrySetTextAsync(previousText);

        return text;
    }

    private static async Task<string?> TryGetTextAsync()
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (ExternalException)
            {
                await Task.Delay(25); // clipboard is locked by another process
            }
        }
        return null;
    }

    private static async Task TrySetTextAsync(string text)
    {
        for (int i = 0; i < 8; i++)
        {
            try
            {
                if (text.Length == 0) Clipboard.Clear();
                else Clipboard.SetText(text);
                return;
            }
            catch (ExternalException)
            {
                await Task.Delay(25);
            }
        }
    }
}
