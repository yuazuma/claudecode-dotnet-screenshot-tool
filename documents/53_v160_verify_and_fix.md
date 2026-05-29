# v1.6.0 動作確認と不具合修正

## ユーザープロンプト

```
リリースビルドしたexeを実際に起動して、要件定義通りの挙動か動作確認してください。
→（確認後）不具合を修正してください。
```

---

## 動作確認結果（PASS）

### 確認環境

- exe: `src\AutoScreenshot\bin\Release\net8.0-windows10.0.17763.0\AutoScreenshot.exe`
- PID: 1364（クリーンビルド後起動）

### 確認項目と結果

| 確認項目 | 結果 |
|---|---|
| ビルド（0 エラー） | ✅ |
| 起動・フック開始 | ✅ |
| config.json に `CaptureBeforeImage: true` / `PostClickDelayMs: 250` | ✅ |
| `images/before/` ディレクトリが生成される | ✅ 96 ファイル |
| project.json が `afterImagePath` / `beforeImagePath` / `afterThumbPath` / `beforeThumbPath` を使用 | ✅ |
| 旧フィールド `imagePath` が project.json に存在しない | ✅ 0 件 |
| `LegacyImagePath` 後方互換シムが動作（旧 JSON を読み込める） | ✅ シミュレーション検証 |
| before/after 画像ペアの内容が正しい（ドラッグ前後の画面差分） | ✅ 画像比較で確認 |
| サムネイル生成（`thumbs/before/step_NNN_before.jpg`） | ✅ |

### トリガー別 before カバレッジ

| TriggerType | total | with_before | coverage |
|---|---|---|---|
| MouseLeftClick | 48 | 47 | **98%** |
| MouseDragDrop | 24 | 24 | **100%** |
| MouseRightClick | 3 | 3 | **100%** |
| Keyboard | 11 | 10 | **91%** |
| ActiveWindowChange | 19 | 0 | 0%（要件通り非対象）|
| MouseWheel | 20 | 0 | 0%（要件通り非対象）|

---

## 発見された不具合

### キーボード before 画像の期限切れ

**症状**: 連続入力が 5 秒超になると `before 画像期限切れ: "Keyboard"` がログに出力され、
before 画像が after と紐付けられずに破棄される。

**根本原因**:

```csharp
// 修正前
if ((DateTime.UtcNow - entry.capturedAt).TotalSeconds <= 5.0)  // 一律 5 秒
```

- キーボード before: 「最初のキー時」に取得 → `capturedAt = T₀`
- キーボード after:「最後のキー + `KeyboardIdleSeconds`（既定 2s）後」に取得
- 入力継続時間が 3 秒以上になると `T_after - T₀ > 5.0` → 期限切れ

**修正**:

```csharp
// 修正後
double maxAgeSec = trigger == TriggerType.Keyboard
    ? _config.Config.Triggers.KeyboardIdleSeconds + 10.0  // 動的: アイドル時間 + バッファ
    : 5.0;                                                  // マウス: 既存の 5 秒
if ((DateTime.UtcNow - entry.capturedAt).TotalSeconds <= maxAgeSec)
    return entry.path;
```

既定値: `2 + 10 = 12 秒`。`KeyboardIdleSeconds` が最大 10 秒に設定されていても `10 + 10 = 20 秒` でカバーする。
マウスイベントは before/after が数秒以内に対応するため 5 秒のまま維持。

**修正ファイル**: `Services/TriggerOrchestrator.cs`（`TakeBeforeShot` メソッド）

---

## ビルド確認

```
dotnet build -c Release --no-incremental → 0 エラー / 3 警告（SixLabors.ImageSharp、既存）
```
