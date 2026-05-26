## AutoScreenshot v1.0.0

業務操作の証跡取得と操作手順書の自動作成を目的とした、
Windows タスクトレイ常駐型スクリーンショットツールです。

---

### 主な機能

#### 自動スクリーンショット撮影
- マウスクリック（左/右/中）、ドラッグ&ドロップ、ホイール操作を自動検知して撮影
- キーボード入力のアイドル後（既定2秒）に撮影
- アクティブウィンドウ切替時に撮影
- 画面差分検知（3秒間隔、30%変化で発火）
- クールダウン・除外アプリ・一時停止で誤撮影を抑制

#### 操作手順書の自動生成
- 撮影した操作イベントをもとに Markdown / Word (.docx) 形式の手順書を自動生成
- Windows UI Automation でクリック先・入力先のUI要素名（ボタン名、フィールド名等）を自動取得
- UIAutomation で取得できない場合は Windows OCR（Windows.Media.Ocr）でフォールバック
- キーボード入力の実文字列記録（Shift 対応・Backspace 補正・Ctrl/Alt コンボ対応）
- アクティブウィンドウ単位でチャプター分け、時間ギャップで小見出しを自動挿入
- Markdown テンプレート（.md）・Word テンプレート（.dotx）によるカスタマイズ対応

#### Azure AI Foundry (Claude) 連携
- Azure AI Foundry にデプロイされた Claude モデルで操作説明文を自然な日本語に改善
- セッション全体の操作サマリー（3〜5行）を生成して表紙に記載
- API キー・エンドポイント URL は Windows DPAPI で暗号化して保存（平文保存なし）
- LLM 呼び出し失敗時はルールベース説明文で継続（フォールバック）

#### プライバシー・セキュリティ
- UIAutomation でパスワード入力欄（IsPassword=true）を自動検知して黒塗りマスキング
- プロセス名・ウィンドウタイトルによる除外アプリ設定（ワイルドカード対応）
- 差分検知でもキー/マウス入力から1秒以内の変化は除外（誤撮影防止）

#### その他
- PNG / JPEG / WebP 形式で保存（WebP は SixLabors.ImageSharp 使用）
- JSONL / CSV 形式の構造化サイドカーログ
- カーソル位置オーバーレイ描画・タイムスタンプ焼き込み
- グローバルホットキーで即座に一時停止/再開
- ディスク残容量監視・自動一時停止
- 管理者権限不要、.NET ランタイムのインストール不要（自己完結型）

---

### 動作環境

| 項目 | 要件 |
|---|---|
| OS | Windows 10 バージョン 1809 以降、Windows 11 |
| アーキテクチャ | x64 |
| .NET ランタイム | 不要（同梱） |
| 管理者権限 | 不要 |

---

### インストール

1. `AutoScreenshot-v1.0.0-win-x64.zip` を任意のフォルダに展開
2. `AutoScreenshot.exe` を実行
3. タスクトレイにアイコンが表示されれば起動完了

> **初回起動時**: Windows SmartScreen の警告が表示される場合は「詳細情報」→「実行」をクリックしてください。

---

### 収録ファイル

| ファイル | 説明 |
|---|---|
| `AutoScreenshot.exe` | メイン実行ファイル（.NET 8・WPF ネイティブ DLL を内包） |
| `Microsoft.Windows.SDK.NET.dll` | OCR 機能（WinRT 投影）に必要 |
| `DocumentFormat.OpenXml.dll` 他 | Word (.docx) 出力に必要 |
| `Serilog.dll` 他 | ロギングに必要 |
| `SixLabors.ImageSharp.dll` | WebP エンコードに必要 |
| `README.txt` | インストール・アンインストール手順 |

---

### 既知の制限事項

- **WebP 形式の画像は Word (.docx) に埋め込まれません**（Open XML が WebP 非対応のため。PNG/JPEG は正常に埋め込まれます）
- **SixLabors.ImageSharp 3.1.7** に中程度の脆弱性（CVE）が報告されています。本ツールは内部でのエンコードのみに使用しており、外部からの WebP ファイルを読み込む処理はないためリスクは限定的ですが、次バージョンで更新予定です
- LLM 連携機能は **Azure AI Foundry** のエンドポイントのみ対応しています（Anthropic API への直接接続は行いません）
- OCR 機能は OS の言語パック（日本語等）がインストールされている場合のみ有効になります

---

### ファイルの整合性確認

```
SHA-256: e008d0a9c84ff0b74cdc08b01a5a4b5ad757034ee06ce3499610b391dcd7e1ba
ファイル: AutoScreenshot-v1.0.0-win-x64.zip
```

---

### ライセンス

- アプリ本体: プライベート
- [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp) v3.1.7: Apache License 2.0
- [Serilog](https://github.com/serilog/serilog): Apache License 2.0
- [DocumentFormat.OpenXml](https://github.com/dotnet/Open-XML-SDK): MIT License
- [Azure.AI.Inference](https://github.com/Azure/azure-sdk-for-net): MIT License
