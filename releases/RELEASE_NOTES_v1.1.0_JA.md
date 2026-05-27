## AutoScreenshot v1.1.0

**リリース日**: 2026-05-27

業務操作の証跡取得と操作手順書の自動作成を目的とした、
Windows タスクトレイ常駐型スクリーンショットツールの v1.1.0 リリースです。

---

### v1.1.0 新機能

#### 動画自動生成機能

手順書と同じ操作ステップから **APNG（アニメーション PNG）** および **MP4（H.264）** 動画ファイルを自動生成する機能を追加しました。

**主な特長:**

- **APNG 出力**: 純マネージド実装（PNG チャンク手書き）。ブラウザやほぼすべての PNG ビューアで再生可能
- **MP4 出力**: Windows MediaFoundation（IMFSinkWriter）による H.264 + AAC エンコード
- **TTS 音声ナレーション**: 各ステップの説明文を Windows SAPI で読み上げて MP4 の音声トラックに合成
- **フレーム装飾**:
  - 波紋エフェクト（マウス操作位置を中心に 3 段階同心円）
  - 操作位置枠（カーソル周辺の破線矩形）
  - テロップ帯（操作種別・説明文・タイムスタンプを下部に表示）
- **出力解像度**: HD (1280×720) / Full HD (1920×1080) / QHD (2560×1440) から選択
- **フレームタイミング**: 固定秒数 / TTS 読み上げ時間に合わせる可変モード
- **バックグラウンド生成**: 生成中も記録・手順書生成を継続できる
- **トレイメニュー「動画を生成」**: 現在のセッションから即時生成
- **手順書と同時生成オプション**: 手順書生成のたびに動画も自動生成

**設定（設定ウィンドウ「動画生成」タブ）:**

| 設定項目 | 既定値 |
|---|---|
| APNG を出力 | オン |
| MP4 を出力 | オン |
| 動画出力先フォルダ | 自動（保存フォルダ内 `videos\`）|
| フレームタイミング | 固定 3.0 秒 |
| 出力解像度 | HD (1280×720) |
| MP4 ビットレート | 4 Mbps |
| 波紋エフェクト | オン |
| 操作位置枠 | オン |
| テロップ表示 | オン |
| TTS 音声ナレーション | オン |
| 完了時に出力フォルダを開く | オン |
| 手順書生成と同時に動画を生成 | オフ |

---

### バグ修正

なし（v1.0.0 は初版リリースのため）

---

### 変更点（v1.0.0 → v1.1.0）

| カテゴリ | 変更内容 |
|---|---|
| 新規ファイル | `Models/VideoGenConfig.cs` — 動画生成設定モデル |
| 新規ファイル | `Services/TtsService.cs` — Windows SAPI TTS |
| 新規ファイル | `Services/FrameRenderer.cs` — フレーム合成 |
| 新規ファイル | `Services/ApngWriter.cs` — APNG 書き込み |
| 新規ファイル | `Services/MfVideoWriter.cs` — MediaFoundation MP4 出力 |
| 新規ファイル | `Services/VideoGenerator.cs` — 動画生成統括 |
| 変更 | `Models/AppConfig.cs` — `VideoGenConfig VideoGen` プロパティ追加 |
| 変更 | `Services/ManualSessionRecorder.cs` — `SetVideoGenerator` / `GenerateVideoNow` 追加 |
| 変更 | `Services/NotifyIconWrapper.cs` — 「動画を生成」メニュー追加 |
| 変更 | `Services/Notifier.cs` — `ShowBalloon(title, message)` メソッド追加 |
| 変更 | `Views/SettingsWindow.xaml` — 「動画生成」タブ（9タブ目）追加 |
| 変更 | `Views/SettingsWindow.xaml.cs` — 動画生成タブの LoadSettings / ApplySettings |
| 変更 | `AutoScreenshot.csproj` — `System.Speech 9.0.0` パッケージ追加・バージョン 1.1.0 |

---

### 動作環境

| 項目 | 要件 |
|---|---|
| OS | Windows 10 バージョン 1809 以降、Windows 11 |
| アーキテクチャ | x64 |
| .NET ランタイム | 不要（同梱） |
| 管理者権限 | 不要 |
| TTS 音声 | Windows 標準音声（追加インストール不要、高品質音声は別途インストールで向上可能） |
| MP4 生成 | Windows MediaFoundation（Windows 10 以降は標準搭載） |

---

### インストール

1. `AutoScreenshot-v1.1.0-win-x64.zip` を任意のフォルダに展開
2. `AutoScreenshot.exe` を実行
3. タスクトレイにアイコンが表示されれば起動完了

**v1.0.0 からのアップデート:**
1. AutoScreenshot を終了する（トレイメニュー →「終了」）
2. インストールフォルダの全ファイルを上書きコピーする
3. `AutoScreenshot.exe` を実行する
4. 設定は自動引き継ぎ（`config.json` に `videoGen` セクションが自動追加される）

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
| `System.Speech.dll` | TTS 音声ナレーションに必要（v1.1.0 追加）|
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

### ライセンス

- アプリ本体: プライベート
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
- System.Speech 9.0.0: MIT License（.NET Foundation）
