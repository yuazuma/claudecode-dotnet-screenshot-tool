# Phase 2 実装・動作確認

## プロンプト

次フェーズ (Phase 2) を実施してください。

---

## 実施内容

### Phase 2 の目標

Phase 2 では以下の機能を実装・検証した:

1. **ドラッグ&ドロップ検知** — `WM_LBUTTONDOWN` 押下時刻を記録し、`WM_LBUTTONUP` で経過時間が `DragThresholdMs` (デフォルト 200ms) 以上であれば `MouseDragDrop` トリガーとして判定
2. **ホイールアイドルタイマー** — `WM_MOUSEWHEEL` ごとにタイマーをリセットし、最終イベントから `WheelIdleMs` (デフォルト 500ms) 後に `MouseWheel` トリガーを発火
3. **実機動作確認** — 5 種類のトリガー (左クリック・ドラッグ・右クリック・キーボード・ホイール) がすべて PNG 保存まで到達することをログで確認

---

### 変更ファイル

#### `Models/AppConfig.cs` — TriggerConfig に追加

```csharp
// クールダウン (秒)
public double CooldownMouseDragDrop { get; set; } = 0.5;
public double CooldownMouseWheel   { get; set; } = 2.0;

// ドラッグ判定閾値 (ミリ秒)
public int DragThresholdMs { get; set; } = 200;

// ホイールアイドル待機 (ミリ秒)
public int WheelIdleMs { get; set; } = 500;
```

#### `Services/HookService.cs` — Phase 2 主要変更

- コンストラクタに `Func<TriggerConfig> triggerConfig` を追加 (設定を動的参照)
- `_lbDownTime` フィールドでドラッグ開始時刻を管理
- `_wheelIdleTimer` フィールドでホイールアイドルタイマーを管理
- `WM_LBUTTONDOWN`: `_lbDownTime = DateTime.UtcNow` を記録するのみ
- `WM_LBUTTONUP`: 経過時間 >= `DragThresholdMs` → `MouseDragDrop`、それ以外 → `MouseLeftClick`
- `WM_MOUSEWHEEL`: タイマーをリセットして一定時間後に `MouseWheel` を発火

```csharp
case NativeMethods.WM_LBUTTONDOWN:
    _lbDownTime = DateTime.UtcNow;
    break;

case NativeMethods.WM_LBUTTONUP:
    double elapsedMs = (DateTime.UtcNow - _lbDownTime).TotalMilliseconds;
    if (_lbDownTime != DateTime.MinValue && elapsedMs >= cfg.DragThresholdMs)
    {
        Log.Debug("HookService: ドラッグ検知 ({Ms:F0}ms)", elapsedMs);
        MouseEvent?.Invoke(this, TriggerType.MouseDragDrop);
    }
    else
    {
        MouseEvent?.Invoke(this, TriggerType.MouseLeftClick);
    }
    _lbDownTime = DateTime.MinValue;
    break;

case NativeMethods.WM_MOUSEWHEEL:
    _wheelIdleTimer?.Dispose();
    _wheelIdleTimer = new System.Threading.Timer(_ =>
    {
        Log.Debug("HookService: ホイールアイドル完了 → 撮影");
        MouseEvent?.Invoke(this, TriggerType.MouseWheel);
    }, null, cfg.WheelIdleMs, System.Threading.Timeout.Infinite);
    break;
```

#### `Services/TriggerOrchestrator.cs` — OnMouseEvent の cooldown 分岐

```csharp
(bool enabled, double cooldown) = triggerType switch
{
    TriggerType.MouseLeftClick   => (cfg.MouseLeftClick,   cfg.CooldownMouseClick),
    TriggerType.MouseRightClick  => (cfg.MouseRightClick,  cfg.CooldownMouseClick),
    TriggerType.MouseMiddleClick => (cfg.MouseMiddleClick, cfg.CooldownMouseClick),
    TriggerType.MouseDragDrop    => (cfg.MouseDragDrop,    cfg.CooldownMouseDragDrop),
    TriggerType.MouseWheel       => (cfg.MouseWheel,       cfg.CooldownMouseWheel),
    _ => (false, 0),
};
```

---

### 動作確認結果

**ツール**: SendInput ベースの入力シミュレーター (`tools/InputStimulator/`) を作成し、  
実際の Win32 グローバルフックが発火することを確認。

**ログ証跡** (`%APPDATA%\AutoScreenshot\logs\app-20260526.log`):

```
[INF] HookService: フック開始 (mouse=True, keyboard=True, winEvent=True)

[DBG] TriggerOrchestrator: 撮影キュー投入 (MouseLeftClick)
[INF] 保存完了: 20260526_144145_408_mouseleftclick_monitor1.png

[DBG] HookService: ドラッグ検知 (351ms)
[DBG] TriggerOrchestrator: 撮影キュー投入 (MouseDragDrop)
[INF] 保存完了: 20260526_144147_274_mousedragdrop_monitor1.png

[DBG] TriggerOrchestrator: 撮影キュー投入 (MouseRightClick)
[INF] 保存完了: 20260526_144148_772_mouserightclick_monitor1.png

[DBG] HookService: キー入力検知  (×2)
[INF] 保存完了: 20260526_144152_586_keyboard_monitor1.png

[DBG] HookService: ホイールアイドル完了 → 撮影
[DBG] TriggerOrchestrator: 撮影キュー投入 (MouseWheel)
[INF] 保存完了: 20260526_144154_834_mousewheel_monitor1.png
```

**保存先**: `C:\Users\y\Pictures\AutoScreenshot\2026-05-26\` に 5 枚の PNG を確認

---

### Verification 結果

| # | 確認内容 | 結果 |
|---|---------|------|
| 1 | フック設置成功 (mouse=True, keyboard=True, winEvent=True) | ✅ |
| 2 | マウスクリック → MouseLeftClick → PNG 保存 | ✅ |
| 3 | ドラッグ操作 → ドラッグ検知 → MouseDragDrop → PNG 保存 | ✅ |
| 4 | キーボード入力後アイドル → Keyboard → PNG 保存 | ✅ |
| 5 | ホイール後アイドル → MouseWheel → PNG 保存 | ✅ |

**Verdict: PASS**

---

## 次フェーズ (Phase 3) 予定

- 画面差分検知 (F-03 方式B) の閾値調整・統合テスト
- マルチモニタ個別保存の確認
- JPEG/WebP 対応
- 命名規則切替 (FolderNamingRule)
