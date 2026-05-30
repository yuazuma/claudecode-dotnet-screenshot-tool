# v1.7.0 リリースパッケージ作成記録

## 実施手順

1. `releases/CHANGELOG.md` に `[1.7.0] — 2026-05-30` セクションを記入
2. `releases/RELEASE_NOTES_v1.7.0_JA.md` / `_EN.md` を全変更内容で更新
3. 実行中の AutoScreenshot.exe を終了
4. `dotnet publish` で自己完結型バイナリを生成
5. Python `zipfile` モジュールで ZIP を作成
6. SHA-256 を計算し `.sha256` ファイルに書き出し
7. リリースノートの SHA-256 欄を更新
8. `publish_tmp/` を削除

---

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true
→ 0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.7.0-win-x64.zip` | 79,008,966 bytes（約 75 MB） |

**SHA-256**: `62e51d2248fca58d51453c7754fbaa766fe7a6abef643dc0a138311afb82718c`

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.6.1 | 78,994,794 bytes |
| v1.7.0 | 79,008,966 bytes |

## v1.7.0 実装サマリー

| 機能 | ドキュメント |
|---|---|
| FR-H1〜H6 実装 | [62_v170_impl.md](62_v170_impl.md) |
| 要件定義書 | `requirements/追加要件_1.7.0.md` |
