# v1.4.0 プロジェクトビュー UI リファクタリング

## 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `src/AutoScreenshot/Views/ProjectViewWindow.xaml` | レイアウト全面再設計 |
| `src/AutoScreenshot/Views/ProjectViewWindow.xaml.cs` | ハンドラー追加・バグ修正 |

## レイアウト変更概要

### ウィンドウサイズ

- 旧: 900×600（MinHeight 480、MinWidth 700）
- 新: 1040×660（MinHeight 520、MinWidth 820）

### 右ペイン構成（旧→新）

**旧:**
```
Grid (2列: 260 | *)
├── 左: プロジェクト一覧
└── 右: TabControl (ステップ一覧タブ / ステップ詳細タブ / エクスポートタブ)
```

**新:**
```
Grid (3列: 260 | 5 | *)
├── 左: プロジェクト一覧（変更なし）
└── 右: Grid (3行: Auto | * | Auto)
    ├── Row0: タイトル + ▼エクスポートボタン
    ├── Row1: ステップ一覧(縦型) | GridSplitter | ステップ詳細
    │   └── ステップ詳細: 画像(260px固定) / アノテーションExpander / ステップ情報GroupBox
    └── Row2: ステータスバー (TxtStatus + PbStatus)
```

## 主要な設計変更

### ステップ一覧（縦型リスト）

- 旧: WrapPanel による横並びサムネイル（64×48px グリッド）
- 新: ListBox の縦リスト、各行に ☰ ドラッグハンドル + 64×48 サムネイル + StepLabel/EffectiveDescription

メリット: 説明文が常時表示され、ステップ識別が容易。

### エクスポートセクション

- 旧: TabControl の「エクスポート」タブに並べたボタン群
- 新: タイトル行右端の「▼ エクスポート」ドロップダウンボタン（ContextMenu）

ContextMenu 構成:
```
手順書を出力 >
    Markdown (.md)
    HTML (.html)
    Word (.docx)
動画を生成
画像を書き出す
──────────
ZIP で保存...
```

ボタン左クリックで ContextMenu を `PlacementMode.Bottom` で開く実装（右クリックではなく左クリックトリガー）。

### アノテーションツール

- 旧: 常時展開された専用エリア
- 新: `Expander`（既存アノテーションがある場合は自動展開、それ以外は折りたたみ）

ツール選択と色選択を1行に統合（旧は2行）。

### ナビゲーション・追加ボタン

- 旧: ステップ一覧上部に配置
- 新: ステップ一覧下部固定（DockPanel の Dock="Bottom"）に配置

### ステータスバー

- 旧: なし（SetStatus は TxtStatus.Text の更新のみ）
- 新: TxtStatus + PbStatus（IsIndeterminate の ProgressBar）
  - エクスポート処理中は busy=true で ProgressBar を表示

## xaml.cs 変更点

### バグ修正

`_selectedProject.Steps[_selectedStepIndex]` が `ChkShowDeleted=false` 時にインデックス不整合を起こす問題を修正。フィルタ済み `_stepVms[_selectedStepIndex].Step` に統一。

対象ハンドラー: `BtnAnnSave_Click`, `BtnConfirmDesc_Click`, `BtnDeleteStep_Click`

### 追加ハンドラー

- `BtnExportMenu_Click`: ContextMenu を左クリックで展開
- `ChkShowDeleted_Changed`: トグル時に `LoadSteps` を再呼び出し

### 変更ハンドラー

- `LoadSteps`: `ChkShowDeleted.IsChecked` でフィルタ適用
- `SetStatus`: `bool busy = false` 引数追加、`PbStatus.Visibility` 制御
- `LstProjects_SelectionChanged`: `BtnExportMenu.IsEnabled` を設定
- `LoadAnnotationImage`: アノテーション存在時に `ExpAnnotation.IsExpanded = true`
- 全エクスポートハンドラー: `SetStatus(..., busy: true/false)` で進捗表示
