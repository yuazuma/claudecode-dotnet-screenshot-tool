# Phase 7 実装ドキュメント

## 日時
2026-05-26

## 概要

配布用の自己完結型単一実行ファイルを生成する。
`dotnet publish` で `SelfContained=true` + `PublishSingleFile=true` を使用。

---

## 実装内容

### 1. .csproj にプロダクトメタデータ追加

**ファイル**: `src/AutoScreenshot/AutoScreenshot.csproj`

```xml
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
<Product>AutoScreenshot</Product>
<Company>AutoScreenshot</Company>
<Description>業務作業証跡の自動スクリーンショット撮影ツール</Description>
<Copyright>Copyright © 2026</Copyright>
<NeutralLanguage>ja-JP</NeutralLanguage>
```

`<Version>`, `<Product>`, `<Copyright>` 等は Windows エクスプローラーの「プロパティ→詳細」に反映される。

### 2. パブリッシュプロファイル作成

**ファイル**: `src/AutoScreenshot/Properties/PublishProfiles/win-x64-release.pubxml`

```xml
<Configuration>Release</Configuration>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<PublishReadyToRun>false</PublishReadyToRun>
<EnableCompressionInSingleFile>false</EnableCompressionInSingleFile>
<PublishDir>$(MSBuildProjectDirectory)\..\..\publish\</PublishDir>
```

**設計判断**:
- `SelfContained=true`: .NET 8 ランタイムを同梱。ターゲット PC に .NET がインストール不要
- `PublishSingleFile=true`: 全 DLL を単一 exe に埋め込み
- `IncludeNativeLibrariesForSelfExtract=true`: WPF ネイティブ DLL も exe に埋め込み
  （起動時に `%TEMP%` へ展開）
- `PublishReadyToRun=false`: R2R は起動高速化に有効だが、ビルド時間増加・一部環境で問題になる可能性があるため無効
- `EnableCompressionInSingleFile=false`: 圧縮はサイズ削減になるが起動時間が増加するため無効

### 3. 配布ビルドコマンド

```powershell
dotnet publish src/AutoScreenshot -p:PublishProfile=win-x64-release
```

プロファイルをコマンドラインで指定することで、開発ビルド (`dotnet run`, `dotnet build`) には
影響を与えない。

---

## 動作確認結果

### ビルド

```
dotnet publish src/AutoScreenshot -p:PublishProfile=win-x64-release
→ 0 エラー
→ publish/AutoScreenshot.exe (164,002,045 bytes ≈ 156 MB)
→ publish/AutoScreenshot.pdb (38,656 bytes)
```

### publish 版の起動確認

```log
2026-05-26 18:12:14 [INF] AutoScreenshot 起動
2026-05-26 18:12:14 [INF] 設定ファイルを読み込みました: ...config.json
2026-05-26 18:12:14 [INF] HookService: フック設置成功 (mouse=true, keyboard=true, winEvent=true)
2026-05-26 18:12:14 [INF] スクリーンショット監視開始
```

- プロセスが起動し、WS: 67 MB でトレイに常駐
- ActiveWindowChange トリガーでスクリーンショット撮影・保存を確認
- JSONL ファイルへの書き込みも確認

### 出力ファイル

| ファイル | サイズ |
|---------|--------|
| `publish/AutoScreenshot.exe` | 156 MB (自己完結型単一 exe) |
| `publish/AutoScreenshot.pdb` | 38 KB (デバッグシンボル、配布不要) |

---

## README 作成

`README.md` をリポジトリルートに作成。  
インストール方法・使い方・設定項目・ビルド方法を記載。

---

## 既知の事象 (Phase 7 以前から存在)

`OnDiffTimer エラー: The handle is invalid`
- `DiffDetector` が `CopyFromScreen` を呼ぶ際、環境によってハンドルが無効になる
- デスクトップ操作環境（RDP 等）で発生しやすい
- マウスクリック・キーボード等のトリガーは正常動作
- 差分検知が必要な場合は別途調査

---

## 全フェーズ完了サマリ

| フェーズ | 内容 | 状態 |
|---------|------|------|
| Phase 1 | コア機能 (タスクトレイ・手動撮影・設定基盤) | ✅ |
| Phase 2 | 自動撮影 (グローバルフック) | ✅ |
| Phase 3 | 画面差分検知 | ✅ |
| Phase 4 | WebP・マスキング・BurnTimestamp | ✅ |
| Phase 5 | グローバルホットキー (一時停止/再開) | ✅ |
| Phase 6 | タイマーリーク修正・構造化ログ UI | ✅ |
| Phase 7 | 配布 (自己完結型単一 exe) | ✅ |
