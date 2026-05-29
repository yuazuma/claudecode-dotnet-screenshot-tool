# v1.5.1 自動起動パス修正

## ユーザープロンプト

```
サインイン時に旧バージョンのexeが自動実行されているようです。
"C:\Users\y\Documents\GitHub\claudecode-dotnet-screenshot-tool\src\AutoScreenshot\bin\Release\net8.0-windows10.0.17763.0\AutoScreenshot.exe"
が実行されるように修正してください。
```

---

## 調査

`AutoStartService.cs` を確認:

```csharp
public static void Enable()
{
    string exePath = Environment.ProcessPath ?? ...;
    using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
    key.SetValue(AppName, $"\"{exePath}\"");
}
```

`Enable()` は **実行中プロセスのパス** (`Environment.ProcessPath`) をそのままレジストリに書く設計。
旧 Debug ビルドの exe で自動起動が有効化されていたため、そのパスが残存していた。

## 実施内容

Python `winreg` モジュールでレジストリを直接書き換え:

```
HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
  "AutoScreenshot" → "C:\...\bin\Release\net8.0-windows10.0.17763.0\AutoScreenshot.exe"
```

| | パス |
|---|---|
| 変更前 | `...\bin\Debug\net8.0-windows\AutoScreenshot.exe` |
| 変更後 | `...\bin\Release\net8.0-windows10.0.17763.0\AutoScreenshot.exe` |

## 補足

今後、設定ウィンドウで「自動起動」を一度 OFF → ON にするだけで、
起動中の exe のパスに再登録できる（`Enable()` が常に現在のプロセスパスを使用するため）。
