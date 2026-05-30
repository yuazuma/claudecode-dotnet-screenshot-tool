# CHANGELOG

すべての主要な変更点をこのファイルに記録します。
[Keep a Changelog](https://keepachangelog.com/ja/1.0.0/) の形式に準拠し、
[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

---

## [1.7.0] — 2026-05-30

### Added（追加）

- **before 画像フォーマットを after 画像と統一（FR-H1）**
  - before 画像を PNG 固定から、設定の画像フォーマット（PNG/JPEG/WebP）と揃えるよう変更
  - JPEG: quality = 100（無圧縮明示）、WebP: lossless モードで保存

- **after 画像もロスレス保存に変更（FR-H2）**
  - キャプチャした after 画像もロスレスで保存（JPEG quality=100、WebP lossless）
  - サムネイル生成は変更なし（プレビュー用途のため引き続き JpegQuality 設定値を使用）

- **保存先フォルダの階層構造テンプレート設定（FR-H3）**
  - `{ベースフォルダ}/{テンプレート評価フォルダ}/{個々のファイル}` の3階層構成に対応
  - テンプレートで使用可能なトークン:
    - 日時短縮形: `{date}` `{datetime}` `{date_time}` `{date-time}` `{hour}`
    - セッション情報: `{title}` `{title_short}` `{id}`
    - 環境情報: `{username}` `{computername}`
    - .NET 書式指定子: `yyyy` `MM` `dd` `HH` `mm` `ss` 等
  - 画像・手順書・動画それぞれ独立したベースフォルダ + テンプレート設定（既定値: `C:\Users\{username}\Pictures\AutoScreenshot`）
  - プロジェクトフォルダ名（`.ascproj`）もテンプレート化（既定: `{date_time}_{title_short}.ascproj`）
  - 旧 `SaveFolder`・`FolderNaming` 設定は自動移行

- **「パス設定」タブを設定ウィンドウに追加（FR-H4）**
  - 「一般」タブの直後に専用タブを新設
  - 画像・手順書・動画のベースフォルダ・テンプレート・第2ベースフォルダを一画面で設定可能
  - 各テンプレート入力欄に [ℹ] アイコンを配置、ホバーでプレースホルダー一覧と使用例をツールヒント表示
  - 「保存」「手順書生成」「動画生成」タブからパス関連項目を移動

- **第2ベースフォルダへのフォールバック保存（FR-H5）**
  - 画像・手順書・動画それぞれ独立した第2ベースフォルダを設定可能（空欄 = 無効）
  - アプリ起動時: 第1ベースフォルダのドライブ存在確認と書き込みテストを実行
  - 撮影中: `IOException`/`UnauthorizedAccessException` 発生時に即座に第2フォルダへ切り替え
  - 切り替え時はバルーン通知でユーザーに知らせる
  - セッション終了まで第2フォルダを維持、次回起動時に再判定

- **エクスポート進捗ダイアログ（FR-H6）**
  - 手順書・動画・画像・ZIP のエクスポート処理中にノンモーダルの進捗ウィンドウを表示
  - 確定プログレスバー（ステップ数/フレーム数）または不確定モード（ZIP）
  - キャンセルボタンで処理を中断可能（×ボタンでも同等）
  - 完了時は自動でウィンドウを閉じる
  - エクスポート中はトレイアイコンのツールヒントにも進捗を表示
    （例: `AutoScreenshot - 本日 42 枚撮影 / MD 60/125 生成中...`）

### Changed（変更）

- バージョン: 1.6.1 → 1.7.0
- 「保存」タブ: 保存先フォルダ・フォルダ命名規則の設定項目を「パス設定」タブへ移動
- 「手順書生成」タブ: 出力先フォルダを「パス設定」タブへ移動
- 「動画生成」タブ: 出力フォルダ・完了時フォルダを開く設定を「パス設定」タブへ移動

---

## [1.6.1] — 2026-05-29

### Fixed（修正）

- **LLM 呼び出し形式を Anthropic Messages API に変更**
  - `Azure.AI.Inference` SDK（v1.0.0-beta.5）が送信する `api-version=2024-05-01-preview`
    をエンドポイントが拒否し LLM 連携が全件失敗していた問題を修正
  - `Azure.AI.Inference.ChatCompletionsClient` から `HttpClient` による直接呼び出しに変更
  - エンドポイント: `{設定の base URL}/anthropic/v1/messages`
  - 認証: `Authorization: Bearer {key}` ヘッダー
  - バージョン: `anthropic-version: 2023-06-01` ヘッダー
  - リクエスト形式: Anthropic Messages API（`system` はトップレベル、`Content-Length` 必須）
  - DPAPI 暗号化・エンドポイント・API キーの保存形式は変更なし（NF-04 維持）

- **設定ウィンドウ: エンドポイント URL 入力欄をマスク解除**
  - 「Microsoft Azure AI Foundry エンドポイント URL」の入力欄を `PasswordBox`（マスク表示）から
    `TextBox`（平文表示）に変更
  - URL は視認・コピーが容易になった
  - config.json への保存は引き続き DPAPI で暗号化（NF-04 変更なし）

- **before 画像フォールバック**
  - `BeforeImagePath` が未取得のステップで、エクスポート時に before 欄が空白になっていた問題を修正
  - before が未取得の場合、素の after 画像（アノテーション焼き込み前）を before として使用する
  - 適用箇所: Markdown / Word / HTML エクスポート、画像エクスポート、ProjectViewWindow 表示

### Changed（変更）

- バージョン: 1.6.0 → 1.6.1

---

## [1.6.0] — 2026-05-29

### Added（追加）

- **操作前後スクリーンショット分離（FR-F）**
  - マウスボタン押下（DOWN）時に操作前 (before) スクリーンショットを PNG で自動取得
  - マウスボタン離放（UP）後 `PostClickDelayMs`（既定 250ms）遅延してから操作後 (after) を撮影
  - キーボードは新シーケンス開始時に before を取得、アイドル後に after を取得（既存動作）
  - before 画像はプロジェクト内 `images/before/` に PNG 固定で保存（劣化なし・証跡用途）
  - before 画像にはアノテーション・カーソルオーバーレイ・タイムスタンプ焼き込みを適用しない
  - before サムネイルを `thumbs/before/step_NNN_before.jpg` に自動生成
  - Markdown・Word・HTML エクスポートで before → after の順に 2 枚出力
  - HTML では before/after を横並び表示（`<figure>` タグで「操作前」「操作後」キャプション付き）
  - 画像エクスポート（FR-PJ04）で before を `exports/images/before/` サブフォルダにコピー
  - ProjectViewWindow ステップ詳細エリアに before 画像表示エリアを追加（読み取り専用）

- **設定項目追加**（「撮影トリガー」タブ）
  - `CaptureBeforeImage`（bool, 既定: オン）— 操作前スクリーンショット取得の有効/無効
  - `PostClickDelayMs`（int, 既定: 250ms）— 操作後撮影の遅延時間（50〜2000ms 推奨）

### Changed（変更）

- バージョン: 1.5.1 → 1.6.0
- **データモデル統一**（命名規則整理）
  - `ProjectStep.ImagePath` → `AfterImagePath`（JSON フィールド名: `afterImagePath`）
  - `ProjectStep.ThumbPath` → `AfterThumbPath`（JSON フィールド名: `afterThumbPath`）
  - `ManualStep.ImagePath` → `AfterImagePath`
  - 旧 `project.json`（`imagePath` / `thumbPath`）はロード時に自動移行（書き込み時に新形式へ）
- 右クリック・中クリックのイベント発火タイミングを `WM_RBUTTONDOWN`（即時）→ `WM_RBUTTONUP` + 遅延に変更
  （before 分離のための仕様変更）
- `VideoGenerator` / `FrameRenderer` は `AfterImagePath`（操作後画像）のみを動画フレームに使用

### Fixed（修正）

- **キーボード before 画像の期限切れ問題**
  - 長時間入力時（5 秒超）に before 画像が期限切れで破棄される問題を修正
  - Keyboard の有効期限を一律 5 秒から `KeyboardIdleSeconds + 10 秒` に変更（既定 12 秒）

---

## [1.5.1] — 2026-05-28

### Fixed（修正）

- **`ExportService`: 未使用の `BuildSession()` メソッドを削除**（デッドコード除去）
  - `BuildAnnotatedSession()` に完全置き換え済みだったが旧メソッドが残存していた
- **`VideoGenerator`: `Where(s => ... || true)` デッドコードを除去**
  - 条件が常に `true` のため `.Where()` 全体が無意味だった。`.ToList()` に簡略化
- **`ProjectViewWindow`: ステップ追加時の挿入位置バグを修正**
  - `BtnAddStep_Click` がビューインデックス（`_selectedStepIndex`）を
    モデルリスト（`_selectedProject.Steps`）のインデックスとして誤用していた
  - 削除済みステップが存在すると挿入位置とステップ番号が両方ズレる問題を修正
  - `StepNumber` ベースの挿入位置計算（`FindLastIndex`）に変更
- **`ProjectViewWindow`: `Loaded` イベントハンドラーの未補足例外クラッシュを修正**
  - `async void` ラムダから `RefreshProjectListAsync` の例外が伝播するとアプリがクラッシュしていた
  - try-catch でラップし、失敗時はステータスバーにエラーメッセージを表示

### Changed（変更）

- バージョン: 1.5.0 → 1.5.1

---

## [1.5.0] — 2026-05-28

### Removed（削除）

- **`ProjectConfig.Enabled` 切り替え機能の除去**（v1.1.0 互換フラグ廃止）
  - `Models/ProjectConfig.cs`: `Enabled` プロパティ削除
  - `Views/SettingsWindow.xaml`: `ChkProjectEnabled` チェックボックスと直後の Separator を削除
  - `Views/SettingsWindow.xaml.cs`: `LoadSettings` / `ApplySettings` の `Enabled` 参照 2 行削除
  - `Services/NotifyIconWrapper.cs`: `Initialize` の `if (Project.Enabled)` ガード除去・`BuildContextMenu` の if/else 分岐をフラット化（プロジェクト有効側に統合）
  - `Services/ManualSessionRecorder.cs`: `StartSession` / `RecordStepAsync` / `WriteSessionAsync` の `projCfg.Enabled` 条件をすべて除去

### Changed（変更）

- バージョン: 1.4.0 → 1.5.0
- プロジェクト機能が常に有効（v1.1.0 以前の動作へのフォールバックを廃止）
- `WriteSessionAsync` のエクスポートフラグが常に `ProjectConfig` 側の値を参照
  - `mdEnabled`, `docxEnabled`, `videoEnabled`, `htmlEnabled`, `incrementalActive` から三項演算子除去
  - `autoVideo` が `projCfg.AutoExportVideo` のみを参照（`VideoGen.AutoGenerateWithManual` 参照廃止）
- 既存の `config.json` 互換性: `"Enabled"` フィールドは JSON デシリアライズ時に黙って無視されるため移行不要

---

## [1.4.0] — 2026-05-28

> 詳細要件: `requirements/追加要件_1.4.0.md`（作成予定）

### Added（追加）

- **タスクトレイアイコン 5 状態表示**
  - `Models/IconState.cs`: Recording（青）/ Paused（グレー）/ Captured（緑）/ Processing（オレンジ）/ Error（赤）の 5 状態 enum
  - `Resources/IconFactory.cs`: 5 色対応、Processing アイコンに右下白ドット追加
  - `Services/Notifier.cs`: アイコン状態機械に全面再設計（BeginProcessing / EndProcessing / ShowError / OnCaptured / SetBaseState）
  - 撮影成功後 200ms 緑フラッシュ、エクスポートエラー後 5 秒赤表示して自動復帰
  - 複数エクスポート並走時も `_processingCount`（Interlocked）で正確に Processing 状態を維持

### Changed（変更）

- バージョン: 1.3.0 → 1.4.0
- **トレイメニュー再構成**（`NotifyIconWrapper.cs`）
  - プロジェクト機能有効時: プロジェクト名表示 / 一時停止・新しいプロジェクト / 今すぐ撮影・キャプチャ履歴 / エクスポートサブメニュー・プロジェクトを管理 / 保存フォルダ・設定 / バージョン・終了
  - プロジェクト機能無効時: 一時停止 / 今すぐ撮影・履歴 / セッション区切り・手順書生成・動画生成 / 保存フォルダ・設定 / バージョン・終了
- **設定ウィンドウ タブ順序変更**（`SettingsWindow.xaml`）
  - 旧 7〜10 タブ（手順書生成 / LLM連携 / 動画生成 / プロジェクト）を 5〜8 位に前出し
  - 旧 5〜6 タブ（メタデータ / 通知）を 9〜10 位（末尾）に移動
  - グループ化: キャプチャ設定（1〜4）→ 出力・生成（5〜8）→ 補助・詳細（9〜10）
- **プロジェクトビューウィンドウ UI リファクタリング**（`ProjectViewWindow.xaml` / `.cs`）
  - TabControl を廃止し、ステップ一覧（左）＋ステップ詳細（右）の常時表示分割レイアウトに変更
  - ステップ一覧を横並びサムネイルグリッドから縦型リスト（☰ドラッグハンドル + 64×48 サムネイル + 説明文）に変更
  - エクスポートセクションをウィンドウ上部の「▼ エクスポート」ドロップダウンボタン（ContextMenu）に集約
  - アノテーションツールを折りたたみ Expander に変更（既存アノテーションがある場合は自動展開）
  - ステータスバーに不確定 ProgressBar 追加（エクスポート処理中に表示）
  - `ChkShowDeleted`「削除済みを表示」チェックボックス追加
  - インデックスバグ修正: フィルタ後の `_stepVms[i].Step` を正しく参照
- `ExportService`: 各エクスポートメソッドで `BeginProcessing()` / `EndProcessing()` / `ShowError()` を呼び出し（アイコン状態と連動）

---

## [1.3.0] — 2026-05-27

> 詳細要件: `requirements/追加要件_1.3.0.md`

### Added（追加）

- **ステップアノテーション機能（FR-C）**
  - `Models/AnnotationItem.cs`: アノテーション種別（Number / Arrow / Rect / Callout）・座標・ラベル・色モデル
  - `Services/AnnotationRenderer.cs`: System.Drawing で番号バッジ / 矢印 / 矩形 / 吹き出しを元画像に焼き込み
  - ProjectViewWindow にアノテーションパネル追加（ツール選択・色選択・Canvas オーバーレイ描画・保存）
  - エクスポート（Markdown / Word / HTML / 動画）でアノテーション焼き込み済み画像を使用
  - `ProjectStep.Annotations` フィールド追加（`List<AnnotationItem>?`、JSON に保存）

- **プロジェクト管理強化（FR-D）**
  - プロジェクト一覧の全文検索（タイトル・説明文・タグ）
  - タグフィルタ（WrapPanel のトグルボタン）
  - `ProjectInfo.Tags` フィールド追加
  - LstSteps ドラッグ&ドロップによるステップ並び替え（StepNumber 自動付け直し）
  - 「＋ステップを追加」ボタンで手動ステップ挿入（画像ファイル選択・説明文入力）
  - `CreatedAtDisplay` 計算プロパティ（`[JsonIgnore]`）でプロジェクト一覧表示改善

- **プロジェクト結合・分割機能（FR-E）**
  - `ProjectStore.MergeProjectsAsync()`: 複数プロジェクトを時系列順に結合（元プロジェクト変更なし）
  - `ProjectStore.SplitProjectAsync()`: 指定ステップ番号で 2 プロジェクトに分割（元プロジェクト変更なし）
  - ProjectViewWindow に「結合...」ボタン（2件以上選択時に有効化）・「ここで分割」ボタン追加
  - `CopyImageFile()` / `CloneStep()` ヘルパーで画像・ステップを安全にコピー

- **HTML エクスポート機能（FR-A）**
  - `Services/HtmlManualWriter.cs`: 単一 HTML ファイルへの手順書生成（Base64 埋め込み画像対応）
  - 設定ウィンドウの「記録停止時に HTML を自動エクスポート」チェックボックス
  - ProjectViewWindow エクスポートセクションに「手順書 (HTML)」ボタン追加

- **インクリメンタル LLM 処理（FR-B）**
  - `Services/ManualSessionRecorder.cs`: ステップ追記後に非同期で LLM 説明文改善をキュー処理
  - 設定ウィンドウの「ステップ追記後に自動 LLM 改善」チェックボックス
  - LLM 失敗時は `DescriptionRuleBased` にフォールバック（既存動作維持）

### Changed（変更）

- バージョン: 1.2.0 → 1.3.0
- `ExportService`: 各エクスポートで `BuildAnnotatedSession()` を使用し、アノテーション焼き込み済み一時 PNG を生成・クリーンアップ
- ProjectViewWindow ウィンドウサイズ: 900×600 → 1040×640（アノテーションパネル追加に伴う拡張）

---

## [1.2.0] — 2026-05-27

> 詳細要件: `requirements/追加要件_プロジェクトファイル.md`

### Added（追加）

- **プロジェクトファイル機能** — ManualSession 単位の `.ascproj/` フォルダによる記録管理
  - `Models/ProjectConfig.cs`: プロジェクト機能設定モデル（有効化・サムネイルサイズ・自動エクスポート設定）
  - `Models/ProjectInfo.cs`: project.json のデシリアライズ対象クラス（ProjectId / Steps / ExportHistory 等）
  - `Services/ProjectStore.cs`: プロジェクトフォルダの作成・project.json 読み書き・一覧取得
  - `Services/ThumbnailService.cs`: サムネイル生成（JPEG・最大 320px・非同期）
  - `Services/ExportService.cs`: エクスポート統括（画像 / 手順書 / 動画 / ZIP を一元管理）
  - `Views/ProjectViewWindow.xaml`: プロジェクトビューウィンドウ（900×600px・リサイズ可能）
  - `AppConfig.Project` プロパティ追加（`ProjectConfig`）
  - 設定ウィンドウに「プロジェクト」タブ追加（10タブ目）
  - トレイメニューを再構成:「プロジェクト区切り」「エクスポート >（サブメニュー）」「プロジェクトを管理...」

- **エクスポート機能**
  - 個別画像エクスポート（PNG / JPEG）
  - 手順書エクスポート（Markdown / Word）
  - 動画エクスポート（APNG / MP4）—バックグラウンド実行
  - ZIP アーカイブエクスポート（ファイル保存ダイアログ）

- **プロジェクト内ステップ編集**
  - ステップ削除（`images/_deleted/` に移動・物理削除なし）
  - 説明文の手修正（`descriptionOverride` フィールド）

### Changed（変更）

- バージョン: 1.1.0 → 1.2.0
- `FileStorage`: 画像保存先を `{SaveFolder}/{date}/` から `{project}/images/` に変更
- `ManualSessionRecorder`: ProjectStore と連携してプロジェクトを自動作成・更新
- `NotifyIconWrapper`: トレイメニューを v1.2.0 構成に変更
- `MarkdownManualWriter` / `DocxManualWriter` / `VideoGenerator`: 入力ソースを ManualSession → ProjectInfo に対応

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

*[1.2.0]: https://github.com/your-org/AutoScreenshot/compare/v1.1.0...v1.2.0*
*[1.1.0]: https://github.com/your-org/AutoScreenshot/compare/v1.0.0...v1.1.0*
*[1.0.0]: https://github.com/your-org/AutoScreenshot/releases/tag/v1.0.0*
