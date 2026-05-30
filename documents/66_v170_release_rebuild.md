# v1.7.0 リリース再ビルド記録

## 背景

動作確認（verify）で発見されたバグ6件を修正後、バージョン番号を変えずに再ビルドして
releases/ を更新した。

## 修正した不具合（修正内容の詳細は documents/62_v170_impl.md 参照）

| # | ファイル | 修正内容 |
|---|---|---|
| 1 | `ManualSessionRecorder.cs` | 動画生成タスクを保持し StopSessionAsync で最大 5 分 await |
| 2 | `App.xaml.cs`（連動） | タスク await により Log.CloseAndFlush() 前にエラーが記録される |
| 3 | `FolderTemplateService.cs` | `{title}` 等の展開値を \x02\x03 でエスケープし DateTime 正規表現の誤置換を防止 |
| 4 | `VideoGenerator.cs` | `mp4Path2` デッドコード削除、`mp4Path` を直接参照 |
| 5 | `NotifyIconWrapper.cs` | `CheckDrivesAndActivateFallback()` に try-catch 追加 |
| 6 | `ProjectViewWindow.xaml.cs` | `win.IsLoaded` チェックでウィンドウクローズ後の競合を防止 |

## ビルド・パッケージ結果

```
dotnet publish -c Release -r win-x64 --self-contained true
→ 0 エラー / 3 警告（SixLabors.ImageSharp NU1902、既存）
```

| ファイル | サイズ |
|---|---|
| `releases/AutoScreenshot-v1.7.0-win-x64.zip` | 79,009,447 bytes |

**SHA-256**: `9311fdc184da729b6022512cb5ce31c10700656cb4f7abfb9a0d6103263dc129`

## 旧ビルドとの差分

| 項目 | 初版（62e51d22） | 再ビルド（9311fdc1） |
|---|---|---|
| バージョン | 1.7.0 | 1.7.0（変更なし） |
| ZIP サイズ | 79,008,966 bytes | 79,009,447 bytes |
| 主な差分 | — | バグ修正 6 件 |
