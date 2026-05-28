# v1.5.0 プロジェクト機能有効無効切り替えの除去

## 概要

v1.1.0 互換のためのプロジェクト機能有効/無効切り替え（`ProjectConfig.Enabled`）を除去し、
プロジェクト機能を常に有効とする。

## 変更ファイル

| ファイル | 変更内容 |
|---|---|
| `Models/ProjectConfig.cs` | `Enabled` プロパティ削除 |
| `Views/SettingsWindow.xaml` | `ChkProjectEnabled` CheckBox と直後の Separator を削除 |
| `Views/SettingsWindow.xaml.cs` | `LoadSettings` / `ApplySettings` の `Enabled` 参照 2 行を削除 |
| `Services/NotifyIconWrapper.cs` | `Initialize` の `if (Project.Enabled)` ガード除去・`BuildContextMenu` の if/else 分岐をプロジェクト有効側に統合 |
| `Services/ManualSessionRecorder.cs` | `StartSession` / `RecordStepAsync` / `WriteSessionAsync` の `projCfg.Enabled` 条件を全除去 |

## 除去したコードの詳細

### ProjectConfig.cs
```csharp
// 削除
/// <summary>プロジェクト機能を有効にする（false = v1.1.0 以前の動作）</summary>
public bool Enabled { get; set; } = true;
```

### SettingsWindow.xaml
```xml
<!-- 削除 -->
<CheckBox x:Name="ChkProjectEnabled"
          Content="プロジェクトファイル機能を有効にする（オフにすると v1.1.0 以前の動作）"
          Margin="0,5"/>
<Separator Margin="0,8"/>
```

### NotifyIconWrapper.cs — Initialize
```csharp
// 削除した if ガード → 常に呼び出し
if (_config.Config.Project.Enabled)
    _ = SetStorageProjectFolderAsync();
```

### NotifyIconWrapper.cs — BuildContextMenu
```csharp
// 削除: bool projectEnabled = ...; if (projectEnabled) { ... } else { ... }
// 残した: if ブロックの内容（プロジェクト有効時メニュー）をフラット化
```

### ManualSessionRecorder.cs
```csharp
// StartSession
if (_projectStore != null && _config.Config.Project.Enabled)  →  if (_projectStore != null)

// RecordStepAsync
if (_projectStore != null && _config.Config.Project.Enabled && step != null)
→  if (_projectStore != null && step != null)

// WriteSessionAsync — 自動エクスポートフラグ
bool mdEnabled    = projCfg.Enabled ? projCfg.AutoExportMarkdown : cfg.OutputMarkdown;
bool docxEnabled  = projCfg.Enabled ? projCfg.AutoExportDocx     : cfg.OutputDocx;
bool videoEnabled = projCfg.Enabled ? projCfg.AutoExportVideo    : false;
bool htmlEnabled  = projCfg.Enabled && projCfg.AutoExportHtml;
// ↓
bool mdEnabled    = projCfg.AutoExportMarkdown;
bool docxEnabled  = projCfg.AutoExportDocx;
bool videoEnabled = projCfg.AutoExportVideo;
bool htmlEnabled  = projCfg.AutoExportHtml;

// WriteSessionAsync — インクリメンタル LLM
bool incrementalActive = projCfg.Enabled && projCfg.IncrementalLlm;
→  bool incrementalActive = projCfg.IncrementalLlm;

// WriteSessionAsync — 出力先フォルダ
if (projCfg.Enabled && project != null)  →  if (project != null)

// WriteSessionAsync — エクスポート記録・フォルダ自動オープン
if (projCfg.Enabled && project != null)  →  if (project != null)

// WriteSessionAsync — 動画自動生成
bool autoVideo = projCfg.Enabled ? videoEnabled : _config.Config.VideoGen.AutoGenerateWithManual;
→  bool autoVideo = videoEnabled;
```

## 注記

`ManualGenConfig.OutputMarkdown` / `OutputDocx` フィールドは手順書生成タブの設定として残存するが、
`WriteSessionAsync` での参照はなくなった（プロジェクト設定側の `AutoExportMarkdown` / `AutoExportDocx` のみ使用）。
`VideoGen.AutoGenerateWithManual` も設定としては残るが、`WriteSessionAsync` では参照されなくなった。
