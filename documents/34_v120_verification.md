# v1.2.0 動作検証レポート

## 概要

v1.2.0「プロジェクトファイル機能」の実装後、実際に exe を起動して動作を検証した。
検証の結果、3つのバグを発見・修正した。

---

## 検証環境

- OS: Windows Server 2025 Datacenter Azure Edition 10.0.26100
- Publish: Self-contained, win-x64
- 設定: デフォルト設定（Project.Enabled = true）

---

## 発見されたバグと修正

### Bug 1: `project.json.tmp` の残留（要経過観察）

**現象**: 検証中に `project.json.tmp` が `project.json` と同時に存在することがあった。

**原因**: `File.Move` が例外をスローした場合 `.tmp` が残留する可能性があった。また検証タイミングによっては書き込み中を観察した可能性がある。

**修正**: `ProjectStore.WriteProjectJsonAsync` に `catch` ブロックを追加し、`File.Move` 失敗時に `.tmp` を削除するよう修正。

```csharp
catch
{
    try { File.Delete(tmpPath); } catch { }
    throw;
}
```

### Bug 2: `thumbPath` にバックスラッシュが混入

**現象**: `project.json` の `thumbPath` フィールドが `"thumbs\\step_001.jpg"` となっていた（`imagePath` は `"images/..."` と正常）。

**原因**: `ManualSessionRecorder.RecordProjectStepAsync` で `Path.Combine("thumbs", thumbFileName)` を使用しており、Windows では `\` 区切りになる。

**修正**: `$"thumbs/{thumbFileName}"` に変更。

### Bug 3: MetadataLogger がイベントログを `images/` 配下に書き込む

**現象**: プロジェクトモードで `{project}/images/events_2026-05-27.log` が生成され、IOException が多発。

**原因**: `MetadataLogger.LogEventAsync` が `imagePath` の親ディレクトリをログ出力先として使用している。プロジェクトモードでは画像が `{project}/images/` に保存されるため、ログも同フォルダに書き込もうとした。

**修正**: `ResolveLogDir` メソッドを追加し、ディレクトリ名が `images` の場合は親ディレクトリ（プロジェクトルート）をログ先として使用。

```csharp
private static string ResolveLogDir(string imagePath)
{
    string dir = Path.GetDirectoryName(imagePath)!;
    return string.Equals(Path.GetFileName(dir), "images", StringComparison.OrdinalIgnoreCase)
        ? (Path.GetDirectoryName(dir) ?? dir)
        : dir;
}
```

---

## 修正後の検証結果

### プロジェクトフォルダ構造

```
20260527_155810_操作手順書_2026_05_27_15_58.ascproj/
├── events_2026-05-27.log       ← プロジェクトルートに正しく配置 ✅
├── events_2026-05-27.jsonl     ← プロジェクトルートに正しく配置 ✅
├── images/                     ← スクリーンショット 13枚 ✅
├── thumbs/                     ← サムネイル step_001.jpg〜step_013.jpg ✅
└── project.json                ← .tmp ファイルなし ✅
```

### project.json 内容確認

```json
{
  "steps": [
    {
      "stepNumber": 1,
      "imagePath": "images/20260527_155925_584_windowchange_monitor1.png",
      "thumbPath": "thumbs/step_001.jpg"
    }
  ]
}
```

- `imagePath`: フォワードスラッシュ ✅
- `thumbPath`: フォワードスラッシュ（修正済み） ✅

### エラーログ確認

修正後のセッション（15:58以降）でエラー・警告ゼロ ✅

---

## 修正対象ファイル

| ファイル | 変更内容 |
|--------|---------|
| `Services/ManualSessionRecorder.cs` | `thumbRelPath` の `Path.Combine` → `$"thumbs/{...}"` |
| `Services/MetadataLogger.cs` | `ResolveLogDir` メソッド追加、`images/` 配下の場合は親ディレクトリを使用 |
| `Services/ProjectStore.cs` | `catch` ブロックで `.tmp` クリーンアップを追加 |
