# v1.6.0 操作前後スクリーンショット実装

## 要件

`requirements/追加要件_前後イベント.md` 参照

## 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `Native/NativeMethods.cs` | 変更 | `WM_RBUTTONUP`, `WM_MBUTTONUP` 定数追加 |
| `Models/ManualSession.cs` | 変更 | `ImagePath` → `AfterImagePath`、`BeforeImagePath` 追加 |
| `Models/ProjectInfo.cs` | 変更 | `ImagePath`→`AfterImagePath`、`ThumbPath`→`AfterThumbPath`、`BeforeImagePath`/`BeforeThumbPath` 追加、旧フィールドの後方互換シム追加 |
| `Models/AppConfig.cs` | 変更 | `TriggerConfig` に `CaptureBeforeImage`/`PostClickDelayMs` 追加 |
| `Services/HookService.cs` | 変更 | `MouseBeforeEvent`/`KeyboardBeforeEvent` 追加。右・中クリックを UP に変更。`FireAfterDelayed` メソッド追加。キーボードセッションフラグ管理 |
| `Services/TriggerOrchestrator.cs` | 変更 | `FireBeforeCapture`/`TakeBeforeShot` 追加。`_pendingBeforeShots` 管理。`FireCapture` で before パスを相関 |
| `Services/FileStorage.cs` | 変更 | `SaveBeforeAsync` 追加（`images/before/` に PNG で保存） |
| `Services/ManualSessionRecorder.cs` | 変更 | `RecordStepAsync`/`RecordProjectStepAsync` に `beforeImagePath` 引数追加。before サムネイル生成 |
| `Services/ProjectStore.cs` | 変更 | `CloneStep`/`MergeProjectsAsync`/`SplitProjectAsync` で before フィールドコピー対応 |
| `Services/ExportService.cs` | 変更 | `BuildAnnotatedSession` で before/after 分離。`ExportImagesAsync` で before を `before/` サブフォルダにコピー |
| `Services/HtmlManualWriter.cs` | 変更 | `BuildImagesSection` で before/after 横並び表示。CSS 追加 |
| `Services/MarkdownManualWriter.cs` | 変更 | before → after の順で 2 枚出力 |
| `Services/DocxManualWriter.cs` | 変更 | before → after の順で 2 枚出力 |
| `Services/FrameRenderer.cs` | 変更 | `step.ImagePath` → `step.AfterImagePath` |
| `Services/VideoGenerator.cs` | 変更 | `s.ImagePath` → `s.AfterImagePath` |
| `Views/ProjectViewWindow.xaml` | 変更 | before 画像表示エリア（`PnlBeforeImage`/`ImgBefore`）追加。Grid.Row 番号更新 |
| `Views/ProjectViewWindow.xaml.cs` | 変更 | `LoadAnnotationImage` で before 画像ロード。`StepViewModel.ThumbImageSource` で `AfterThumbPath` 使用。削除時に before 画像移動 |
| `Views/SettingsWindow.xaml` | 変更 | 撮影トリガータブに「操作前後スクリーンショット」グループ追加 |
| `Views/SettingsWindow.xaml.cs` | 変更 | `LoadSettings`/`ApplySettings` に新設定追加 |

## 設計のポイント

### HookService の変更
- 右・中クリックは DOWN で before、UP + 遅延で after を発火（元は DOWN で after 発火）
- 左クリック: DOWN で before、UP + 遅延で after（遅延追加）
- キーボード: `_inKeyboardSession` フラグで新しいシーケンス開始を検知
- `TakeAccumulatedKeys()` でフラグリセット → 次のキーで新しい before が発火

### before/after の相関
- `TriggerOrchestrator._pendingBeforeShots[TriggerType]` に before パスを一時保持
- `MouseDragDrop` は `MouseLeftClick` と同一 key に正規化（同じ before 画像を共有）
- `FireCapture` 開始時に `TakeBeforeShot` で before を取り出し、after 保存後 `RecordStepAsync` に渡す
- 5 秒以内のエントリのみ使用（期限切れは破棄）

### 後方互換性
- `ProjectStep` に `LegacyImagePath`/`LegacyThumbPath` compat シムを追加
- `get => null` + `WhenWritingNull` で書き込み時はスキップ
- `init => AfterImagePath ??= value` で旧 project.json の `imagePath` を `AfterImagePath` に移行

## ビルド結果

```
dotnet build -c Release --no-incremental
→ 0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```
