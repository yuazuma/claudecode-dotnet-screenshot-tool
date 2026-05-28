# v1.4.0 リリースパッケージ作成記録

## 実施手順

1. `dotnet publish` で自己完結型バイナリを生成
2. Python `zipfile` モジュールで `AutoScreenshot-v1.4.0-win-x64.zip` を作成
3. Python `hashlib.sha256()` でチェックサムを計算し `.sha256` ファイルに書き出し
4. `releases/CHANGELOG.md` の `[1.4.0] — TBD` セクションを実際の変更内容で更新
5. `releases/RELEASE_NOTES_v1.4.0_JA.md` / `_EN.md` を完成版に更新
6. 一時ディレクトリ `publish_tmp/` を削除

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ ビルドに成功しました。0 エラー / 2 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.4.0-win-x64.zip` | 81,816,195 bytes（約 78 MB） |
| `releases/AutoScreenshot-v1.4.0-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `0bf25b4b00f6df6246be7d6ea3d55d31c66e7f9088fc4b15f575bc271a3776f0`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.4.0] — 2026-05-28` セクションを実装内容で記入（アイコン5状態・メニュー再構成・設定タブ順序・プロジェクトビュー刷新） |
| `releases/RELEASE_NOTES_v1.4.0_JA.md` | 概要・新機能・SHA-256・リリース日を記入 |
| `releases/RELEASE_NOTES_v1.4.0_EN.md` | 同上（英語） |

## v1.4.0 実装サマリー（各詳細は documents/37〜40 参照）

| 機能 | ドキュメント |
|---|---|
| トレイメニュー再構成 | [37_v140_tray_menu_restructure.md](37_v140_tray_menu_restructure.md) |
| タスクトレイアイコン 5 状態 | [38_v140_icon_state.md](38_v140_icon_state.md) |
| 設定ウィンドウ タブ順序変更 | [39_v140_settings_tab_reorder.md](39_v140_settings_tab_reorder.md) |
| プロジェクトビュー UI リファクタリング | [40_v140_project_view_refactor.md](40_v140_project_view_refactor.md) |
