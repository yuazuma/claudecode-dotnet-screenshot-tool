# AutoScreenshot v1.6.0 リリースノート

**リリース日**: 2026-05-29

---

## 概要

操作前後スクリーンショット分離機能を追加しました。
クリックやキー入力ごとに「操作前」と「操作後」の 2 枚のスクリーンショットを
自動取得・保存し、手順書と証跡の双方の品質を高めます。

---

## 変更点

### Added（追加）

- **操作前後スクリーンショット分離（before / after）**
  - マウスボタン押下（DOWN）時に操作前 (before) スクリーンショットを PNG で自動取得
  - マウスボタン離放（UP）後 `PostClickDelayMs`（既定 250ms）遅延してから操作後 (after) を撮影
  - キーボードは新シーケンス開始時に before を取得、アイドル後に after を取得
  - before 画像はプロジェクト内 `images/before/` に PNG 固定で保存（劣化なし・証跡用途）
  - before 画像にはアノテーション・カーソルオーバーレイ・タイムスタンプを適用しない
  - Markdown / Word 手順書で before → after の順に 2 枚出力
  - HTML 手順書で before / after を横並び表示（キャプション付き）
  - 画像エクスポートで before を `exports/images/before/` サブフォルダに含める
  - ProjectViewWindow ステップ詳細に before 画像表示エリアを追加（読み取り専用・証跡）

- **設定項目追加**（「撮影トリガー」タブ）
  - 操作前スクリーンショットを取得する（既定: オン）
  - 操作後の撮影遅延: ms（既定: 250ms）

### Changed（変更）

- データモデル統一: `ImagePath` → `AfterImagePath`、`ThumbPath` → `AfterThumbPath`
  - 旧 `project.json` はロード時に自動移行（既存データはそのまま利用可能）
- 右クリック・中クリックの after 撮影タイミングを DOWN → UP + 遅延に変更

### Fixed（修正）

- キーボード before 画像の期限切れ問題を修正
  （長時間タイプ時に before が破棄されていた問題）

---

## インストール

1. `AutoScreenshot-v1.6.0-win-x64.zip` を展開して任意のフォルダに配置
2. `AutoScreenshot.exe` を実行（管理者権限・.NET ランタイム不要）
3. v1.5.x からのアップグレード:
   - 既存の `config.json` はそのまま使用可能
   - 既存の `.ascproj` フォルダはそのまま使用可能
     （旧 `imagePath` フィールドはロード時に自動的に `afterImagePath` へ移行）

SHA-256: `d831f7b67375e55dc665643a365caad82ca827d67401be3f4b238df449031cbd`

---

## 動作要件

- Windows 10 (1809) 以降 / Windows 11
- x64 アーキテクチャ
