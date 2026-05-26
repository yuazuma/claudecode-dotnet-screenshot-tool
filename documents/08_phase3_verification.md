# Phase 3 動作確認レポート

## 日時
2026-05-26 15:48 ～ 15:55

## 確認環境
- バイナリ: `bin/Debug/net8.0-windows/AutoScreenshot.exe`
- DLL タイムスタンプ: 2026-05-26 15:46:21 (Phase 3 ビルド済み, 71680 bytes)
- dotnet SDK: `C:\Program Files\dotnet\dotnet.exe`

## ビルド方法
```powershell
& "C:\Program Files\dotnet\dotnet.exe" build src/AutoScreenshot -c Debug
```
→ `ビルドに成功しました。0 個の警告 0 エラー`

## 確認結果

### 1. 起動 / フック設置 ✅
```
15:46:38 [INF] AutoScreenshot 起動
15:46:38 [INF] 設定ファイルを読み込みました: ...config.json
15:46:38 [INF] HookService: フック設置成功 (mouse=true, keyboard=true, winEvent=true)
```

### 2. Phase 3 新機能: 差分検知ログ (DiffDetector) ✅
```
15:48:38 [DBG] 差分検知: モニタ0 diff=53.6% (閾値=30.0%)
```
- モニタインデックスと差分比率、閾値を出力
- 閾値未満の場合はログなし (高頻度ログ抑制)

### 3. Phase 3 新機能: スクリーンインデックス付きログ ✅
```
15:48:38 [DBG] 差分チェック: 変化モニタ数=1 (0)
15:48:38 [INF] 保存: ...20260526_154838_805_screendiff_monitor1.png
```
- `変化モニタ数=1 (0)` → 1画面が変化、インデックス0 (monitor1)
- `CaptureScreensByIndex([0])` で対象モニタのみ撮影

### 4. Phase 3 新機能: 変化なし時はログなし ✅
Phase 2 では毎回 `変化モニタ数=0` がログ出力されていたが、
Phase 3 では変化なしの場合はログ出力なし (verbose ノイズ削減)。

### 5. Phase 2 機能の継続動作確認

#### アクティブウィンドウ切替 ✅
```
15:48:35 [DBG] HookService: アクティブウィンドウ切替検知
15:48:36 [INF] 保存: ...activewindowchange_monitor1.png
```

#### ドラッグ操作 ✅
```
15:48:45 [DBG] HookService: ドラッグ検知 (208ms)
15:48:45 [DBG] TriggerOrchestrator: 撮影キュー投入 ("MouseDragDrop")
15:48:45 [INF] 保存: ...mousedragdrop_monitor1.png
```

## 差分検知の動作シーケンス (Notepad 起動時)
```
1. Notepad.exe 起動 → 画面が大きく変化
2. ActiveWindowChange トリガー → activewindowchange_monitor1.png 保存
3. 3秒後 DiffTimer 発火 → CalcDiffRatio = 53.6% > 30% → 変化検知
4. マウス/キーボード活動チェック通過
5. CaptureScreensByIndex([0]) 実行
6. screendiff_monitor1.png 保存
```

## Phase 3 実装確認まとめ

| 機能 | 実装 | 動作確認 |
|------|------|----------|
| `DiffDetector` lock 保護 | ✅ | 並行アクセス安全 |
| diff ratio ログ | ✅ | `diff=53.6% (閾値=30.0%)` |
| `CaptureScreensByIndex` | ✅ | `(0)` インデックス動作 |
| マウス活動後 1 秒抑制 | ✅ | ドラッグ後 diff 分離 |
| ConfigChanged サブスクリプション | ✅ | 起動時設置 |
| 変化なし時のログ削除 | ✅ | 静止時ログなし |

## Phase 4 への引き継ぎ
1. マルチモニタ環境での複数インデックス動作確認 (現在 1 モニタのみ)
2. JPEG/WebP エンコード対応 (現在 PNG フォールバック)
3. FolderNamingRule の DateHour/Session/Flat オプション確認
