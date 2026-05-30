# AutoScreenshot v1.7.1 リリースノート

**リリース日**: 2026-05-30

---

## 概要

v1.7.1 は Azure Windows Server 2025 環境での MP4 エクスポートを正式サポートするバグ修正・機能追加リリースです。
H.264 MFT が利用できない環境（Azure Server、Desktop Experience 未インストール等）向けに FFmpeg を用いたフォールバックを実装し、**AVI 形式への出力を廃止して MP4 一本化**しました。

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
  - FFmpeg 未インストール時のエラーメッセージに `DISM` コマンドによる Media Feature Pack
    インストール方法を案内

### Fixed（修正）

- **JSON プロパティ名重複**: `ManualGenConfig` の `outputFolder` が二重定義され、設定読み込み時に例外が発生していた問題を修正
- **フォルダテンプレートの `.ascproj` バグ**: `.ascproj` 中の文字 `s` が `DateTime.ToString()` の秒指定子として誤展開される問題を修正
- **JPEG 保存 `ArgumentException`**: ARGB Bitmap を JPEG 保存しようとして失敗する問題を修正（24bppRgb 変換を追加）

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

SHA-256: `84a043e2f598c3a2bef7ea55f9f1cd09a974b6d2ab12ad8aae8c645dec8a657d`

---

## 動作要件

- Windows 10 (1809) 以降 / Windows 11
- x64 アーキテクチャ
- MP4 エクスポート: H.264 MFT（Windows クライアント標準搭載）または FFmpeg 8.x+
