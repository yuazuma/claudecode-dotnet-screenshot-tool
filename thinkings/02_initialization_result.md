# 思考メモ 02: 環境構築の結果

## 日時
2026-05-26

## 実施内容と判断記録

### .NET SDK のインストール
- `dotnet` コマンドが PATH に存在しなかった
- winget で `Microsoft.DotNet.SDK.8` (8.0.421) をインストール
- パスは `/c/Program Files/dotnet`

### NuGet ソースの設定
- デフォルトで NuGet ソースが登録されていなかった
- `dotnet nuget add source https://api.nuget.org/v3/index.json --name "nuget.org"` で追加

### ビルドエラーと解決

#### エラー 1: Application 型のあいまい性
- WPF (`System.Windows.Application`) と WinForms (`System.Windows.Forms.Application`) が衝突
- **解決**: App.xaml.cs にエイリアス `using Application = System.Windows.Application;` を追加

#### エラー 2: ImageFormat 型のあいまい性
- `AutoScreenshot.Models.ImageFormat` と `System.Drawing.Imaging.ImageFormat` が衝突
- **解決**: CaptureService.cs で `Models.ImageFormat` と明示的修飾

#### エラー 3: グローバル using が wpftmp プロジェクトに未反映
- WPF XAML コンパイル用の一時プロジェクト (`_wpftmp.csproj`) で `System.IO` の型 (`Path`, `File`, `Directory`) が未解決
- `ImplicitUsings=enable` は本プロジェクトには効いているが wpftmp プロジェクトに波及しない
- **解決**: `GlobalUsings.cs` に `global using System.IO;` 等を明示的に宣言

### 最終的なビルド結果
```
ビルドに成功しました。
    0 個の警告
    0 エラー
```

## 作成したファイル一覧
```
src/AutoScreenshot/
├── AutoScreenshot.csproj    (UseWPF + UseWindowsForms + Serilog)
├── app.manifest             (Per-Monitor DPI V2, asInvoker)
├── GlobalUsings.cs          (global using System.IO 等)
├── App.xaml                 (StartupUri 削除)
├── App.xaml.cs              (二重起動防止, Serilog 設定)
├── MainWindow.xaml          (未使用、デフォルトのまま)
├── Models/
│   ├── AppConfig.cs         (全設定項目)
│   └── TriggerEvent.cs      (トリガーイベント型)
├── Native/
│   └── NativeMethods.cs     (Win32 P/Invoke 宣言)
├── Services/
│   ├── AutoStartService.cs  (HKCU レジストリ登録)
│   ├── CaptureService.cs    (スクリーンキャプチャ)
│   ├── ConfigStore.cs       (設定読み書き)
│   ├── DiffDetector.cs      (縮小画像差分検知)
│   ├── FileStorage.cs       (ファイル保存・命名)
│   ├── HookService.cs       (Win32 フック)
│   ├── MaskingService.cs    (UIAutomation マスキング)
│   ├── MetadataLogger.cs    (テキスト/JSON ログ)
│   ├── NotifyIconWrapper.cs (トレイアイコン管理)
│   ├── Notifier.cs          (点滅・トースト・カウンター)
│   └── TriggerOrchestrator.cs (トリガー調停)
└── Views/
    ├── SettingsWindow.xaml  (タブ形式設定UI)
    └── SettingsWindow.xaml.cs
```

## 次のステップ (Phase 1 実装)
- アプリアイコン (ICO) の作成・組み込み
- TriggerOrchestrator の実機テスト
- Phase 1 機能: タスクトレイ常駐 + 手動撮影 + PNG 保存 + 設定ファイル基盤
