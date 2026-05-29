# AutoScreenshot v1.6.1 リリースノート

**リリース日**: 2026-05-29

---

## 概要

設定ウィンドウの UI 修正のみのパッチリリースです。新機能はありません。

---

## 変更点

### Fixed（修正）

- **エンドポイント URL 入力欄のマスク表示を解除**
  - 「LLM連携」タブの「Microsoft Azure AI Foundry エンドポイント URL」入力欄を
    `PasswordBox`（入力文字が `●` で隠れる）から `TextBox`（平文表示）に変更
  - URL を直接確認・コピーできるようになりました
  - config.json への保存は引き続き Windows DPAPI で暗号化されます（NF-04 変更なし）

### Changed（変更）

- バージョン: 1.6.0 → 1.6.1

---

## インストール

1. `AutoScreenshot-v1.6.1-win-x64.zip` を展開して任意のフォルダに配置
2. `AutoScreenshot.exe` を実行（管理者権限・.NET ランタイム不要）
3. v1.6.0 からのアップグレード:
   - 既存の `config.json` はそのまま使用可能
   - 既存の `.ascproj` フォルダはそのまま使用可能

SHA-256: `4edf6dea1cb2450cbe61d42966964f92e0e04d08935dc4d742438754a4bf71f3`

---

## 動作要件

- Windows 10 (1809) 以降 / Windows 11
- x64 アーキテクチャ
