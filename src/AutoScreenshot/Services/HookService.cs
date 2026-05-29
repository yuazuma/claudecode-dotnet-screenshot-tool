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

    // キーボード入力蓄積バッファ (E-04)
    private readonly System.Text.StringBuilder _inputTextBuf = new();
    private readonly System.Collections.Generic.List<string> _keyCodeBuf = [];
    private readonly object _keyBufLock = new();
    private bool _shiftDown;
    private bool _ctrlDown;
    private bool _altDown;

    // キーボードセッション追跡（before 撮影用）
    private bool _inKeyboardSession;

    public event EventHandler<TriggerType>? MouseBeforeEvent;
    public event EventHandler<TriggerType>? MouseEvent;
    public event EventHandler? KeyboardBeforeEvent;
    public event EventHandler? KeyboardActivity;
    public event EventHandler? ActiveWindowChanged;

    /// <summary>蓄積されたキーボード入力を取り出してバッファをクリアする。セッションフラグもリセット。</summary>
    public (string inputText, string keyCodes) TakeAccumulatedKeys()
    {
        lock (_keyBufLock)
        {
            string text = _inputTextBuf.ToString();
            string codes = string.Join(", ", _keyCodeBuf);
            _inputTextBuf.Clear();
            _keyCodeBuf.Clear();
            _inKeyboardSession = false;
            return (text, codes);
        }
    }

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
                    _lbDownTime = DateTime.UtcNow;
                    MouseBeforeEvent?.Invoke(this, TriggerType.MouseLeftClick);
                    break;

                case NativeMethods.WM_LBUTTONUP:
                {
                    double elapsedMs = (DateTime.UtcNow - _lbDownTime).TotalMilliseconds;
                    TriggerType afterTrigger;
                    if (_lbDownTime != DateTime.MinValue && elapsedMs >= cfg.DragThresholdMs)
                    {
                        Log.Debug("HookService: ドラッグ検知 ({Ms:F0}ms)", elapsedMs);
                        afterTrigger = TriggerType.MouseDragDrop;
                    }
                    else
                    {
                        afterTrigger = TriggerType.MouseLeftClick;
                    }
                    _lbDownTime = DateTime.MinValue;
                    FireAfterDelayed(afterTrigger, cfg.PostClickDelayMs);
                    break;
                }

                case NativeMethods.WM_RBUTTONDOWN:
                    MouseBeforeEvent?.Invoke(this, TriggerType.MouseRightClick);
                    break;

                case NativeMethods.WM_RBUTTONUP:
                    FireAfterDelayed(TriggerType.MouseRightClick, cfg.PostClickDelayMs);
                    break;

                case NativeMethods.WM_MBUTTONDOWN:
                    MouseBeforeEvent?.Invoke(this, TriggerType.MouseMiddleClick);
                    break;

                case NativeMethods.WM_MBUTTONUP:
                    FireAfterDelayed(TriggerType.MouseMiddleClick, cfg.PostClickDelayMs);
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

    private void FireAfterDelayed(TriggerType trigger, int delayMs)
    {
        if (delayMs <= 0)
        {
            MouseEvent?.Invoke(this, trigger);
            return;
        }
        var t = trigger;
        Task.Run(async () =>
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            MouseEvent?.Invoke(this, t);
        });
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var ks = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            var vk = (System.Windows.Forms.Keys)ks.vkCode;

            bool isDown = msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN;
            bool isUp   = msg == 0x0101 /* WM_KEYUP */ || msg == 0x0105 /* WM_SYSKEYUP */;

            if (isDown || isUp)
            {
                switch (vk)
                {
                    case System.Windows.Forms.Keys.LShiftKey:
                    case System.Windows.Forms.Keys.RShiftKey:
                    case System.Windows.Forms.Keys.ShiftKey:
                        _shiftDown = isDown; break;
                    case System.Windows.Forms.Keys.LControlKey:
                    case System.Windows.Forms.Keys.RControlKey:
                    case System.Windows.Forms.Keys.ControlKey:
                        _ctrlDown = isDown; break;
                    case System.Windows.Forms.Keys.LMenu:
                    case System.Windows.Forms.Keys.RMenu:
                    case System.Windows.Forms.Keys.Menu:
                        _altDown = isDown; break;
                }
            }

            if (isDown)
            {
                Log.Debug("HookService: キー入力検知");
                if (!_inKeyboardSession)
                {
                    _inKeyboardSession = true;
                    KeyboardBeforeEvent?.Invoke(this, EventArgs.Empty);
                }
                AccumulateKey(vk);
                KeyboardActivity?.Invoke(this, EventArgs.Empty);
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private void AccumulateKey(System.Windows.Forms.Keys vk)
    {
        bool shift = _shiftDown;
        bool ctrl  = _ctrlDown;
        bool alt   = _altDown;

        // モディファイアキー自体は単独で蓄積しない
        if (vk is System.Windows.Forms.Keys.ShiftKey or System.Windows.Forms.Keys.LShiftKey
                or System.Windows.Forms.Keys.RShiftKey
                or System.Windows.Forms.Keys.ControlKey or System.Windows.Forms.Keys.LControlKey
                or System.Windows.Forms.Keys.RControlKey
                or System.Windows.Forms.Keys.Menu or System.Windows.Forms.Keys.LMenu
                or System.Windows.Forms.Keys.RMenu
                or System.Windows.Forms.Keys.LWin or System.Windows.Forms.Keys.RWin)
            return;

        string? printable = null;
        string keyName;

        if (!ctrl && !alt)
        {
            printable = VkToPrintable(vk, shift);
        }

        keyName = BuildKeyName(vk, shift, ctrl, alt);

        lock (_keyBufLock)
        {
            if (vk == System.Windows.Forms.Keys.Back)
            {
                if (_inputTextBuf.Length > 0)
                    _inputTextBuf.Remove(_inputTextBuf.Length - 1, 1);
            }
            else if (printable != null)
            {
                _inputTextBuf.Append(printable);
            }
            _keyCodeBuf.Add(keyName);
        }
    }

    private static string? VkToPrintable(System.Windows.Forms.Keys vk, bool shift)
    {
        if (vk >= System.Windows.Forms.Keys.A && vk <= System.Windows.Forms.Keys.Z)
            return (shift ? vk.ToString() : vk.ToString().ToLowerInvariant());

        if (vk == System.Windows.Forms.Keys.Space) return " ";

        var shiftMap = new System.Collections.Generic.Dictionary<System.Windows.Forms.Keys, string>
        {
            [System.Windows.Forms.Keys.D1] = shift ? "!" : "1",
            [System.Windows.Forms.Keys.D2] = shift ? "@" : "2",
            [System.Windows.Forms.Keys.D3] = shift ? "#" : "3",
            [System.Windows.Forms.Keys.D4] = shift ? "$" : "4",
            [System.Windows.Forms.Keys.D5] = shift ? "%" : "5",
            [System.Windows.Forms.Keys.D6] = shift ? "^" : "6",
            [System.Windows.Forms.Keys.D7] = shift ? "&" : "7",
            [System.Windows.Forms.Keys.D8] = shift ? "*" : "8",
            [System.Windows.Forms.Keys.D9] = shift ? "(" : "9",
            [System.Windows.Forms.Keys.D0] = shift ? ")" : "0",
            [System.Windows.Forms.Keys.OemMinus]     = shift ? "_" : "-",
            [System.Windows.Forms.Keys.Oemplus]      = shift ? "+" : "=",
            [System.Windows.Forms.Keys.OemOpenBrackets] = shift ? "{" : "[",
            [System.Windows.Forms.Keys.OemCloseBrackets] = shift ? "}" : "]",
            [System.Windows.Forms.Keys.OemBackslash] = shift ? "|" : "\\",
            [System.Windows.Forms.Keys.OemSemicolon] = shift ? ":" : ";",
            [System.Windows.Forms.Keys.OemQuotes]    = shift ? "\"" : "'",
            [System.Windows.Forms.Keys.Oemcomma]     = shift ? "<" : ",",
            [System.Windows.Forms.Keys.OemPeriod]    = shift ? ">" : ".",
            [System.Windows.Forms.Keys.OemQuestion]  = shift ? "?" : "/",
            [System.Windows.Forms.Keys.NumPad0]  = "0", [System.Windows.Forms.Keys.NumPad1] = "1",
            [System.Windows.Forms.Keys.NumPad2]  = "2", [System.Windows.Forms.Keys.NumPad3] = "3",
            [System.Windows.Forms.Keys.NumPad4]  = "4", [System.Windows.Forms.Keys.NumPad5] = "5",
            [System.Windows.Forms.Keys.NumPad6]  = "6", [System.Windows.Forms.Keys.NumPad7] = "7",
            [System.Windows.Forms.Keys.NumPad8]  = "8", [System.Windows.Forms.Keys.NumPad9] = "9",
        };
        return shiftMap.TryGetValue(vk, out var c) ? c : null;
    }

    private static string BuildKeyName(System.Windows.Forms.Keys vk, bool shift, bool ctrl, bool alt)
    {
        string vkStr = vk switch
        {
            System.Windows.Forms.Keys.Return    => "Enter",
            System.Windows.Forms.Keys.Back      => "Backspace",
            System.Windows.Forms.Keys.Delete    => "Delete",
            System.Windows.Forms.Keys.Tab       => "Tab",
            System.Windows.Forms.Keys.Escape    => "Escape",
            System.Windows.Forms.Keys.Left      => "Left",
            System.Windows.Forms.Keys.Right     => "Right",
            System.Windows.Forms.Keys.Up        => "Up",
            System.Windows.Forms.Keys.Down      => "Down",
            System.Windows.Forms.Keys.Home      => "Home",
            System.Windows.Forms.Keys.End       => "End",
            System.Windows.Forms.Keys.Prior     => "PageUp",
            System.Windows.Forms.Keys.Next      => "PageDown",
            System.Windows.Forms.Keys.Space     => "Space",
            _ when vk >= System.Windows.Forms.Keys.F1 && vk <= System.Windows.Forms.Keys.F24
                => vk.ToString(),
            _ when vk >= System.Windows.Forms.Keys.A && vk <= System.Windows.Forms.Keys.Z
                => vk.ToString(),
            _ => vk.ToString(),
        };

        var prefix = new System.Text.StringBuilder();
        if (ctrl)  prefix.Append("Ctrl+");
        if (alt)   prefix.Append("Alt+");
        if (shift) prefix.Append("Shift+");
        return prefix.ToString() + vkStr;
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
