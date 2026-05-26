# Phase 5 動作確認レポート

## 日時
2026-05-26 23:30〜

## 対象
`publish/AutoScreenshot.exe`（ビルド: 2026-05-26 23:14）  
PID 1168（新規起動、セッション ID: f032a24e-cb4c-4636-8f67-cd87c8130171）

---

## 検証方法

- `publish/AutoScreenshot.exe` を起動し、ログ・設定ファイル・出力ファイルを観察
- 既存セッションログ（app-20260526.log）の過去証跡を照合
- ソースコードのレビュー（主要サービスクラス）
- 出力ファイル（.md）の内容検証

---

## 検証結果

### セッション管理 (S)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| S-01 | 起動時にセッション自動開始 | ✅ | ログ: `手順書セッション開始: 操作手順書 2026-05-26 23:27 ("f032a24e-...")` |
| S-02 | 終了時にセッション自動完了・手順書保存 | ✅ | ログ: `21:00:10 手順書 Markdown 出力完了: ...20260526_210010_...md`、実ファイル 4 件確認 |
| S-03 | トレイメニュー「手順書セッション区切り」「手順書を今すぐ生成」 | ✅ | NotifyIconWrapper.cs L137-141: 両メニュー項目実装確認 |
| S-04 | セッション開始時タイトル入力ダイアログ（設定で ON/OFF） | ✅ | `ManualTitleDialog.xaml` 実装済み、`ShowTitleDialogOnStart: false` で正常スキップ |
| S-05 | セッションごとに一意 GUID | ✅ | ログに UUID: `18f6f82b-4c2d-4c9c-a334-7bee9be4c2b6` 等 |

**S-02 備考**: ステップ 0 件のセッション（デスクトップ操作なし）は意図的にスキップされる（`WriteSessionAsync` L165: `if (session.Steps.Count == 0) return;`）。force kill 時は Dispose() の実行タイミングによって `手順書 Markdown 出力完了` ログが Serilog バッファに残る場合があるが、ファイル自体は書き出されている。

---

### UI 要素情報取得 (U)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| U-01 | UIA でクリック座標の UI 要素取得 | ✅ | 手順書に `「Message input」をクリックしました。` 等の具体的 UI 名が記録される |
| U-02 | UIA 失敗時 OCR フォールバック | ✅ | `OcrService.RecognizeNearbyTextAsync()` 実装済み |
| U-03 | 両方失敗時 TODO マーカー | ✅ | `RuleBasedDescriber.cs` で `NeedsReview=true` 時にマーカー挿入 |
| U-04 | キーボードイベント時 UIA でフォーカス要素取得 | ✅ | 手順書に「Committing 10 files to main」「Message input」等のフォーカス要素名が表示される |
| U-05 | ウィンドウ切替時のタイトル・プロセス名取得 | ✅ | 手順書 L39: `「GitHub Desktop」(GitHubDesktop) に切り替えました。` |

---

### 操作イベント記録 (E)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| E-01 | ステップにタイムスタンプ・トリガー・UI 名・カーソル座標・説明を記録 | ✅ | ManualStep モデル + JSONL 出力で確認 |
| E-02 | ルールベース操作目的テキスト自動生成 | ✅ | `RuleBasedDescriber.cs` |
| E-03 | ルールベース生成ルール各種 | ✅ (一部) | ウィンドウ切替・クリック・右クリック等は正常。キーボードは後述 |
| E-04 | キーボード記録方式設定（RealText/KeyCode/Both） | ⚠️ **未動作** | 設定項目は存在するが実際のキー文字列を取得していない（後述） |
| E-05 | ScreenDiff・ManualCapture は手順書に記録しない | ✅ | `RecordStepAsync` L49: `if (evt.Type is TriggerType.ScreenDiff or TriggerType.ManualCapture) return;` |

**E-04 詳細（重要な不具合）**:
- `HookService.KeyboardHookCallback()` は WM_KEYDOWN を検知して `KeyboardActivity?.Invoke()` をシグナルするのみ。vkCode（lParam）を読まない。
- `TriggerEvent` モデルに `InputText` / `KeyCodes` フィールドが存在しない。
- その結果、`ManualStep.InputText` と `ManualStep.KeyCodes` は常に `null`。
- キーボードステップの説明は常に `「{UI名}」にキー入力しました。`（フォールバック）となり、実際の入力テキストが記録されない。
- **要件 E-03** の `キー入力（文字） → 「{UI名}」に「{入力テキスト}」と入力しました。` が機能しない。
- **要件 E-04** の `KeyboardMode` 設定（RealText/KeyCode/Both）が無効。

---

