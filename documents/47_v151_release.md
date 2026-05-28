# v1.5.1 リリースパッケージ作成記録

## 実施手順

1. `AutoScreenshot.csproj`: Version / AssemblyVersion / FileVersion を 1.5.0 → 1.5.1 に更新
2. `NotifyIconWrapper.cs`: バージョン文字列を `v1.5.0` → `v1.5.1` に更新
3. `releases/CHANGELOG.md`: `[1.5.1] — 2026-05-28` セクションを `[1.5.0]` の前に追加
4. `releases/RELEASE_NOTES_v1.5.1_JA.md` / `_EN.md` を新規作成
5. `dotnet publish` で自己完結型バイナリを生成
6. Python `zipfile` モジュールで `AutoScreenshot-v1.5.1-win-x64.zip` を作成
7. Python `hashlib.sha256()` でチェックサムを計算し `.sha256` ファイルに書き出し
8. リリースノートの SHA-256 欄を更新
9. `publish_tmp/` を削除

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ ビルドに成功しました。0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.5.1-win-x64.zip` | 78,987,170 bytes（約 75 MB） |
| `releases/AutoScreenshot-v1.5.1-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `49f8e5000d1049f28d2af9fa42245bb74f9d1a8d299c2baecbf5da8c7a913ed8`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.5.1] — 2026-05-28` セクション記入済み |
| `releases/RELEASE_NOTES_v1.5.1_JA.md` | 新規作成・SHA-256 記入 |
| `releases/RELEASE_NOTES_v1.5.1_EN.md` | 新規作成・SHA-256 記入 |

## v1.5.1 実装サマリー

| 修正内容 | ドキュメント |
|---|---|
| バグ修正 4 件 | [46_v151_bugfix.md](46_v151_bugfix.md) |

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.1.0 | 81,757,399 bytes |
| v1.4.0 | 81,816,195 bytes |
| v1.5.0 | 81,815,378 bytes |
| v1.5.1 | 78,987,170 bytes |

v1.5.1 は v1.5.0 より約 2.7 MB 小さい（デッドコード除去による圧縮効果と zip圧縮率の差異）。
