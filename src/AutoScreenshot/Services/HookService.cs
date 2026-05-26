using System.Diagnostics;
using System.Runtime.InteropServices;
using AutoScreenshot.Models;
using AutoScreenshot.Native;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>マウス・キーボード・ウィンドウ切替イベントの検知</summary>
public class HookService : IDisposable
{
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _winEventHook = IntPtr.Zero;

    // フック デリゲートを GC から保護するためフィールドに保持
    private NativeMethods.LowLevelProc? _mouseProcDelegate;
    private NativeMethods.LowLevelProc? _keyboardProcDelegate;
    private NativeMethods.WinEventProc? _winEventProcDelegate;

    public event EventHandler<TriggerType>? MouseEvent;
    public event EventHandler? KeyboardActivity;
    public event EventHandler? ActiveWindowChanged;

    public void Start()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        IntPtr hMod = NativeMethods.GetModuleHandle(module.ModuleName);

        _mouseProcDelegate = MouseHookCallback;
        _keyboardProcDelegate = KeyboardHookCallback;
        _winEventProcDelegate = WinEventCallback;

        _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProcDelegate, hMod, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProcDelegate, hMod, 0);
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProcDelegate, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        Log.Information("HookService: フック開始");
    }

    public void Stop()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        Log.Information("HookService: フック停止");
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            TriggerType? trigger = msg switch
            {
                NativeMethods.WM_LBUTTONDOWN => TriggerType.MouseLeftClick,
                NativeMethods.WM_RBUTTONDOWN => TriggerType.MouseRightClick,
                NativeMethods.WM_MBUTTONDOWN => TriggerType.MouseMiddleClick,
                NativeMethods.WM_MOUSEWHEEL  => TriggerType.MouseWheel,
                _ => null,
            };
            if (trigger.HasValue)
                MouseEvent?.Invoke(this, trigger.Value);
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                KeyboardActivity?.Invoke(this, EventArgs.Empty);
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
            ActiveWindowChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();
}
