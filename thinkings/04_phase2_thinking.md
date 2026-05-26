# 思考メモ 04: Phase 2 実装の思考過程

## 日時
2026-05-26

---

## ドラッグ&ドロップ検知の設計判断

### 判定方式の選択

| 案 | 方式 | 問題点 |
|----|------|--------|
| A | `WM_LBUTTONDBLCLK` の有無で判断 | ダブルクリックとドラッグは別物; ダブルクリックがドラッグを隠す |
| B | DOWN→UP の座標差で判断 | LLHOOK では座標差を正確に取れない場合がある |
| **C** | **DOWN→UP の時間差で判断** | シンプルで信頼性が高い。閾値 200ms は Windows 標準に近い |

→ **方式 C を採用**。`DragThresholdMs` として設定から参照可能にし、将来的に調整可能に。

### `_lbDownTime = DateTime.MinValue` の使い方

`WM_LBUTTONUP` が `WM_LBUTTONDOWN` なしに到達するエッジケース (フック設置タイミング) に対処するため、  
`_lbDownTime != DateTime.MinValue` チェックを判定条件に含める。

---

## ホイールアイドルタイマーの設計

### なぜ「最終イベントから N ms 後」方式なのか

スクロール中は `WM_MOUSEWHEEL` が連続して届く。毎回撮影すると:
- スクロール 1 秒で数十枚 PNG が生成される → ディスク・CPU 負荷が高い
- ページ読み込み確認などの「見た画面」を記録したいユーザー意図に合わない

「スクロールが止まった後 500ms 待ってから 1 枚だけ撮る」ことで、  
スクロール完了後の状態を自然にキャプチャできる。

### タイマーの正しいリセット方法

```csharp
// 毎回 Dispose → new することでリセット
_wheelIdleTimer?.Dispose();
_wheelIdleTimer = new System.Threading.Timer(_ => { ... }, null, cfg.WheelIdleMs, Timeout.Infinite);
```

`Timer.Change()` を使ってリセットも可能だが、`Dispose` 後に `Change` が例外を投げる競合がある。  
フック内では毎回 new する方が安全。

---

## 入力シミュレーターの必要性

### なぜ手動操作では検証が難しいか

`dotnet run` を実行したターミナル (Git Bash / MinGW) 内でのキーボード操作は、  
POSIX 経由で処理されるため `WH_KEYBOARD_LL` フックに到達しない可能性がある。

→ `SendInput` API を直接呼び出す C# 製シミュレーターを `tools/InputStimulator/` に作成し、  
Win32 入力イベントを直接発生させることで確実にフックを通過させた。

### シミュレーターの設計

```
InputStimulator の動作シーケンス:
1. 左クリック (DOWN + UP)
2. 1秒待機
3. ドラッグ (DOWN → 300ms待機 → UP)
4. 1秒待機
5. 右クリック (DOWN + UP)
6. 1秒待機
7. キー入力 (A キー DOWN + UP × 2)
8. 3秒待機 (KeyboardIdleSeconds=2 を超える)
9. マウスホイール (1回)
10. 1秒待機 (WheelIdleMs=500ms を超える)
```

各ステップで対応するトリガーが発火することをログで確認。

---

## 発見した技術的知見

### `System.Threading.Timer` のコールバックスレッド

`_wheelIdleTimer` のコールバックはスレッドプールスレッドで実行される。  
`MouseEvent?.Invoke(this, TriggerType.MouseWheel)` はそのままイベントリスナーに伝達される。  
`TriggerOrchestrator.OnMouseEvent` → `FireCapture` では `Task.Run` でさらに非同期化しているので問題なし。

### フックコールバックのスレッド制約

`WH_MOUSE_LL` / `WH_KEYBOARD_LL` のコールバックは、フックを設置したスレッド (UIスレッド) で呼ばれる。  
`CallNextHookEx` を適切に呼び出さないとシステム全体の入力が遅延するため、コールバック内で重い処理をしない。  
→ `Task.Run` で非同期化することでフックコールバックをすぐに返している。

### `Dispose` パターンと `HookService`

`HookService.Dispose()` は `Stop()` を呼ぶが、`_wheelIdleTimer` も `Stop()` 内で Dispose する必要がある。  
フック解除後にタイマーが発火してイベントを送るとクラッシュするため、順序が重要:

```
1. _wheelIdleTimer?.Dispose()  // タイマー停止
2. UnhookWindowsHookEx()       // フック解除
3. UnhookWinEvent()            // WinEventフック解除
```

---

## Phase 3 への引き継ぎ事項

1. **画面差分 (ScreenDiff) の閾値調整**: デフォルト 30% は通常操作ではほぼ発火しない。Phase 3 では実際のウィンドウ切替やフルスクリーン変化でテストする
2. **マルチモニタ**: 現状は `Screen.AllScreens` で全モニターを撮影しているが、差分検知は各モニターを独立して管理している
3. **JPEG/WebP**: `CaptureService.Encode` は既に実装済み。設定 UI との接続を確認する
4. **AutoStart 登録パス**: 現在 Debug パス (`bin\Debug\...`) が登録されている。Publish 後に上書き登録が必要
