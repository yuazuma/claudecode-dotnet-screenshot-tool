# v1.4.0 アイコン状態管理の実装

## 概要

タスクトレイアイコンをアプリの処理ステータスに応じて5色で切り替える機能を実装した。

## 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/AutoScreenshot/Models/IconState.cs` | 新規: 5状態 enum |
| `src/AutoScreenshot/Resources/IconFactory.cs` | 2色→5色対応、Processing に右下ドット追加 |
| `src/AutoScreenshot/Services/Notifier.cs` | アイコン状態機械に全面再設計 |
| `src/AutoScreenshot/Services/ExportService.cs` | 各エクスポートに BeginProcessing/EndProcessing/ShowError を追加 |
| `src/AutoScreenshot/Services/NotifyIconWrapper.cs` | 5アイコン生成・渡し・Dispose 更新、SetBaseState 呼び出しに変更 |

## アイコン状態一覧

| 状態 | 色 | RGB | トリガー |
|---|---|---|---|
| Recording | 青 | `#0078D7` | 起動・一時停止解除 |
| Paused | グレー | `#808080` | ユーザー一時停止・ディスク不足自動停止 |
| Captured | 緑 | `#107C10` | 撮影成功直後200ms点滅→基本状態に戻る |
| Processing | オレンジ+白ドット | `#CA5010` | エクスポート処理中（複数並走はカウンタ管理） |
| Error | 赤 | `#C50F1F` | エクスポートエラー→5秒後に基本状態に戻る |

## 状態優先度（ApplyDisplayState）

```
Error > Captured(一時的) > Processing > Paused / Recording
```

- `_errorActive` が true → Error
- `_capturedActive` が true → Captured
- `_processingCount > 0` → Processing
- それ以外 → `_isPaused` ? Paused : Recording

## 複数エクスポート並走の処理

`_processingCount`（int）を `Interlocked.Increment/Decrement` で管理。
複数エクスポートが同時実行中でも、すべて完了するまで Processing 状態を維持する。

## 設計変更点

- `SetPausedState(bool, NotifyIcon)` を廃止し `SetBaseState(bool)` に統一
- `NotifyIconWrapper` はアイコンの直接操作を行わず、すべて `_notifier.SetBaseState()` に委譲
- `FlashIcon()` を `FlashCaptured()` にリネームし、グレーではなく緑（Captured）アイコンを使用
