## AutoScreenshot v1.2.0

**リリース日**: 2026-05-27

業務操作の証跡取得と操作手順書の自動作成を目的とした、
Windows タスクトレイ常駐型スクリーンショットツールの v1.2.0 リリースです。

---

### v1.2.0 新機能

#### プロジェクトファイル機能

セッション単位のすべての成果物を **`.ascproj/` フォルダ** にまとめて管理する機能を追加しました。
スクリーンショット・サムネイル・手順書・動画・エクスポート履歴が一箇所に集約されます。

**プロジェクトフォルダ構造:**

```
20260527_153524_操作手順書.ascproj/
├── project.json          ← メタデータ・ステップ一覧
├── images/               ← スクリーンショット
├── thumbs/               ← サムネイル（JPEG・最大 320px）
├── exports/              ← エクスポートした手順書・動画
├── events_YYYY-MM-DD.log ← 操作ログ
└── events_YYYY-MM-DD.jsonl
```

**プロジェクトビュー（「プロジェクトを管理...」）:**

- すべてのプロジェクトを一覧表示（作成日時降順）
- ステップのサムネイルグリッド表示・削除・説明文の手修正
- プロジェクトフォルダをエクスプローラーで開く

**エクスポート機能（ProjectViewWindow または トレイメニュー）:**

| エクスポート種別 | 出力先 |
|---|---|
| 画像 | `exports/{ts}_images/` |
| 手順書 (Markdown) | `exports/{ts}_slug.md` |
| 手順書 (Word) | `exports/{ts}_slug.docx` |
| 動画 (APNG/MP4) | `exports/` |
| ZIP アーカイブ | 任意の場所（ファイル保存ダイアログ） |

**プロジェクト内ステップ編集:**

- **ステップ削除**: 物理削除ではなく `images/_deleted/` に移動。`project.json` に削除フラグを記録
- **説明文の手修正**: `descriptionOverride` フィールドで LLM / ルールベース説明文を上書き

**トレイメニュー（v1.2.0 新構成）:**

- 「プロジェクト区切り（新しいプロジェクトを開始）」— 現セッションを保存して新規プロジェクトを開始
- 「エクスポート >」— サブメニューで現プロジェクトを即時エクスポート
- 「プロジェクトを管理...」— ProjectViewWindow を開く

**設定（設定ウィンドウ「プロジェクト」タブ）:**

| 設定項目 | 既定値 |
|---|---|
| プロジェクト機能を有効にする | オン |
| サムネイル最大幅（px） | 320 |
| 手順書を自動エクスポート | オン |
| Word を自動エクスポート | オフ |
| 動画を自動エクスポート | オフ |
| エクスポート完了時にフォルダを開く | オン |

> **後方互換性**: プロジェクト機能をオフにすると v1.1.0 と同じ動作（日付フォルダへの保存）になります。

---

### バグ修正

なし（v1.1.0 の既知バグはなし）

---

### 変更点（v1.1.0 → v1.2.0）

| カテゴリ | 変更内容 |
|---|---|
| 新規ファイル | `Models/ProjectConfig.cs` — プロジェクト機能設定モデル |
| 新規ファイル | `Models/ProjectInfo.cs` — project.json デシリアライズ対象（ProjectInfo / ProjectStep / ExportRecord） |
| 新規ファイル | `Services/ProjectStore.cs` — プロジェクトフォルダ作成・project.json 読み書き・一覧取得 |
| 新規ファイル | `Services/ThumbnailService.cs` — サムネイル生成（JPEG・最大 320px・非同期） |
| 新規ファイル | `Services/ExportService.cs` — エクスポート統括（画像 / 手順書 / 動画 / ZIP） |
| 新規ファイル | `Views/ProjectViewWindow.xaml` — プロジェクトビューウィンドウ（900×600px） |
| 変更 | `Models/AppConfig.cs` — `ProjectConfig Project` プロパティ追加 |
| 変更 | `Services/FileStorage.cs` — `SetProjectFolder` / `ClearProjectFolder` 追加 |
| 変更 | `Services/ManualSessionRecorder.cs` — ProjectStore と連携してプロジェクトを自動作成・更新 |
| 変更 | `Services/MetadataLogger.cs` — プロジェクトモードでのログ出力先をプロジェクトルートに修正 |
| 変更 | `Services/NotifyIconWrapper.cs` — トレイメニューを v1.2.0 構成に変更・ProjectStore / ExportService 追加 |
| 変更 | `Views/SettingsWindow.xaml` — 「プロジェクト」タブ（10タブ目）追加 |
| 変更 | `AutoScreenshot.csproj` — バージョン 1.2.0 |

