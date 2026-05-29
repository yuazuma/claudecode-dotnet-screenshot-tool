# AutoScreenshot v1.6.1 リリースノート

**リリース日**: 2026-05-29

---

## 概要

LLM 連携の不具合修正、設定 UI の改善、before 画像フォールバックを含むパッチリリースです。

---

## 変更点

### Fixed（修正）

#### LLM 呼び出し形式を Anthropic Messages API に変更

Azure AI Foundry エンドポイントが Anthropic Messages API 形式（`/anthropic/v1/messages`）を
使用しているにもかかわらず、内部の SDK が OpenAI 互換形式で呼び出していたため、
LLM 連携（インクリメンタル LLM・一括 LLM）が全件失敗していた問題を修正しました。

**変更内容:**
- URL: `{base}/anthropic/v1/messages`（設定エンドポイントのホスト部分を使用）
- 認証: `Authorization: Bearer {key}` ヘッダー
- バージョン指定: `anthropic-version: 2023-06-01` ヘッダー
- リクエスト形式: Anthropic Messages API（`system` はトップレベルフィールド）
- DPAPI による暗号化保存は変更なし（NF-04 維持）

#### エンドポイント URL 入力欄のマスク表示を解除

設定ウィンドウ「LLM 連携」タブの「Microsoft Azure AI Foundry エンドポイント URL」
入力欄を `PasswordBox`（`●` 表示）から `TextBox`（平文表示）に変更しました。
URL の確認・コピーが容易になります。

#### before 画像フォールバック

`BeforeImagePath` が未取得のステップで、エクスポート時に before 欄が空白になっていた問題を修正。
before が未取得の場合、素の after 画像（アノテーション焼き込み前）を before として使用します。

これにより、アノテーションが付与されたステップでは「アノテーション前 → アノテーション後」の
比較が可能になります。

**適用箇所:** Markdown / Word / HTML エクスポート、画像エクスポート、ProjectViewWindow 表示

### Changed（変更）

- バージョン: 1.6.0 → 1.6.1

---

## インストール

1. `AutoScreenshot-v1.6.1-win-x64.zip` を展開して任意のフォルダに配置
2. `AutoScreenshot.exe` を実行（管理者権限・.NET ランタイム不要）
3. v1.6.0 からのアップグレード:
   - 既存の `config.json` はそのまま使用可能
   - 既存の `.ascproj` フォルダはそのまま使用可能

SHA-256: `7cb2208b23a546cf8bf48761565808b8cb18c1ab9d650553ae43055c55bd8c69`

---

## 動作要件

- Windows 10 (1809) 以降 / Windows 11
- x64 アーキテクチャ
