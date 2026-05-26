# v1.0.0 リリースパッケージ作成

## 日時
2026-05-27

## 生成ファイル

| ファイル | サイズ | 説明 |
|---|---|---|
| `releases/AutoScreenshot-v1.0.0-win-x64.zip` | 82 MB | GitHub Releases 配布 ZIP |
| `releases/AutoScreenshot-v1.0.0-win-x64.zip.sha256` | - | SHA-256 チェックサム |
| `releases/README.txt` | - | ZIP 内同梱 README |

## ZIP 収録ファイル

| ファイル | 非圧縮 | 圧縮後 | 用途 |
|---|---|---|---|
| AutoScreenshot.exe | 187 MB | 74 MB | メイン実行ファイル（自己完結・.NET 8 内包） |
| AutoScreenshot.dll | 0.1 MB | - | managed アセンブリ（参照用） |
| AutoScreenshot.runtimeconfig.json | - | - | ランタイムホスト設定 |
| AutoScreenshot.deps.json | - | - | 依存関係マニフェスト |
| Microsoft.Windows.SDK.NET.dll | 22.5 MB | 5.8 MB | WinRT 投影（OcrService 用） |
| DocumentFormat.OpenXml.dll | 6.1 MB | 1.5 MB | Word docx 生成 |
| DocumentFormat.OpenXml.Framework.dll | 0.4 MB | - | OpenXml フレームワーク |
| SixLabors.ImageSharp.dll | 2.0 MB | - | WebP エンコード |
| WinRT.Runtime.dll | 0.5 MB | - | WinRT ランタイム |
| Serilog.dll / .Sinks.*.dll | 0.3 MB | - | ロギング |
| README.txt | - | - | ユーザー向けインストール手順 |

## ビルド設定

```
dotnet publish -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true
  -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=false
```

WPF ネイティブ DLL（wpfgfx_cor3.dll 等）は `IncludeNativeLibrariesForSelfExtract=true` により exe に内包済み。

## SHA-256

```
e008d0a9c84ff0b74cdc08b01a5a4b5ad757034ee06ce3499610b391dcd7e1ba  AutoScreenshot-v1.0.0-win-x64.zip
```

## GitHub Releases 手順

1. GitHub の Releases ページ → "Create a new release"
2. Tag: `v1.0.0`、Title: `AutoScreenshot v1.0.0`
3. `releases/AutoScreenshot-v1.0.0-win-x64.zip` をアップロード
4. `releases/AutoScreenshot-v1.0.0-win-x64.zip.sha256` をアップロード
5. リリースノートを記載して公開