---

### 動作環境

| 項目 | 要件 |
|---|---|
| OS | Windows 10 バージョン 1809 以降、Windows 11 |
| アーキテクチャ | x64 |
| .NET ランタイム | 不要（同梱） |
| 管理者権限 | 不要 |
| TTS 音声 | Windows 標準音声（追加インストール不要） |
| MP4 生成 | Windows MediaFoundation（Windows 10 以降は標準搭載） |

---

### インストール

1. `AutoScreenshot-v1.2.0-win-x64.zip` を任意のフォルダに展開
2. `AutoScreenshot.exe` を実行
3. タスクトレイにアイコンが表示されれば起動完了

**v1.1.0 からのアップデート:**
1. AutoScreenshot を終了する（トレイメニュー →「終了」）
2. インストールフォルダの全ファイルを上書きコピーする
3. `AutoScreenshot.exe` を実行する
4. 設定は自動引き継ぎ（`config.json` に `project` セクションが自動追加される）

> **プロジェクト機能について**: v1.1.0 の保存フォルダ構成（日付フォルダ）は v1.2.0 では変わります。既存の撮影画像には影響しませんが、新しいセッションから `.ascproj/` フォルダで管理されます。プロジェクト機能を無効にすると v1.1.0 と同じ動作になります。

> **初回起動時**: Windows SmartScreen の警告が表示される場合は「詳細情報」→「実行」をクリックしてください。

---

### 収録ファイル

| ファイル | 説明 |
|---|---|
| `AutoScreenshot.exe` | メイン実行ファイル（.NET 8・WPF ネイティブ DLL を内包） |
| `Microsoft.Windows.SDK.NET.dll` | OCR 機能（WinRT 投影）に必要 |
| `DocumentFormat.OpenXml.dll` 他 | Word (.docx) 出力に必要 |
| `Serilog.dll` 他 | ロギングに必要 |
| `SixLabors.ImageSharp.dll` | WebP エンコードに必要 |
| `System.Speech.dll` | TTS 音声ナレーションに必要 |
| `README.txt` | インストール・アンインストール手順 |

---

### 既知の制限事項

- **WebP 形式の画像は Word (.docx) に埋め込まれません**（Open XML が WebP 非対応のため。PNG/JPEG は正常に埋め込まれます）
- **WebP 形式のスクリーンショットは動画生成に使用されません**（フレーム合成時に PNG 変換済みとして扱います）
- **SixLabors.ImageSharp 3.1.7** に中程度の脆弱性（CVE）が報告されています。本ツールは内部でのエンコードのみに使用しており、外部からの WebP ファイルを読み込む処理はないためリスクは限定的です
- **LLM 連携機能は Azure AI Foundry のエンドポイントのみ対応**（Anthropic API への直接接続は行いません）
- **OCR 機能は OS の言語パック（日本語等）がインストールされている場合のみ有効**
- **TTS 音声の品質はシステム設定に依存**（Windows 標準音声は簡易品質。高品質な日本語音声（Microsoft Haruka 等）はシステム設定から追加可能）

---

### ファイルの整合性確認

```
SHA-256: 29340a1263497800af36923dc5cfbae88804b702573763c9dade3148e1a3067a
ファイル: AutoScreenshot-v1.2.0-win-x64.zip
```

---

### ライセンス

- アプリ本体: プライベート
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
- System.Speech 9.0.0: MIT License（.NET Foundation）
