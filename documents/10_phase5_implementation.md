# Phase 5 実装記録

## 日時
2026-05-26

---

## 実装概要

Phase 5 では以下を実装した。

| # | 機能 | 対象ファイル |
|---|------|-------------|
| 1 | グローバルホットキー管理 (HotkeyService) | Services/HotkeyService.cs (新規) |
| 2 | RegisterHotKey/UnregisterHotKey API | Native/NativeMethods.cs |
| 3 | HotkeyService の統合 | Services/NotifyIconWrapper.cs |
| 4 | ホットキー入力 UI | Views/SettingsWindow.xaml |
| 5 | ホットキー入力ハンドラ + ApplySettings | Views/SettingsWindow.xaml.cs |

---

## 1. HotkeyService 設計

### アーキテクチャ
`System.Windows.Forms.NativeWindow` を継承し、  
`HWND_MESSAGE` (Parent = -3) で非表示のメッセージ専用ウィンドウを作成。

WPF ディスパッチャは同一スレッド上の全 HWND へのメッセージを処理するため、  
WPF アプリでも `WndProc` が正しく呼ばれる。

```
CreateHandle(new CreateParams { Parent = new IntPtr(-3) })
  → HWND_MESSAGE ウィンドウ (画面に表示されない)
RegisterHotKey(Handle, id, MOD_CONTROL | MOD_NOREPEAT, VK_F9)
  → WM_HOTKEY メッセージ → WndProc → HotkeyPressed イベント
  → NotifyIconWrapper.OnPauseClick() → 一時停止/再開トグル
```

### ホットキー文字列形式
`"Ctrl+F9"`, `"Alt+F12"`, `"Ctrl+Shift+F10"` などの `+` 区切り形式。

### キー解析
WPF の `KeyConverter.ConvertFromString(name)` + `KeyInterop.VirtualKeyFromKey(key)` を使用。  
Ctrl, Alt, Shift, Win の修飾子を `MOD_*` フラグにマップ。

### MOD_NOREPEAT
長押しでの連続発火を防ぐため `MOD_NOREPEAT` を常に付加。

---

## 2. NotifyIconWrapper 統合

```csharp
_hotkeyService = new HotkeyService();
_hotkeyService.HotkeyPressed += (_, _) => OnPauseClick(null, EventArgs.Empty);

// Initialize() 内:
_hotkeyService.Register(_config.Config.HotkeyPause);
_config.ConfigChanged += OnHotkeyConfigChanged;

// ConfigChanged ハンドラ:
private void OnHotkeyConfigChanged(object? sender, EventArgs e)
{
    _hotkeyService.Register(_config.Config.HotkeyPause);
}
```

設定ウィンドウで OK/適用 → `ConfigChanged` → 即時再登録。

---

## 3. SettingsWindow ホットキー入力

### UI 変更
- `TxtHotkey` を読み取り専用のままにしつつ `PreviewKeyDown` で実際のキー入力を検知
- フォーカスを取得したとき「(キーを押してください)」を表示
- キー押下時: `Keyboard.Modifiers` + `e.Key` から "Ctrl+F9" 形式文字列を生成
- Escape でクリア
- 「クリア」ボタンで明示的にリセット

### キー入力ロジック
```csharp
private void TxtHotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
{
    e.Handled = true;
    var key = e.Key == Key.System ? e.SystemKey : e.Key;
    // 修飾キー単体・Escape を除外して HotkeyService.KeyToString で文字列化
    string combo = HotkeyService.KeyToString(Keyboard.Modifiers, key);
    _pendingHotkey = combo;
    TxtHotkey.Text = combo;
}
```

---

## 動作確認結果

### ホットキー登録
```
16:09:55 [INF] HotkeyService: ホットキー登録成功: Ctrl+F9
```

### 一時停止 (Ctrl+F9 1回目)
```
16:10:03 [INF] 撮影 一時停止
```

### 再開 (Ctrl+F9 2回目)
```
16:10:22 [INF] 撮影 再開
```

トグル動作が正常に機能することを確認。

---

## Phase 6 への引き継ぎ

1. **StructuredOutput テスト** - JSONL/CSV 出力の動作確認 (実装済み、未テスト)
2. **SettingsWindow の動作検証** - 実際に設定ウィンドウを開いてホットキー入力を確認
3. **ExcludeApps のテスト** - 除外アプリ設定の効果確認
4. **ImageOverlay** - MetadataConfig.ImageOverlay=false のまま (要件確認)
5. **Notifier.FlashIcon の Timer リーク** - FlashIcon で作成した Timer が Dispose されない問題あり
