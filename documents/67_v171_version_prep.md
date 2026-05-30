# v1.7.1 バージョン宣言・リリースノート準備

## ユーザープロンプト

```
プロジェクトファイルなどのバージョン番号を1.7.1に更新してください。リリースノートについては、
既存のものとは別に1.7.1のものを新規作成してください。
```

---

## 実施内容

### 1. プロジェクトファイルのバージョン更新

**ファイル**: `src/AutoScreenshot/AutoScreenshot.csproj`

| 属性 | 変更前 | 変更後 |
|---|---|---|
| `<Version>` | `1.7.0` | `1.7.1` |
| `<AssemblyVersion>` | `1.7.0.0` | `1.7.1.0` |
| `<FileVersion>` | `1.7.0.0` | `1.7.1.0` |

### 2. バージョン情報ダイアログの文字列更新

**ファイル**: `src/AutoScreenshot/Services/NotifyIconWrapper.cs`

```csharp
// 変更前
"AutoScreenshot v1.7.0\n\n..."
// 変更後
"AutoScreenshot v1.7.1\n\n..."
```

### 3. CHANGELOG.md に [1.7.1] セクション追加（プレースホルダー）

`[1.7.0]` の前に `[1.7.1] — TBD` セクションを追加。実装後に記入。

### 4. リリースノート新規作成

| ファイル | 内容 |
|---|---|
| `releases/RELEASE_NOTES_v1.7.1_JA.md` | 日本語（プレースホルダー） |
| `releases/RELEASE_NOTES_v1.7.1_EN.md` | 英語（プレースホルダー） |

---

## 変更ファイル一覧

| ファイル | 変更種別 |
|---|---|
| `src/AutoScreenshot/AutoScreenshot.csproj` | 1.7.0 → 1.7.1 |
| `src/AutoScreenshot/Services/NotifyIconWrapper.cs` | v1.7.0 → v1.7.1 |
| `releases/CHANGELOG.md` | [1.7.1] — TBD セクション追加 |
| `releases/RELEASE_NOTES_v1.7.1_JA.md` | 新規作成（プレースホルダー） |
| `releases/RELEASE_NOTES_v1.7.1_EN.md` | 新規作成（プレースホルダー） |