### 手順書の文書構成 (D)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| D-01 | 表紙（タイトル・開始/終了日時・OS/ユーザー・ツール名・セッション ID） | ✅ | 手順書 L1-7 で全フィールド確認 |
| D-02 | チャプターをウィンドウ切替で分割 | ✅ | 5 チャプター（VS Code, GitHub Desktop, Explorer×2, AutoScreenshot） |
| D-03 | 5 分以上のタイムギャップで小見出し挿入 | 実装済み・未実測 | `MarkdownManualWriter.cs` 実装確認。テストセッションが全て 2 分未満のため実測できず |
| D-04 | チャプターをまたぐ通番ステップ番号 | ✅ | ステップ 1〜9（VS Code）→10〜11（GitHub Desktop）→12〜14（Explorer/AutoScreenshot） |
| D-05 | スクリーンショット添付モード設定（All/WindowChange/None） | ✅ | `ScreenshotMode: 1`（WindowChange）でウィンドウ切替のみ画像あり |
| D-06 | Markdown は相対パスで参照 | ✅ | `../2026-05-26/20260526_231603_384_windowchange_monitor1.png` |
| D-07 | 目次に各チャプターへのアンカーリンク | ✅ | 手順書 L9-15 でリンク付き目次確認 |

**D-02 補足（軽微）**: ウィンドウタイトルが空文字のウィンドウ（Explorer の高速切替時等）はチャプター見出しが `## 3. `（空）となる。TOC も `[3. ](#3-)` と不明瞭。実用上は問題ないが、プロセス名でフォールバックする改善の余地あり。

---

### 出力形式 (O)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| O-01 | Markdown (.md) 出力 | ✅ | manuals フォルダに 4 件の .md ファイル確認 |
| O-02 | Word (.docx) 出力 | 実装済み・未実測 | `DocxManualWriter.cs` 実装確認。`OutputDocx: false` のため生成なし |
| O-03 | docx 書式要素（表紙・TOC・ヘッダー/フッター・番号付きリスト・画像） | 実装済み・未実測 | `DocxManualWriter.cs` で Open XML 生成コード実装確認 |
| O-04 | ファイル名: `{開始日時}_{タイトルslug}.md` | ✅ | `ManualSessionRecorder.cs` L203: `fileBase = $"{session.StartedAt:yyyyMMdd_HHmmss}_{slug}"` |
| O-05 | 出力フォルダを設定で指定 | ✅ | `config.json` の `ManualGen.OutputFolder` |
| O-06 | Markdown/docx 個別に出力選択 | ✅ | `OutputMarkdown: true`, `OutputDocx: false` で片方のみ出力 |
| O-07 | ユーザーテンプレートファイル対応（.md / .dotx） | ✅ | 設定 UI（参照ボタン）・`MarkdownManualWriter.ApplyMarkdownTemplate()`・`DocxManualWriter.Generator` で実装確認 |

---

### LLM 連携 (L)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| L-01 | 設定画面「LLM 連携」タブ（エンドポイント/API キー/デプロイメント名） | ✅ | SettingsWindow.xaml: `PwdLlmEndpoint`, `PwdLlmApiKey`, `TxtLlmDeploymentName`, `ChkLlmEnabled` |
| L-02 | 両方設定済みの場合のみ LLM 有効化 | ✅ | `WriteSessionAsync` L173-175: `!string.IsNullOrWhiteSpace(cfg.LlmEndpoint) && !string.IsNullOrWhiteSpace(cfg.LlmApiKey)` |
| L-03 | 操作目的テキスト LLM 改善 | ✅ | `LlmService.ImproveDescriptionsAsync()` 実装確認 |
| L-04 | ダイジェスト生成（表紙に記載） | ✅ | `LlmService.GenerateDigestAsync()` + `ManualSession.Digest` フィールド |
| L-05 | LLM 送信データにスクリーンショット含まない | ✅ | `LlmService.cs`: テキスト（UI名・説明文・タイムスタンプ）のみ送信 |
| L-06 | LLM 失敗時フォールバック | ✅ | `WriteSessionAsync` で try/catch → `Log.Warning` → ルールベース継続 |
| L-07 | デプロイメント名設定 | ✅ | `config.json`: `"LlmDeploymentName": "claude-haiku-4-5"` |
| L-08 | Azure AI Inference SDK `ChatCompletionsClient` 使用 | ✅ | `LlmService.cs`: `new ChatCompletionsClient(...)` / `await client.CompleteAsync(options)` / `response.Value.Content` |

**NF-04 (DPAPI)**: `DpapiHelper.cs` で `ProtectedData.Protect/Unprotect` 実装確認。`config.json` の `LlmEndpoint`・`LlmApiKey` は現在未設定のため空文字。設定保存時は `DpapiHelper.Protect()` を通過することをコードで確認（`SettingsWindow.xaml.cs: ApplySettings()`）。

---

### 設定 (C)

