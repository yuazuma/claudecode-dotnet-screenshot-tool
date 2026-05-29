# v1.6.1 バージョン更新・リリースパッケージ作成記録

## ユーザープロンプト

```
プロジェクトファイルなどのバージョン番号を1.6.1に更新してください。リリースノートについては、
1.6.0のものとは別に1.6.1のものを新規作成してください。./release/配下のバイナリ・ハッシュ・
リリースノートをv1.6.1としてクリーンビルドしてアップデートしてください。設計書類も更新してください。
```

---

## v1.6.1 変更内容

設定ウィンドウ LLM 連携タブの「Microsoft Azure AI Foundry エンドポイント URL」入力欄を
`PasswordBox`（マスク表示）から `TextBox`（平文表示）に変更。

| ファイル | 変更内容 |
|---|---|
| `Views/SettingsWindow.xaml:269` | `PasswordBox x:Name="PwdLlmEndpoint"` → `TextBox x:Name="TxtLlmEndpoint"` |
| `Views/SettingsWindow.xaml.cs` LoadSettings | `PwdLlmEndpoint.Password` → `TxtLlmEndpoint.Text` |
| `Views/SettingsWindow.xaml.cs` ApplySettings | `PwdLlmEndpoint.Password` → `TxtLlmEndpoint.Text.Trim()` |

config.json への保存は引き続き DPAPI 暗号化（NF-04 変更なし）。

---

## バージョン更新

| ファイル | 変更前 | 変更後 |
|---|---|---|
| `AutoScreenshot.csproj` Version | 1.6.0 | 1.6.1 |
| `AutoScreenshot.csproj` AssemblyVersion | 1.6.0.0 | 1.6.1.0 |
| `AutoScreenshot.csproj` FileVersion | 1.6.0.0 | 1.6.1.0 |
| `NotifyIconWrapper.cs` バージョン文字列 | v1.6.0 | v1.6.1 |

---

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true -o publish_tmp
→ 0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.6.1-win-x64.zip` | 78,992,959 bytes（約 75 MB） |
| `releases/AutoScreenshot-v1.6.1-win-x64.zip.sha256` | SHA-256 チェックサムファイル |

**SHA-256**: `4edf6dea1cb2450cbe61d42966964f92e0e04d08935dc4d742438754a4bf71f3`

## 更新したリリース関連ファイル

| ファイル | 変更内容 |
|---|---|
| `releases/CHANGELOG.md` | `[1.6.1] — 2026-05-29` セクション追加 |
| `releases/RELEASE_NOTES_v1.6.1_JA.md` | 新規作成・SHA-256 記入 |
| `releases/RELEASE_NOTES_v1.6.1_EN.md` | 新規作成・SHA-256 記入 |

## ZIP サイズ比較

| バージョン | ZIP サイズ |
|---|---|
| v1.5.1 | 78,987,170 bytes |
| v1.6.0 | 78,992,971 bytes |
| v1.6.1 | 78,992,959 bytes |
