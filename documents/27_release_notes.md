# リリースノート 作成

## 日時
2026-05-27

## プロンプト（原文）

> 先ほど生成したRelease用一式をGitHubに公開します。
> リリースノートの記述内容を提案してください。

> （提案確認後）
> 提案したリリースノートを ./releases/ 配下にMarkdown形式で保存してください。

## 作業内容

GitHub Releases 公開に向け、日本語・英語のリリースノートを提案・作成した。

## 生成ファイル

| ファイル | サイズ | 説明 |
|---|---|---|
| `releases/RELEASE_NOTES_JA.md` | 約 4.9 KB | 日本語版リリースノート |
| `releases/RELEASE_NOTES_EN.md` | 約 3.4 KB | 英語版リリースノート（GitHub 国際向け） |

## リリースノートの構成

### 日本語版（RELEASE_NOTES_JA.md）

| セクション | 内容 |
|---|---|
| 概要 | タスクトレイ常駐型スクリーンショットツールの説明 |
| 主な機能 | 自動撮影・操作手順書自動生成・Azure AI Foundry連携・プライバシー・その他 |
| 動作環境 | OS / アーキテクチャ / .NET / 管理者権限 |
| インストール | 3ステップ手順 |
| 収録ファイル | ZIP内の全ファイルと用途 |
| 既知の制限事項 | WebP/docx非対応・ImageSharp CVE・LLM連携範囲・OCR要件 |
| ファイルの整合性確認 | SHA-256チェックサム |
| ライセンス | アプリ + OSSコンポーネント |

### 英語版（RELEASE_NOTES_EN.md）

日本語版と同等の構成。機能説明を英語圏ユーザー向けに簡潔に再構成。

## SHA-256（参照）

```
e008d0a9c84ff0b74cdc08b01a5a4b5ad757034ee06ce3499610b391dcd7e1ba  AutoScreenshot-v1.0.0-win-x64.zip
```

## GitHub Releases 公開手順（提案内容）

1. GitHub の Releases ページ → "Draft a new release"
2. Tag: `v1.0.0`、Title: `AutoScreenshot v1.0.0`
3. `releases/AutoScreenshot-v1.0.0-win-x64.zip` をアップロード
4. `releases/AutoScreenshot-v1.0.0-win-x64.zip.sha256` をアップロード
5. `RELEASE_NOTES_EN.md` または `RELEASE_NOTES_JA.md` の内容を Description 欄に貼り付けて公開