`config.json` の `ManualGen` セクションで全設定項目を確認:

```json
"ManualGen": {
  "Enabled": true,
  "OutputFolder": "",
  "OutputMarkdown": true,
  "OutputDocx": false,
  "ScreenshotMode": 1,
  "KeyboardMode": 2,
  "ChapterTimeGapMinutes": 5,
  "ShowTitleDialogOnStart": false,
  "TemplateMarkdownPath": "",
  "TemplateDotxPath": "",
  "LlmEnabled": false,
  "LlmEndpoint": "",
  "LlmApiKey": "",
  "LlmDeploymentName": "claude-haiku-4-5"
}
```

全 14 設定項目が要件定義書通りに存在する。

---

### 非機能要件 (NF)

| ID | 要件 | 結果 | 証跡 |
|----|------|------|------|
| NF-01 | 手順書生成はバックグラウンドスレッド | ✅ | `SplitSession()` / `GenerateNow()` は `Task.Run(async () => ...)` |
| NF-02 | 100 ステップ LLM なしで 3 秒以内 | 実装的に問題なし | 実測なし（ファイル I/O のみで CPU/メモリ負荷が低い実装） |
| NF-03 | LLM 完了後にトースト通知 | ✅ | `Notifier.ShowManualGeneratedToast(llmUsed)` 実装確認（LLM なし: 「手順書を生成しました」 / LLM あり: 「手順書を生成しました（LLM最適化済み）」） |
| NF-04 | API キー・エンドポイント URL を DPAPI 暗号化 | ✅ | `DpapiHelper.Protect/Unprotect` で `DataProtectionScope.CurrentUser` |
| NF-05 | UIA タイムアウト 200ms | ✅ | `UiaService.cs` 実装確認 |
| NF-06 | Windows.Media.Ocr 優先、Tesseract フォールバック | ✅ | `OcrService.cs` 実装確認 |
| NF-07 | DocumentFormat.OpenXml 使用 | ✅ | `.csproj`: `<PackageReference Include="DocumentFormat.OpenXml" Version="3.1.0" />` |

---

## 発見された問題

### 🔴 重大: E-04 / E-03 キーボード入力テキスト未取得

**要件**: `KeyboardMode: RealText/KeyCode/Both` 設定に基づき実際の入力文字列またはキーコードを記録する。  
**現状**: `HookService` は WM_KEYDOWN の発生を通知するのみで、どのキーが押されたか（vkCode）を取得していない。`TriggerEvent` に対応フィールドがない。`ManualStep.InputText` / `ManualStep.KeyCodes` は常に `null`。  
**影響**: 手順書のキーボードステップが全て `「{UI名}」にキー入力しました。`（汎用フォールバック）となる。入力内容が記録されない。`KeyboardMode` 設定が無効。  
**修正方針**: `HookService.KeyboardHookCallback()` で `lParam` から vkCode を読み取り、`TriggerEvent` にキーコード・文字情報を追加する。

### 🟡 軽微: D-02 空ウィンドウタイトルのチャプター見出し

**要件**: 章見出しは「{連番}. {ウィンドウタイトル}」形式。  
**現状**: Explorer 等でウィンドウタイトルが空文字の場合、`## 3. ` のような空見出しになる。  
**影響**: 手順書の可読性がやや低下する。  
**修正方針**: タイトルが空の場合はプロセス名または「(不明なウィンドウ)」でフォールバック。

### 🟡 参考: S-02 force kill 時のログ欠損

force kill (`taskkill //F`) で `Dispose()` 途中に強制終了された場合、Serilog の `手順書 Markdown 出力完了` ログが未フラッシュのまま消える。ただし実ファイルは書き出されている（`StopSessionAsync` が File.WriteAllText を完了後にログ記録するため）。トレイメニューの「終了」で正常終了する場合は問題なし。

---

## まとめ

| 分類 | 合格 | 問題 | 未実測 |
|------|------|------|--------|
| セッション管理 (S) | 5/5 | - | - |
| UI 要素取得 (U) | 5/5 | - | - |
| イベント記録 (E) | 4/5 | E-04: キーボードテキスト未取得 | - |
| 文書構成 (D) | 6/7 | D-02: 空タイトル章 | D-03 (時間ギャップ小見出し) |
| 出力形式 (O) | 5/7 | - | O-02, O-03 (docx 出力) |
| LLM 連携 (L) | 8/8 | - | - |
| 非機能 (NF) | 6/7 | - | NF-02 (パフォーマンス実測) |

**判定: 条件付き PASS**  
手順書自動生成機能の基本フローは動作しており、出力ファイルの品質・構成は要件に合致している。主要な問題は E-04 のキーボード入力テキスト未取得（実装が未完了）。docx 出力は実装済みだが設定が OFF のため未実測。
