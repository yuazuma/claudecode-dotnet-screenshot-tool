# v1.3.0 実装・リリース記録

## 実施内容

### FR-C: ステップアノテーション

**新規ファイル**:
- `src/AutoScreenshot/Models/AnnotationItem.cs` — Type / X / Y / X2 / Y2 / Label / Color
- `src/AutoScreenshot/Services/AnnotationRenderer.cs` — System.Drawing で番号/矢印/矩形/吹き出しを焼き込み

**変更ファイル**:
- `Models/ProjectInfo.cs`: `ProjectStep.Annotations` フィールド追加
- `Views/ProjectViewWindow.xaml`: アノテーションパネル（ツール・色・Canvas・保存ボタン）
- `Views/ProjectViewWindow.xaml.cs`: Canvas 描画・座標変換・保存ロジック全追加
- `Services/ExportService.cs`: `BuildAnnotatedSession()` で焼き込み済み一時 PNG を生成

### FR-D: プロジェクト管理強化

**変更ファイル**:
- `Models/ProjectInfo.cs`: Tags, CreatedAtDisplay 追加
- `Views/ProjectViewWindow.xaml`: 検索ボックス・タグWrapPanel・結合ボタン・分割ボタン・ステップ追加ボタン
- `Views/ProjectViewWindow.xaml.cs`: FilterAndDisplayProjects / RefreshTagPanel / ドラッグ&ドロップ / BtnAddStep_Click

### FR-E: プロジェクト結合・分割

**変更ファイル**:
- `Services/ProjectStore.cs`: MergeProjectsAsync / SplitProjectAsync / CopyImageFile / CloneStep 追加
- `Views/ProjectViewWindow.xaml.cs`: BtnMergeProjects_Click / BtnSplitHere_Click

### FR-A: HTML エクスポート

**新規ファイル**:
- `src/AutoScreenshot/Services/HtmlManualWriter.cs` — 単一 HTML 生成（Base64 画像埋め込み）

**変更ファイル**:
- `Services/NotifyIconWrapper.cs`: HTML エクスポートメニュー追加
- `Views/SettingsWindow.xaml/.cs`: AutoExportHtml チェックボックス
- `Models/ProjectConfig.cs`: AutoExportHtml プロパティ追加

### FR-B: インクリメンタル LLM 処理

**変更ファイル**:
- `Services/ManualSessionRecorder.cs`: ステップ追記後の非同期 LLM キュー処理
- `Services/LlmService.cs`: ImproveStepDescriptionAsync 追加
- `Views/SettingsWindow.xaml/.cs`: IncrementalLlm チェックボックス
- `Models/ProjectConfig.cs`: IncrementalLlm プロパティ追加

### バージョン更新

- `AutoScreenshot.csproj`: 1.2.0 → 1.3.0

---

## ビルド結果

```
ビルドに成功しました。
2 個の警告（SixLabors.ImageSharp NU1902）
0 エラー
```

## リリースアーティファクト

| ファイル | 内容 |
|---|---|
| `releases/AutoScreenshot-v1.3.0-win-x64.zip` | 自己完結型 win-x64 パッケージ |
| `releases/AutoScreenshot-v1.3.0-win-x64.zip.sha256` | SHA-256 チェックサム |
| `releases/RELEASE_NOTES_v1.3.0_JA.md` | 日本語リリースノート |
| `releases/RELEASE_NOTES_v1.3.0_EN.md` | 英語リリースノート |
| `releases/CHANGELOG.md` | v1.3.0 エントリ追記済み |

ZIP SHA-256: `36096a3427c04f14db3941ea9e258fbd714e59064ae0e40c16a3d28d47db8901`

---

## WPF/WinForms 型競合の解決方法

UseWPF + UseWindowsForms 同時有効のプロジェクトでは広い using ディレクティブを避け、
個別型エイリアスを ProjectViewWindow.xaml.cs に集中定義:

```csharp
using WinPoint = System.Windows.Point;
using WinDragEventArgs = System.Windows.DragEventArgs;
// ... 20+ aliases
```

DragDropEffects のみ完全修飾: `System.Windows.DragDropEffects.Move`
