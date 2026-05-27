# プロジェクトファイル機能 要件定義（ヒアリング）

## ユーザープロンプト

```
追加改修として、『プロジェクトファイル（フォルダ）』機能を追加したいと思います。
現状では、スクリーンショットを撮影すると同時に画像ファイルや動画、手順書を書き出していますが、
追加改修後には、スクリーンショットを撮影する時点ではプロジェクトファイルを更新するだけにして、
そこから動画、手順書、個別の画像を『エクスポート』する形に変更したいと思います。
プロジェクトファイルの形式は、
①画像やメタデータを1プロジェクト1フォルダにまとめたもの、
②画像やメタデータを単一Zipファイルに圧縮したもの、
③独自形式のバイナリファイル、
④それ以外の適切な形式の提案、
のいずれかにしてください。
私にヒアリングして、./requirements/追加要件_プロジェクトファイル.md に
追加改修の要件定義を書き出してください。
```

---

## ヒアリング実施内容

### 第1回ヒアリング（4問）

**Q1: プロジェクトファイルの形式**  
→ ① フォルダ形式（`.ascproj/`）

**Q2: プロジェクトの単位**  
→ ManualSession 単位（現行のセッション区切りと同じ）

**Q3: 過去プロジェクトの管理**  
→ 専用のプロジェクトビューウィンドウを新設

**Q4: ダブルクリックでの開き方**  
→（後のヒアリングで「ファイル関連付けなし」に変更。トレイメニューまたはプロジェクトビューから操作）

---

### 第2回ヒアリング（2問）

**Q5: ステップ編集の範囲**  
→ 削除 ＋ 説明文の手修正の両方

**Q6: ファイル関連付け**  
→ なし（エクスポートはトレイメニューまたはプロジェクトビューから実施）

---

### 第3回ヒアリング（2問）

**Q7: エクスポート成果物**  
→ 個別画像（PNG/JPEG）・手順書（Markdown/Word）・動画（APNG/MP4）・ZIPアーカイブの全種別

**Q8: プロジェクトフォルダの配置場所**  
→ 既存の `SaveFolder` 直下に作成（Pictures\AutoScreenshot\ の中に `.ascproj` フォルダを作る）

---

## 作成した要件定義書

**ファイル**: `requirements/追加要件_プロジェクトファイル.md`

### 主要決定事項

| 項目 | 決定内容 |
|---|---|
| プロジェクト形式 | `.ascproj/` フォルダ |
| プロジェクト単位 | ManualSession 単位 |
| フォルダ名規則 | `{YYYYMMDD_HHmmss}_{titleSlug}.ascproj` |
| 内部構造 | `images/`・`thumbs/`・`exports/`・`project.json` |
| project.json 書き込み方式 | `.tmp` ファイル経由の原子的置換 |
| サムネイル | JPEG・最大 320px 幅・Q=75・非同期生成 |
| エクスポート種別 | 画像（PNG/JPEG）・手順書（Markdown/Word）・動画（APNG/MP4）・ZIP |
| 管理UI | ProjectViewWindow（900×600px、リサイズ可能）|
| トレイメニュー | 「プロジェクト区切り」「エクスポート >」「プロジェクトを管理...」|
| 設定タブ | SettingsWindow に「プロジェクト」タブ追加（10 タブ目）|
| 終了時自動エクスポート | Markdown: オン / Word: オフ / 動画: オフ（設定で変更可）|
| 後方互換 | フォールバックモード（オフ時は v1.1.0 以前の動作）|
| ファイル関連付け | なし |

### 機能要件一覧

| ID | 概要 |
|---|---|
| FR-PJ01 | プロジェクトの自動作成（ManualSession 開始時）|
| FR-PJ02 | 撮影時のプロジェクト更新（images/ 保存 + project.json 追記）|
| FR-PJ03 | project.json の更新（排他ロックで整合性保護）|
| FR-PJ04 | エクスポート — 個別画像ファイル |
| FR-PJ05 | エクスポート — 手順書（Markdown/Word）|
| FR-PJ06 | エクスポート — 動画（APNG/MP4）|
| FR-PJ07 | エクスポート — ZIP アーカイブ |
| FR-PJ08 | プロジェクトビューウィンドウ |
| FR-PJ09 | ステップ削除（`_deleted/` フォルダに移動・物理削除なし）|
| FR-PJ10 | 説明文の手修正（`descriptionOverride` フィールド）|
| FR-PJ11 | トレイメニューの変更 |
| FR-PJ12 | 終了時の自動エクスポート（設定対応）|

### 非機能要件一覧

| ID | 要件 |
|---|---|
| NF-PJ01 | 撮影パフォーマンスへの影響最小化（非同期 Task.Run）|
| NF-PJ02 | project.json の耐障害性（.tmp リネーム方式）|
| NF-PJ03 | サムネイル生成の非同期化 |
| NF-PJ04 | 大量ステップ時の project.json サイズ（1000 steps ≈ 500 KB）|
| NF-PJ05 | プロジェクトビューの応答性（VirtualizingStackPanel 遅延読み込み）|
| NF-PJ06 | 既存 SaveFolder との共存（.ascproj 以外に手を加えない）|
| NF-PJ07 | エクスポートの多重実行防止 |

### 新規・変更クラス一覧

| クラス | 新規/変更 |
|---|---|
| `Models/ProjectConfig.cs` | **新規** |
| `Models/ProjectInfo.cs` | **新規** |
| `Services/ProjectStore.cs` | **新規** |
| `Services/ThumbnailService.cs` | **新規** |
| `Services/ExportService.cs` | **新規** |
| `Views/ProjectViewWindow.xaml` | **新規** |
| `Views/ProjectViewWindow.xaml.cs` | **新規** |
| `Models/AppConfig.cs` | 変更 |
| `Services/FileStorage.cs` | 変更 |
| `Services/ManualSessionRecorder.cs` | 変更 |
| `Services/NotifyIconWrapper.cs` | 変更 |
| `Services/MarkdownManualWriter.cs` | 変更 |
| `Services/DocxManualWriter.cs` | 変更 |
| `Services/VideoGenerator.cs` | 変更 |
| `Views/SettingsWindow.xaml` | 変更 |
| `Views/SettingsWindow.xaml.cs` | 変更 |

---

## 対象バージョン

AutoScreenshot **v1.2.0**（予定）
