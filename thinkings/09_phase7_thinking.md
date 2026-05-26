# 思考メモ 09: Phase 7 実装の思考過程

## 日時
2026-05-26

---

## 設計判断

### SelfContained vs Framework-Dependent

**SelfContained=true (採用)**:
- .NET 8 ランタイムを exe に同梱
- ターゲット PC に .NET がインストールされていなくても動作
- exe サイズが ~156 MB に増加 (ランタイム込み)
- 一般ユーザー配布に適している

**Framework-Dependent (不採用)**:
- exe サイズは ~5-10 MB
- 実行環境に .NET 8 ランタイムが必要
- 技術的専門知識のないユーザーには不向き

→ 要件定義書の「技術的専門知識を持たない一般PC利用者」に合わせて SelfContained を選択

### PublishSingleFile と WPF ネイティブ DLL

WPF は `PresentationNative_cor3.dll`, `vcruntime140_cor3.dll` などのネイティブ DLL を使用する。
`PublishSingleFile=true` 単独では、これらは exe の隣に配置される（完全な単一ファイルにならない）。

`IncludeNativeLibrariesForSelfExtract=true` を加えると:
- ネイティブ DLL も exe に埋め込まれる
- 起動時に `%TEMP%\...\AutoScreenshot` へ自動展開される
- 2回目以降の起動では展開をスキップ（ハッシュ確認）
- 結果として exe 1ファイルのみで配布可能

### PublishReadyToRun について

`PublishReadyToRun=true` は JIT コンパイル結果を事前に格納して起動を高速化する。
タスクトレイ常駐アプリは「起動したら長時間動き続ける」用途のため、
起動時間よりも実行時のメモリ効率が重要。また R2R は環境差異でビルド失敗することがある。
→ 不採用

### EnableCompressionInSingleFile について

`EnableCompressionInSingleFile=true` にするとファイルサイズが削減できる（~60-80% 程度）。
しかし起動時に展開処理が入り起動時間が増加する。
長時間常駐アプリは初回起動時間より安定動作が優先なので無効のままとした。

### パブリッシュプロファイルを使う理由

`.csproj` に `<RuntimeIdentifier>`, `<SelfContained>`, `<PublishSingleFile>` を直接書いた場合:
- `dotnet run` も win-x64 専用ビルドになる
- クロスプラットフォーム開発ができなくなる
- `dotnet watch` の動作が変わる可能性がある

→ 開発ビルドと配布ビルドを分離するため `.pubxml` プロファイルを使用

### .pdb ファイルの扱い

`publish/AutoScreenshot.pdb` は配布不要。
スタックトレースの行番号をログに残したい場合は配布するが、
今回はログに `in .cs:line XX` が含まれているため、.pdb なしでも調査可能。
→ `.gitignore` に `publish/` を追加することを推奨

## exe サイズ内訳 (概算)

| コンポーネント | サイズ |
|------------|--------|
| .NET 8 ランタイム | ~100 MB |
| WPF フレームワーク | ~30 MB |
| WinForms フレームワーク | ~10 MB |
| アプリ本体 + ImageSharp + Serilog | ~5 MB |
| その他 (UIAutomation 等) | ~11 MB |
| **合計** | **~156 MB** |

## 全体を通じての振り返り

Phase 1 から Phase 7 まで、要件定義書の全機能を実装した。
主要な技術的挑戦:
1. WPF + WinForms ハイブリッド (NotifyIcon / NativeWindow の共存)
2. Win32 グローバルフック (WH_MOUSE_LL, WH_KEYBOARD_LL, SetWinEventHook)
3. UIAutomation によるパスワード欄マスキング
4. SixLabors.ImageSharp 3.x を使った WebP エンコード (4.x は商用ライセンス)
5. SingleFile WPF アプリの配布 (IncludeNativeLibrariesForSelfExtract が鍵)
