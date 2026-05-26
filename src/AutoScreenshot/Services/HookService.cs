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

    // デリゲートを GC から保護するためフィールドに保持
    private NativeMethods.LowLevelProc? _mouseProcDelegate;
    private NativeMethods.LowLevelProc? _keyboardProcDelegate;
    private NativeMethods.WinEventProc? _winEventProcDelegate;

    // ドラッグ検知用: 左ボタン押下時刻
    private DateTime _lbDownTime = DateTime.MinValue;

    // ホイールアイドルタイマー
    private System.Threading.Timer? _wheelIdleTimer;

    // 設定参照 (ドラッグ閾値・ホイールアイドル時間)
    private readonly Func<TriggerConfig> _triggerConfig;

    public event EventHandler<TriggerType>? MouseEvent;
    public event EventHandler? KeyboardActivity;
    public event EventHandler? ActiveWindowChanged;

    public HookService(Func<TriggerConfig> triggerConfig)
    {
        _triggerConfig = triggerConfig;
    }

    public void Start()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        IntPtr hMod = NativeMethods.GetModuleHandle(module.ModuleName);

        _mouseProcDelegate    = MouseHookCallback;
        _keyboardProcDelegate = KeyboardHookCallback;
        _winEventProcDelegate = WinEventCallback;

        _mouseHook    = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProcDelegate, hMod, 0);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardProcDelegate, hMod, 0);
        _winEventHook = NativeMethods.SetWinEventHook(
            NativeMethods.EVENT_SYSTEM_FOREGROUND, NativeMethods.EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventProcDelegate, 0, 0, NativeMethods.WINEVENT_OUTOFCONTEXT);

        if (_mouseHook == IntPtr.Zero)
            Log.Warning("HookService: マウスフックの設置に失敗 (LastError={E})", Marshal.GetLastWin32Error());
        if (_keyboardHook == IntPtr.Zero)
            Log.Warning("HookService: キーボードフックの設置に失敗 (LastError={E})", Marshal.GetLastWin32Error());
        if (_winEventHook == IntPtr.Zero)
            Log.Warning("HookService: WinEventフックの設置に失敗");

        Log.Information("HookService: フック開始 (mouse={M}, keyboard={K}, winEvent={W})",
            _mouseHook != IntPtr.Zero, _keyboardHook != IntPtr.Zero, _winEventHook != IntPtr.Zero);
    }

    public void Stop()
    {
        _wheelIdleTimer?.Dispose();
        _wheelIdleTimer = null;

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
            var cfg = _triggerConfig();

            switch (msg)
            {
                case NativeMethods.WM_LBUTTONDOWN:
                    // ドラッグ検知のため押下時刻を記録するのみ。クリック判定は UP で行う。
                    _lbDownTime = DateTime.UtcNow;
                    break;

                case NativeMethods.WM_LBUTTONUP:
                    double elapsedMs = (DateTime.UtcNow - _lbDownTime).TotalMilliseconds;
                    if (_lbDownTime != DateTime.MinValue && elapsedMs >= cfg.DragThresholdMs)
                    {
                        // ドラッグ完了
                        Log.Debug("HookService: ドラッグ検知 ({Ms:F0}ms)", elapsedMs);
                        MouseEvent?.Invoke(this, TriggerType.MouseDragDrop);
                    }
                    else
                    {
                        // 通常クリック
                        MouseEvent?.Invoke(this, TriggerType.MouseLeftClick);
                    }
                    _lbDownTime = DateTime.MinValue;
                    break;

                case NativeMethods.WM_RBUTTONDOWN:
                    MouseEvent?.Invoke(this, TriggerType.MouseRightClick);
                    break;

                case NativeMethods.WM_MBUTTONDOWN:
                    MouseEvent?.Invoke(this, TriggerType.MouseMiddleClick);
                    break;

                case NativeMethods.WM_MOUSEWHEEL:
                    // 最終ホイールイベントから WheelIdleMs 後に1枚撮影するアイドルタイマー
                    _wheelIdleTimer?.Dispose();
                    _wheelIdleTimer = new System.Threading.Timer(_ =>
                    {
                        Log.Debug("HookService: ホイールアイドル完了 → 撮影");
                        MouseEvent?.Invoke(this, TriggerType.MouseWheel);
                    }, null, cfg.WheelIdleMs, System.Threading.Timeout.Infinite);
                    break;
            }
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                Log.Debug("HookService: キー入力検知");
                KeyboardActivity?.Invoke(this, EventArgs.Empty);
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void WinEventCallback(IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild, uint idEventThread, uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EVENT_SYSTEM_FOREGROUND)
        {
            Log.Debug("HookService: アクティブウィンドウ切替検知");
            ActiveWindowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose() => Stop();
}
