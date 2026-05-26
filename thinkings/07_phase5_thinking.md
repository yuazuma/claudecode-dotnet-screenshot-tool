# 思考メモ 07: Phase 5 実装の思考過程

## 日時
2026-05-26

---

## 設計判断

### NativeWindow vs HwndSource

グローバルホットキー受信には Win32 HWND が必要。

**方式A (採用): `NativeWindow` (WinForms)**
- `CreateHandle(new CreateParams { Parent = new IntPtr(-3) })` で HWND_MESSAGE ウィンドウ作成
- WPF + WinForms 混在アプリで既に `NotifyIcon` が WinForms を使用しているため自然
- シンプル、軽量

**方式B (不採用): `HwndSource` (WPF)**
- `new HwndSource(new HwndSourceParameters(...))` でメッセージウィンドウ作成
- WPF の機能を使うが、WinForms の `NativeWindow` より冗長

**方式C (不採用): WPF Window を非表示で使用**
- `MainWindow` を `Width=0, Height=0, ShowInTaskbar=false` で show/hide
- WPF ウィンドウのオーバーヘッドが不要

### HWND_MESSAGE ウィンドウの動作確認

WPF のディスパッチャループは `GetMessage` / `DispatchMessage` を呼び出し、  
同一スレッド上の全 HWND へのメッセージを配送する。  
`NativeWindow` は Win32 レベルで WNDPROC を登録するため、  
WPF ディスパッチャが `DispatchMessage` を呼ぶと `WndProc(ref Message m)` が呼ばれる。

### MOD_NOREPEAT について

`RegisterHotKey` に `MOD_NOREPEAT (0x4000)` を指定すると、  
キーを押し続けても WM_HOTKEY が 1 回しか発火しない。  
一時停止トグルに使うので連続発火は不要 → 必ず付加する。

### SendKeys vs 実際のキー入力

動作確認に `[System.Windows.Forms.SendKeys]::SendWait('^{F9}')` を使用した。  
`SendKeys` は `SendInput` を経由してキーボードイベントをキューに入れるため、  
グローバルホットキーとして登録された Ctrl+F9 を発火できた。

### ホットキー文字列形式の設計

`"Ctrl+F9"` 形式を選んだ理由:
- 直感的で人間が読みやすい
- WPF `KeyConverter` でパース可能 (`converter.ConvertFromString("F9")` → `Key.F9`)
- `KeyInterop.VirtualKeyFromKey(Key.F9)` → VK コード (0x78) に変換可能
- UI 表示と config ファイル保存の両方に使える

### SettingsWindow の LostFocus ハンドラ

`TxtHotkey_GotFocus` で "(キーを押してください)" を表示し、  
`TxtHotkey_LostFocus` で `_pendingHotkey ?? "(未設定)"` に戻す。  
これにより「フォーカスを得たが何もキーを押さなかった」場合に  
表示が "(キーを押してください)" のままにならない。

---

## 発見したバグ: Notifier.FlashIcon の Timer リーク

```csharp
private void FlashIcon()
{
    if (_notifyIcon == null || _flashIcon == null) return;
    _notifyIcon.Icon = _flashIcon;
    var timer = new System.Threading.Timer(_ => {
        _notifyIcon.Icon = _normalIcon;
    }, null, 200, System.Threading.Timeout.Infinite);
    // timer が Dispose されない!
}
```

撮影ごとに `System.Threading.Timer` が作成されるが、  
コールバック実行後も GC されるまで生き続ける。  
高頻度撮影時はタイマーオブジェクトが積み上がる可能性がある。

**修正案** (Phase 6 で対応):  
```csharp
private System.Threading.Timer? _flashTimer;

private void FlashIcon()
{
    if (_notifyIcon == null || _flashIcon == null) return;
    _notifyIcon.Icon = _flashIcon;
    _flashTimer?.Dispose();
    _flashTimer = new System.Threading.Timer(_ => {
        _notifyIcon.Icon = _normalIcon;
    }, null, 200, System.Threading.Timeout.Infinite);
}
```

---

## Phase 6 への引き継ぎ

1. **Notifier.FlashIcon Timer リーク修正**
2. **StructuredOutput (JSONL/CSV) の動作テスト**
3. **設定ウィンドウの UI テスト** - ホットキー入力欄が実際に機能するか
4. **除外アプリ (ExcludeApps) のテスト**
5. **Notifier.ShowCounter の動作確認** - トレイアイコンのツールチップに枚数表示
