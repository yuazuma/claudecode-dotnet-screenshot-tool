# Phase 5（残機能） 実装ドキュメント

## 日時
2026-05-26

## 実装内容

### S-04: タイトル入力ダイアログ

**新規**: `Views/ManualTitleDialog.xaml` + `.xaml.cs`
- セッション開始時にタイトル入力を促すシンプルなダイアログウィンドウ
- `EnteredTitle` プロパティで入力値を返す。Cancel / 空欄の場合は既定タイトルが使用される

**更新**: `Services/NotifyIconWrapper.cs`
- `GetSessionTitle()` ヘルパーメソッドを追加
  - `ShowTitleDialogOnStart = true` のときのみダイアログを表示
- `Initialize()` の `StartSession()` 呼び出しを `StartSession(GetSessionTitle())` に変更
- セッション区切りメニューも `SplitSession(GetSessionTitle())` に変更

---

### O-07: ユーザーテンプレートファイル対応

**MarkdownManualWriter.cs**
- `WriteAsync()` に `templatePath` 引数を追加
- `ApplyMarkdownTemplate()` メソッドを追加:
  - テンプレートファイルを読み込み
  - `{{content}}` プレースホルダーがあれば生成コンテンツと置換
  - なければテンプレート内容の後ろに生成コンテンツを追記

**DocxManualWriter.cs**
- `WriteAsync()` に `templateDotxPath` 引数を追加
- `Generator` コンストラクタに `templateDotxPath` 追加
- `Run()` 内でテンプレート有無を判定:
  - テンプレートあり: `.dotx` を出力パスにコピー → `Open()` → `ChangeDocumentType()` → Body をクリア → コンテンツ追記（テンプレートのスタイルを保持）
  - テンプレートなし: 従来どおり `Create()` + `DefineStyles()`
  - テンプレート読み込み失敗時: `Log.Warning` してフォールバック（通常作成）

**SettingsWindow.xaml**
- 「手順書生成」タブにテンプレートファイル選択欄を追加:
  - `TxtTemplateMarkdownPath` + 「参照...」ボタン
  - `TxtTemplateDotxPath` + 「参照...」ボタン

**SettingsWindow.xaml.cs**
- `LoadSettings()` / `ApplySettings()` にテンプレートパスの読み書き追加
- `BtnBrowseMarkdownTemplate_Click()` — .md ファイル選択ダイアログ
- `BtnBrowseDotxTemplate_Click()` — .dotx ファイル選択ダイアログ

---

### NF-03: LLM完了トースト通知

**Notifier.cs**
- `ShowManualGeneratedToast(bool llmUsed = false)` メソッドを追加
  - `llmUsed = false`: 「手順書を生成しました」
  - `llmUsed = true`: 「手順書を生成しました（LLM最適化済み）」

**ManualSessionRecorder.cs**
- コンストラクタに `Notifier? notifier = null` を追加
- `WriteSessionAsync()` 内で手順書ファイル書き出し成功後に `_notifier?.ShowManualGeneratedToast(llmUsed)` を呼び出す

**NotifyIconWrapper.cs**
- `ManualSessionRecorder` の生成時に `_notifier` を渡すよう変更

---

## ビルド・パブリッシュ結果

```
ビルド: 0 エラー, 3 警告（NU1902 ImageSharp 脆弱性のみ）
パブリッシュ: publish/AutoScreenshot.exe 生成成功
```

## トラブルシューティング

- XAML で `{{content}}` と書くと WPF パーサーが `{` を MarkupExtension として解釈してビルドエラーになる
  → `{}{content}` と書くことでリテラル `{content}` として扱われる
