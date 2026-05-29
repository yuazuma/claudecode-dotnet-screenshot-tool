# v1.6.1 リリースパッケージ作成記録

## 実施手順

1. `releases/CHANGELOG.md` に `[1.6.1]` セクションを全変更内容で更新
2. `releases/RELEASE_NOTES_v1.6.1_JA.md` / `_EN.md` を全変更内容で再作成
3. 実行中の AutoScreenshot.exe を終了（taskkill）
4. `dotnet publish` で自己完結型バイナリを生成
5. Python `zipfile` モジュールで `AutoScreenshot-v1.6.1-win-x64.zip` を作成
6. Python `hashlib.sha256()` でチェックサムを計算し `.sha256` ファイルに書き出し
7. リリースノートの SHA-256 欄を更新
8. `publish_tmp/` を削除

---

## v1.6.1 変更内容サマリー

| 変更内容 | ファイル | ドキュメント |
|---|---|---|
| LLM 呼び出し → Anthropic Messages API | `Services/LlmService.cs` | FR-G1 |
| エンドポイント URL TextBox 化 | `Views/SettingsWindow.xaml` / `.xaml.cs` | FR-G2 |
| before 画像フォールバック | `Services/ExportService.cs`, `HtmlManualWriter.cs`, `Views/ProjectViewWindow.xaml.cs` | FR-G3 |

詳細要件: `requirements/追加要件_LLM呼出修正.md`

---

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ 0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.6.1-win-x64.zip` | 78,994,794 bytes（約 75 MB） |
| `releases/AutoScreenshot-v1.6.1-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `7cb2208b23a546cf8bf48761565808b8cb18c1ab9d650553ae43055c55bd8c69`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.6.1]` セクションを全変更内容で更新 |
| `releases/RELEASE_NOTES_v1.6.1_JA.md` | 全文更新・SHA-256 記入 |
| `releases/RELEASE_NOTES_v1.6.1_EN.md` | 全文更新・SHA-256 記入 |

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.6.0 | 78,992,971 bytes |
| v1.6.1（初版） | 78,992,959 bytes |
| v1.6.1（最終） | 78,994,794 bytes |
