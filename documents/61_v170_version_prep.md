# v1.7.0 バージョン宣言・リリースノート準備

## ユーザープロンプト

```
プロジェクトファイルなどのバージョン番号を1.7.0に更新してください。リリースノートについては、
1.6.1のものとは別に1.7.0のものを新規作成してください。
```

---

## 実施内容

### 1. プロジェクトファイルのバージョン更新

**ファイル**: `src/AutoScreenshot/AutoScreenshot.csproj`

| 属性 | 変更前 | 変更後 |
|---|---|---|
| `<Version>` | `1.6.1` | `1.7.0` |
| `<AssemblyVersion>` | `1.6.1.0` | `1.7.0.0` |
| `<FileVersion>` | `1.6.1.0` | `1.7.0.0` |

### 2. バージョン情報ダイアログの文字列更新

**ファイル**: `src/AutoScreenshot/Services/NotifyIconWrapper.cs`

```csharp
// 変更前
"AutoScreenshot v1.6.1\n\n..."
// 変更後
"AutoScreenshot v1.7.0\n\n..."
```

### 3. CHANGELOG.md に [1.7.0] セクション追加（プレースホルダー）

`[1.6.1]` の前に `[1.7.0] — TBD` セクションを追加。実装後に記入。

### 4. リリースノート新規作成

| ファイル | 内容 |
|---|---|
| `releases/RELEASE_NOTES_v1.7.0_JA.md` | 日本語リリースノート（プレースホルダー） |
| `releases/RELEASE_NOTES_v1.7.0_EN.md` | 英語リリースノート（プレースホルダー） |

---

## 変更ファイル一覧

| ファイル | 変更種別 |
|---|---|
| `src/AutoScreenshot/AutoScreenshot.csproj` | バージョン 1.6.1 → 1.7.0 |
| `src/AutoScreenshot/Services/NotifyIconWrapper.cs` | バージョン文字列 v1.6.1 → v1.7.0 |
| `releases/CHANGELOG.md` | [1.7.0] — TBD セクション追加 |
| `releases/RELEASE_NOTES_v1.7.0_JA.md` | 新規作成（プレースホルダー） |
| `releases/RELEASE_NOTES_v1.7.0_EN.md` | 新規作成（プレースホルダー） |
