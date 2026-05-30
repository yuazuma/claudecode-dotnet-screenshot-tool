# AutoScreenshot v1.7.1 リリースノート

**リリース日**: 2026-05-31

---

## 概要

v1.7.1 は Azure Windows Server 2025 環境での MP4 エクスポート・RDP キャプチャ・Markdown 画像表示を正式サポートするバグ修正・機能追加リリースです。
H.264 MFT が利用できない環境向けに FFmpeg フォールバックを実装し、**AVI 形式への出力を廃止して MP4 一本化**しました。
また RDP セッションでのスクリーンショット取得を `Windows.Graphics.Capture` API に切り替え、単色背景になる問題を解決しました。

---

## 変更点

### Added（追加）

- **`--export` CLI フラグによるヘッドレスエクスポート**
  - `AutoScreenshot.exe --export <プロジェクトパス> [--type md,html,video,images,zip]`
  - GUI なしでエクスポートを実行できます（RDP / 自動化スクリプト向け）
  - 終了コード: 0=成功、1=部分失敗、2=引数エラー

- **MP4 エクスポートの Azure Windows Server 2025 対応**
  - 以下の優先順位でエンコーダーを試行:
    1. `MfVideoWriter` — IMFSinkWriter H.264（標準）
    2. `H264Mp4Writer` — H.264 MFT 直接呼び出し（MPEG-4 マルチプレクサーをバイパス）
    3. `FfmpegMp4Writer` — FFmpeg によるエンコード（PATH または既知パスを自動検索）
  - **AVI 形式フォールバックを廃止**。MP4 が生成できない場合は明示的にエラーログを出力
  - FFmpeg 未インストール時のエラーメッセージに `DISM` コマンドによる Media Feature Pack インストール方法を案内

- **RDP セッションでのスクリーンキャプチャ改善**
  - `Windows.Graphics.Capture (WGC)` API を使用し、DWM コンポジターの出力を直接取得
  - RDP セッション検出時に自動的に WGC を使用（非 RDP 環境は GDI のまま）
  - WGC 失敗時は GDI `CopyFromScreen` にフォールバック
  - キャプチャ通知の黄色ボーダーを非表示（Windows 11 Build 22000+）

- **Markdown 手順書の画像を `_images/` サブフォルダ管理に変更**
  - MD ファイルと同階層に `{MDファイル名}_images/` を作成して画像をコピー
  - `../images/...` パスを廃止 → VS Code・GitHub 等で画像が正常表示される
  - 横幅 > 1200px の画像をアスペクト比維持でリサイズ（1200px 固定）

### Fixed（修正）

- **JSON プロパティ名重複**: `ManualGenConfig` の `outputFolder` が二重定義され、設定読み込み時に例外が発生していた問題を修正
- **フォルダテンプレートの `.ascproj` バグ**: `.ascproj` 中の文字 `s` が `DateTime.ToString()` の秒指定子として誤展開される問題を修正
- **JPEG 保存 `ArgumentException`**: ARGB Bitmap を JPEG 保存しようとして失敗する問題を修正（24bppRgb 変換を追加）
- **Markdown 画像の重複コピー**: before/after が同一ファイルを参照する場合に余分なコピー（`step_001_1.png` 等）が生成される問題を修正
- **VideoGenerator dur バグ**: フレーム時間（秒単位）を 10,000,000 で誤除算していた問題を修正

### Changed（変更）

- バージョン: 1.7.0 → 1.7.1

---

## インストール

1. `AutoScreenshot-v1.7.1-win-x64.zip` を展開して任意のフォルダに配置
2. `AutoScreenshot.exe` を実行（管理者権限・.NET ランタイム不要）
3. v1.7.0 からのアップグレード:
   - 既存の `config.json` はそのまま使用可能
   - 既存の `.ascproj` フォルダはそのまま使用可能

> **MP4 エクスポートについて**: Azure Windows Server など H.264 エンコーダーが存在しない環境では
> FFmpeg が必要です。`winget install Gyan.FFmpeg` または
> `DISM /Online /Add-Capability /CapabilityName:Media.MediaFeaturePack~~~~0.0.1.0`
> のいずれかでインストールしてください。

SHA-256: `68b96b11ef6ae2c161d36ff2fe7274e65a26e854d6ff94ca975b70adc4abece2`

---

## 動作要件

- Windows 10 (1809) 以降 / Windows 11
- x64 アーキテクチャ
- MP4 エクスポート: H.264 MFT（Windows クライアント標準搭載）または FFmpeg 8.x+
- RDP キャプチャ: Windows 10 1803+ / Windows Server 2019+（WGC API 対応）
