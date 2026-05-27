# CHANGELOG

すべての主要な変更点をこのファイルに記録します。
[Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) の形式に準拠し、
[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

---

## [1.1.0] — 2026-05-27

### Added（追加）

- **動画自動生成機能** — 操作ステップから APNG / MP4 動画を生成
  - `Models/VideoGenConfig.cs`: 動画生成設定モデル（VideoUnit / FrameTimingMode / VideoResolution enum 含む）
  - `Services/TtsService.cs`: Windows SAPI TTS による WAV 生成
  - `Services/FrameRenderer.cs`: フレーム合成（波紋・破線矩形・テロップ帯）
  - `Services/ApngWriter.cs`: 純マネージド APNG チャンク書き込み（acTL / fcTL / fdAT / CRC32）
  - `Services/MfVideoWriter.cs`: Windows MediaFoundation P/Invoke による H.264 + AAC MP4 出力
  - `Services/VideoGenerator.cs`: 動画生成の統括・バックグラウンド実行（SemaphoreSlim(1,1) で多重実行防止）
  - 設定ウィンドウに「動画生成」タブ（9タブ目）追加（24 設定項目）
  - トレイメニューに「動画を生成」メニュー追加
  - `Notifier.ShowBalloon(title, message)` メソッド追加（動画生成の開始/完了通知）
  - `ManualSessionRecorder.SetVideoGenerator()` / `GenerateVideoNow()` メソッド追加
  - `AppConfig.VideoGen` プロパティ追加（`VideoGenConfig`）
  - `System.Speech 9.0.0` NuGet パッケージ追加

### Changed（変更）

- バージョン: 1.0.0 → 1.1.0

### Fixed（修正）

- `MfVideoWriter`: `IMFSinkWriter` COM インターフェース GUID を正しい Windows SDK IID `{3137f1cd-fe5e-4805-a5d8-fb477448cb3d}` に修正（誤った GUID では QueryInterface が `E_NOINTERFACE` で失敗し MP4 が 0 バイトになる問題）
- `FrameRenderer`: WebP 画像の PNG 変換を `img.Save(ms, new PngEncoder())` に修正（`SaveAsPng()` 拡張メソッドが名前空間競合で解決できない問題）
- `ApngWriter`: 未使用フィールド `_disposed` によるコンパイル警告 (CS0414) を除去

---

## [1.0.0] — 2026-05-26

### Added（追加）

- **自動スクリーンショット撮影**
  - マウス左/右/中クリック、ドラッグ、ホイール操作を自動検知して撮影
  - キーボード入力アイドル後（既定 2 秒）に撮影（Shift 対応・Backspace 補正）
  - アクティブウィンドウ切替時に撮影
  - 画面差分検知（3 秒間隔、30% 変化で発火）
  - クールダウン・除外アプリ（ワイルドカード）・一時停止で誤撮影を抑制

- **操作手順書の自動生成**
  - Markdown (.md) / Word (.docx) 形式を自動生成
  - Windows UI Automation でクリック先・入力先の UI 要素名を取得
  - UIAutomation 失敗時は Windows OCR (Windows.Media.Ocr) でフォールバック
  - アクティブウィンドウ単位でチャプター分け、時間ギャップで小見出しを自動挿入
  - Markdown テンプレート (.md) / Word テンプレート (.dotx) によるカスタマイズ対応
  - セッション分割・即時生成・終了時自動生成

- **Azure AI Foundry (Claude) LLM 連携**
  - 操作説明文を Azure AI Foundry 上の Claude で改善
  - セッション全体の操作サマリー（3〜5 行）を生成
  - API キー / エンドポイント URL を Windows DPAPI で暗号化保存（NF-04）
  - LLM 失敗時はルールベース説明文でフォールバック

- **プライバシー・セキュリティ**
  - UIAutomation でパスワード欄（IsPassword=true）を自動検知して黒塗りマスキング
  - プロセス名・ウィンドウタイトルによる除外アプリ設定
  - LLM に画像データを送信しない（テキストのみ）

- **その他**
  - PNG / JPEG / WebP 形式で保存
  - JSONL / CSV 形式の構造化サイドカーログ
  - カーソル位置オーバーレイ描画・タイムスタンプ焼き込み
  - グローバルホットキーで即座に一時停止/再開
  - ディスク残容量監視・自動一時停止
  - 管理者権限不要・.NET ランタイムのインストール不要（自己完結型）
  - Named Mutex によるシングルインスタンス保証
  - exe 配置フォルダの `config.json` を優先するポータブル運用対応

---

*[1.1.0]: https://github.com/your-org/AutoScreenshot/compare/v1.0.0...v1.1.0*
*[1.0.0]: https://github.com/your-org/AutoScreenshot/releases/tag/v1.0.0*
