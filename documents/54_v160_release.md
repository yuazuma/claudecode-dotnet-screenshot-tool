# v1.6.0 リリースパッケージ作成記録

## 実施手順

1. `releases/CHANGELOG.md` に `[1.6.0] — 2026-05-29` セクションを記入
2. `releases/RELEASE_NOTES_v1.6.0_JA.md` / `_EN.md` を実装内容で更新
3. 実行中の AutoScreenshot.exe を終了（taskkill）
4. `dotnet publish` で自己完結型バイナリを生成
5. Python `zipfile` モジュールで `AutoScreenshot-v1.6.0-win-x64.zip` を作成
6. Python `hashlib.sha256()` でチェックサムを計算し `.sha256` ファイルに書き出し
7. リリースノートの SHA-256 欄を更新
8. `publish_tmp/` を削除

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ ビルドに成功しました。0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.6.0-win-x64.zip` | 78,992,971 bytes（約 75 MB） |
| `releases/AutoScreenshot-v1.6.0-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `d831f7b67375e55dc665643a365caad82ca827d67401be3f4b238df449031cbd`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.6.0] — 2026-05-29` セクション記入済み |
| `releases/RELEASE_NOTES_v1.6.0_JA.md` | 全文記入・SHA-256 記入 |
| `releases/RELEASE_NOTES_v1.6.0_EN.md` | 全文記入・SHA-256 記入 |

## v1.6.0 実装サマリー

| 機能 | ドキュメント |
|---|---|
| 実装可否検討 | [51_v160_前後イベント実装可否.md](51_v160_前後イベント実装可否.md) |
| 要件定義書 | [52_v160_要件定義書作成.md](52_v160_要件定義書作成.md) |
| 実装 | [49_v160_before_after_impl.md](49_v160_before_after_impl.md) |
| 動作確認・不具合修正 | [53_v160_verify_and_fix.md](53_v160_verify_and_fix.md) |

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.1.0 | 81,757,399 bytes |
| v1.4.0 | 81,816,195 bytes |
| v1.5.0 | 81,815,378 bytes |
| v1.5.1 | 78,987,170 bytes |
| v1.6.0 | 78,992,971 bytes |
