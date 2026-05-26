using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using AutoScreenshot.Native;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>グローバルホットキーの登録・解除（HWND_MESSAGE ウィンドウ使用）</summary>
public class HotkeyService : NativeWindow, IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;

    public event EventHandler? HotkeyPressed;

    private bool _registered;

    public HotkeyService()
    {
        // メッセージ専用ウィンドウを作成 (画面に表示されない)
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) }); // HWND_MESSAGE
    }

    /// <summary>"Ctrl+F9" 形式の文字列でホットキーを登録する</summary>
    public bool Register(string? hotkeyString)
    {
        Unregister();
        if (string.IsNullOrWhiteSpace(hotkeyString)) return false;

        if (!TryParseHotkey(hotkeyString, out uint mods, out uint vk))
        {
            Log.Warning("HotkeyService: ホットキー解析失敗: {Hotkey}", hotkeyString);
            return false;
        }

        bool ok = NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, mods | NativeMethods.MOD_NOREPEAT, vk);
        if (ok)
        {
            _registered = true;
            Log.Information("HotkeyService: ホットキー登録成功: {Hotkey}", hotkeyString);
        }
        else
        {
            Log.Warning("HotkeyService: ホットキー登録失敗 (他アプリが使用中の可能性): {Hotkey}", hotkeyString);
        }
        return ok;
    }

    public void Unregister()
    {
        if (_registered && Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
            _registered = false;
            Log.Debug("HotkeyService: ホットキー解除");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && (int)m.WParam == HOTKEY_ID)
            HotkeyPressed?.Invoke(this, EventArgs.Empty);

        base.WndProc(ref m);
    }

    /// <summary>"Ctrl+Alt+F9" → modifiers + VK コードに変換する</summary>
    public static bool TryParseHotkey(string input, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        var parts = input.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 1) return false;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            modifiers |= parts[i].ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => NativeMethods.MOD_CONTROL,
                "ALT"               => NativeMethods.MOD_ALT,
                "SHIFT"             => NativeMethods.MOD_SHIFT,
                "WIN" or "WINDOWS"  => NativeMethods.MOD_WIN,
                _                   => 0u,
            };
        }

        // WPF Key 名 → VK コード変換
        try
        {
            var converter = new KeyConverter();
            var key = (Key)converter.ConvertFromString(parts[^1])!;
            int vkInt = KeyInterop.VirtualKeyFromKey(key);
            if (vkInt <= 0) return false;
            vk = (uint)vkInt;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>WPF の Key + ModifierKeys → "Ctrl+F9" 形式文字列に変換する</summary>
    public static string KeyToString(ModifierKeys modifiers, Key key)
    {
        var sb = new StringBuilder();
        if (modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
        if (modifiers.HasFlag(ModifierKeys.Alt))     sb.Append("Alt+");
        if (modifiers.HasFlag(ModifierKeys.Shift))   sb.Append("Shift+");
        sb.Append(new KeyConverter().ConvertToString(key));
        return sb.ToString();
    }

    public void Dispose()
    {
        Unregister();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
