# v1.5.0 リリースパッケージ作成記録

## 実施手順

1. `dotnet publish` で自己完結型バイナリを生成
2. Python `zipfile` モジュールで `AutoScreenshot-v1.5.0-win-x64.zip` を作成
3. Python `hashlib.sha256()` でチェックサムを計算し `.sha256` ファイルに書き出し
4. `releases/RELEASE_NOTES_v1.5.0_JA.md` / `_EN.md` の SHA-256 欄を更新
5. 一時ディレクトリ `publish_tmp/` を削除

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ ビルドに成功しました。0 エラー / 2 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.5.0-win-x64.zip` | 81,815,378 bytes（約 78 MB） |
| `releases/AutoScreenshot-v1.5.0-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `a2a2ffc9fb9629edb53589bb2241ef9b647877665e5936bc485736ddf563806c`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.5.0] — 2026-05-28` セクション記入済み |
| `releases/RELEASE_NOTES_v1.5.0_JA.md` | SHA-256 を記入 |
| `releases/RELEASE_NOTES_v1.5.0_EN.md` | SHA-256 を記入 |

## v1.5.0 実装サマリー

| 機能 | ドキュメント |
|---|---|
| `ProjectConfig.Enabled` 切り替え除去 | [42_v150_remove_project_enabled_toggle.md](42_v150_remove_project_enabled_toggle.md) |
| 動作確認 | [43_v150_verify.md](43_v150_verify.md) |
| バージョンバンプ準備 | [44_v150_version_prep.md](44_v150_version_prep.md) |

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.1.0 | 81,757,399 bytes |
| v1.4.0 | 81,816,195 bytes |
| v1.5.0 | 81,815,378 bytes |

v1.5.0 は v1.4.0 より 817 bytes 小さい（Enabled フラグ・UI 要素の削除によるコード縮小）。
