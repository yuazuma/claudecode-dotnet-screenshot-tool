# v1.5.0 バージョン宣言・リリースノート準備

## ユーザープロンプト

```
これから、v1.5.0へのアップデート作業を行います。プロジェクトファイルなどの
バージョン番号を更新してください。リリースノートについては、1.4.0のものとは別に
1.5.0のものを新規作成してください。
```

---

## 実施内容

### 1. プロジェクトファイルのバージョン更新

**ファイル**: `src/AutoScreenshot/AutoScreenshot.csproj`

| 属性 | 変更前 | 変更後 |
|---|---|---|
| `<Version>` | `1.4.0` | `1.5.0` |
| `<AssemblyVersion>` | `1.4.0.0` | `1.5.0.0` |
| `<FileVersion>` | `1.4.0.0` | `1.5.0.0` |

### 2. バージョン情報ダイアログの文字列更新

**ファイル**: `src/AutoScreenshot/Services/NotifyIconWrapper.cs`

`BuildVersionItem()` 内の文字列リテラル:

```csharp
// 変更前
"AutoScreenshot v1.4.0\n\nタスクトレイ常駐型 自動スクリーンショット撮影・動画生成ツール"

// 変更後
"AutoScreenshot v1.5.0\n\nタスクトレイ常駐型 自動スクリーンショット撮影・動画生成ツール"
```

### 3. CHANGELOG.md に [1.5.0] セクション追加（プレースホルダー）

**ファイル**: `releases/CHANGELOG.md`

`[1.4.0]` の前に `[1.5.0] — TBD` セクションを追加。
内容は実装後に [42_v150_remove_project_enabled_toggle.md](42_v150_remove_project_enabled_toggle.md) をもとに記入。

### 4. リリースノート新規作成

| ファイル | 内容 |
|---|---|
| `releases/RELEASE_NOTES_v1.5.0_JA.md` | 日本語リリースノート（新規作成、詳細は実装後に記入） |
| `releases/RELEASE_NOTES_v1.5.0_EN.md` | 英語リリースノート（同上） |

インストール手順には「v1.4.0 からのアップグレード: `config.json` と `.ascproj` はそのまま使用可能」を明記。

---

## 変更ファイル一覧

| ファイル | 変更種別 |
|---|---|
| `src/AutoScreenshot/AutoScreenshot.csproj` | バージョン 1.4.0 → 1.5.0 |
| `src/AutoScreenshot/Services/NotifyIconWrapper.cs` | バージョン文字列 v1.4.0 → v1.5.0 |
| `releases/CHANGELOG.md` | [1.5.0] — TBD セクション追加 |
| `releases/RELEASE_NOTES_v1.5.0_JA.md` | 新規作成（プレースホルダー） |
| `releases/RELEASE_NOTES_v1.5.0_EN.md` | 新規作成（プレースホルダー） |
