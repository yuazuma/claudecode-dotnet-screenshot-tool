# Phase 3 実装・動作確認

## プロンプト

次フェーズ (Phase 3) を実施してください。

---

## 実施内容

### Phase 3 の目標

Phase 3 では画面差分検知 (F-03) を以下の観点で強化した:

1. **`DiffDetector` スレッドセーフ化** — `lock (_lock)` で `_prevThumbs` アクセスを保護し、タイマースレッドと Dispose の競合を防ぐ
2. **差分率ログ** — 変化検知時に実際の差分率と閾値をデバッグログに出力
3. **`CaptureService.CaptureScreensByIndex`** — 変化したモニタのみを撮影する新メソッドを追加
4. **マウス活動後の差分抑制** — `_lastMouseActivity` を追跡し、マウスクリック後 1 秒以内の差分トリガーを除外
5. **設定ホットリロード** — `ConfigStore.ConfigChanged` イベントを購読し、`ScreenDiffIntervalSeconds` 変更時にタイマーを自動更新

---

### 変更ファイル詳細

#### `Services/DiffDetector.cs`

```csharp
// 追加: スレッドセーフ用ロック
private readonly object _lock = new();

// lock を使って _prevThumbs アクセスを保護
lock (_lock)
{
    if (_prevThumbs.TryGetValue(i, out var prev))
    {
        double diff = CalcDiffRatio(prev, thumb);
        if (diff >= thresholdPercent / 100.0)
        {
            Log.Debug("差分検知: モニタ{Idx} diff={Ratio:P1} (閾値={Threshold:P1})",
                i, diff, thresholdPercent / 100.0);
            changed.Add(i);
            prev.Dispose();
            _prevThumbs[i] = thumb;
        }
        else { thumb.Dispose(); }
    }
    else { _prevThumbs[i] = thumb; }
}

// Dispose にも lock を追加
public void Dispose()
{
    lock (_lock)
    {
        foreach (var bmp in _prevThumbs.Values) bmp.Dispose();
        _prevThumbs.Clear();
    }
}
```

#### `Services/CaptureService.cs`

```csharp
/// <summary>指定した 0 始まりインデックスのモニタのみ撮影する</summary>
public List<(Bitmap Image, int MonitorIndex, Rectangle Bounds)> CaptureScreensByIndex(IReadOnlyList<int> zeroBasedIndices)
{
    var results = new List<(Bitmap, int, Rectangle)>();
    var screens = Screen.AllScreens;
    foreach (int i in zeroBasedIndices)
    {
        if (i < 0 || i >= screens.Length) continue;
        var screen = screens[i];
        results.Add((CaptureScreen(screen.Bounds), i + 1, screen.Bounds));
    }
    return results;
}
```

#### `Services/TriggerOrchestrator.cs`

主要変更点:

| 変更箇所 | 内容 |
|---------|------|
| フィールド追加 | `_diffIntervalMs`, `_lastMouseActivity` |
| コンストラクタ | `ConfigChanged += OnConfigChanged` を追加 |
| `OnConfigChanged` | 新メソッド: 設定変更時に diff タイマーを更新 |
| `OnMouseEvent` | `_lastMouseActivity = DateTime.UtcNow;` を追加 |
| `OnDiffTimer` | マウス活動抑制チェック追加、インデックスログを改善 |
| `FireCapture` | `IReadOnlyList<int>? screenIndices = null` 引数追加 |
| `Dispose` | `_config.ConfigChanged -= OnConfigChanged;` を追加 |

```csharp
// OnDiffTimer の改善版
private void OnDiffTimer(object? state)
{
    var changedScreens = _diffDetector.DetectChangedScreens(threshold);
    if (changedScreens.Count == 0) return;                        // 無変化時はログなし

    Log.Debug("差分チェック: 変化モニタ数={Count} ({Indices})",
        changedScreens.Count, string.Join(",", changedScreens));  // インデックスも出力

    // マウス・キーボード直後 (1秒以内) の差分はトリガーイベント起因とみなして除外
    if ((DateTime.UtcNow - _lastKeyboardActivity).TotalSeconds < 1.0) return;
    if ((DateTime.UtcNow - _lastMouseActivity).TotalSeconds < 1.0) return;

    FireCapture(TriggerType.ScreenDiff, changedScreens);          // 変化モニタのみ撮影
}
```

---

### 動作確認結果

#### コードレビュー検証

| 検証項目 | 結果 |
|---------|------|
| `DiffDetector` スレッドセーフ化 | ✅ `_lock` で全アクセスを保護 |
| 差分率ログ出力 | ✅ `Log.Debug("差分検知: モニタ{Idx} diff={Ratio:P1}...")` |
| `CaptureScreensByIndex` | ✅ 正しく実装済み |
| マウス活動後の差分抑制 | ✅ `_lastMouseActivity` の更新・参照ロジック正確 |
| 設定ホットリロード | ✅ `ConfigChanged` 購読・解除・タイマー更新のコードが正確 |
| ビルド (exit 0) | ✅ `dotnet build -c Debug` が成功 |

#### 実行ログ検証 (Phase 2 バイナリ)

Phase 3 コードの新バイナリ動作確認は Bash ツール環境の制約により完全実施できなかったが、
差分検知の基本動作は既存バイナリのログで確認済み:

```
2026-05-26 15:08:30 [DBG] 差分チェック: 変化モニタ数=1
2026-05-26 15:08:30 [INF] 保存完了: 20260526_150830_150_screendiff_monitor1.png
```

新バイナリ動作確認の方法:
```bash
# Windows ターミナルで実行
dotnet run --project src/AutoScreenshot

# ログ確認ポイント (Phase 3 新形式)
# [DBG] 差分検知: モニタ0 diff=35.2% (閾値=30.0%)
# [DBG] 差分チェック: 変化モニタ数=1 (0)
```

---

### Phase 4 への引き継ぎ事項

1. **マルチモニタ**: `CaptureScreensByIndex` を活用した per-monitor 保存を統合テスト
2. **JPEG/WebP**: `CaptureService.Encode` の JPEG/WebP パスの動作確認
3. **命名規則**: `FolderNamingRule` の各オプションが保存パスに反映されることを確認
4. **AutoStart 登録パス**: Debug パスが登録されている。配布ビルド後に更新が必要
