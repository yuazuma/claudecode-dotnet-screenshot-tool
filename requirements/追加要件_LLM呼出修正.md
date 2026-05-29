# 追加要件定義書 — LLM 呼び出し修正・UI 修正（AutoScreenshot v1.6.1）

**文書バージョン**: 1.0  
**作成日**: 2026-05-29  
**対象バージョン**: **AutoScreenshot v1.6.1**

---

## 目次

1. [概要と目的](#1-概要と目的)
2. [FR-G1: LLM 呼び出し形式の変更（Anthropic Messages API）](#2-fr-g1-llm-呼び出し形式の変更)
3. [FR-G2: エンドポイント URL 入力欄のマスク解除](#3-fr-g2-エンドポイント-url-入力欄のマスク解除)
4. [FR-G3: before 画像フォールバック](#4-fr-g3-before-画像フォールバック)
5. [非機能要件](#5-非機能要件)
6. [実装ガイド（変更ファイル一覧）](#6-実装ガイド変更ファイル一覧)

---

## 1. 概要と目的

### 1.1 背景

v1.6.0 でプロジェクト機能に LLM 連携（インクリメンタル LLM）が実装されていたが、
v1.6.1 を運用開始したところ以下の問題が発見された:

1. **LLM 呼び出しが全件失敗する**  
   - `Azure.AI.Inference` SDK v1.0.0-beta.5 が送信する
     `api-version=2024-05-01-preview` をエンドポイントが拒否する（HTTP 400）
   - Azure AI Foundry の当該エンドポイントは OpenAI 互換の Chat Completions API ではなく、
     **Anthropic Messages API**（`/anthropic/v1/messages`）を使用していた

2. **エンドポイント URL の入力欄がマスク表示**  
   - `PasswordBox` コントロールを使用しているため URL を確認・コピーできない

3. **before 画像が取得できなかったステップで前後比較が機能しない**  
   - `BeforeImagePath` が null のステップはエクスポート時に before 欄が空になる
   - アノテーションがある場合でも「アノテーション前 → アノテーション後」の比較ができない

### 1.2 バージョンポリシー

- **バージョン番号**: 1.6.1
- **後方互換**: v1.6.0 の `project.json` を変更なしで読み込める
- **設定互換**: `config.json` の既存エントリは変更しない

---

## 2. FR-G1: LLM 呼び出し形式の変更

### 2.1 背景・原因

`Azure.AI.Inference` NuGet パッケージの最新版（v1.0.0-beta.5）は
Chat Completions API（OpenAI 互換、`/models/chat/completions?api-version=2024-05-01-preview`）
を使用する。しかし実際の Azure AI Foundry エンドポイントは
**Anthropic Messages API** 形式を要求しており、SDK による呼び出しは以下のエラーで全件失敗していた:

```
Azure.RequestFailedException: API version not supported
Status: 400 (BadRequest)
{"error":{"code":"BadRequest","message":"API version not supported"}}
```

### 2.2 正しい呼び出し形式（curl サンプル）

```bash
curl -X POST "https://proj-claude-aic-resource.services.ai.azure.com/anthropic/v1/messages" \
-H "Content-Type: application/json" \
-H "Authorization: Bearer $AZURE_API_KEY" \
-H "anthropic-version: 2023-06-01" \
-d '{
  "model": "claude-opus-4-7",
  "max_tokens": 1000,
  "system": "You are a helpful assistant.",
  "messages": [{ "role": "user", "content": "What are 3 things to visit in Seattle?" }]
}'
```

### 2.3 変更仕様

#### 変更前（Azure AI Inference SDK）

| 項目 | 旧仕様 |
|---|---|
| クライアント | `Azure.AI.Inference.ChatCompletionsClient` |
| URL | `{endpoint}/chat/completions?api-version=2024-05-01-preview` |
| 認証ヘッダー | `api-key: {key}` |
| バージョン指定 | `?api-version=2024-05-01-preview`（クエリパラメータ） |
| リクエスト形式 | OpenAI Chat Completions 形式（`messages` 配列に `system` を含む） |

#### 変更後（HttpClient + Anthropic Messages API）

| 項目 | 新仕様 |
|---|---|
| クライアント | `System.Net.Http.HttpClient` |
| URL | `{baseUrl}/anthropic/v1/messages`（設定エンドポイントのホスト部分のみ使用） |
| 認証ヘッダー | `Authorization: Bearer {key}` |
| バージョン指定 | `anthropic-version: 2023-06-01`（リクエストヘッダー） |
| リクエスト形式 | Anthropic Messages API 形式（`system` はトップレベル、`messages` は `user` ロールのみ） |
| その他 | `Content-Length` ヘッダーを確実に付与（`ByteArrayContent` を使用） |

#### URL 組み立て規則

設定に保存されているエンドポイント URL からホスト部分のみを抽出し、
Anthropic Messages API パスを付加する:

```
設定値例: https://proj-xxx.services.ai.azure.com/models
→ 生成URL: https://proj-xxx.services.ai.azure.com/anthropic/v1/messages
```

#### リクエスト / レスポンスボディ形式

**リクエスト:**
```json
{
  "model": "{deploymentName}",
  "max_tokens": 4096,
  "system": "{systemPrompt}",
  "messages": [{ "role": "user", "content": "{userPrompt}" }]
}
```

**レスポンス（成功時）:**
```json
{
  "content": [
    { "type": "text", "text": "..." }
  ]
}
```

`content[].type == "text"` の最初の要素の `text` フィールドを使用する。

### 2.4 機能要件

| ID | 要件 |
|---|---|
| FR-G101 | `LlmService` を `Azure.AI.Inference.ChatCompletionsClient` から `HttpClient` に変更する |
| FR-G102 | エンドポイント URL から `{scheme}://{host}` 部分のみ抽出して `/anthropic/v1/messages` を付加する |
| FR-G103 | リクエストヘッダーに `Authorization: Bearer {apiKey}` と `anthropic-version: 2023-06-01` を付与する |
| FR-G104 | リクエストボディは `ByteArrayContent` で送信し `Content-Length` を確実に含める |
| FR-G105 | JSON シリアライズは `JsonNamingPolicy.SnakeCaseLower` を使用する（`max_tokens` 等） |
| FR-G106 | レスポンスの `content[].text`（`type == "text"` の先頭）を返す |
| FR-G107 | HTTP エラー時は `Log.Warning("LLM 呼び出し失敗: {Status} {Body}", ...)` で記録して `null` を返す |
| FR-G108 | 既存の `ImproveDescriptionsAsync` / `ImproveStepDescriptionAsync` / `GenerateDigestAsync` の
           インターフェースは変更しない |
| FR-G109 | DPAPI 暗号化・エンドポイント・API キーの保存形式は変更しない（NF-04 維持）|

### 2.5 セキュリティ

- エンドポイント URL・API キーは引き続き DPAPI 暗号化して `config.json` に保存する（NF-04 変更なし）
- `HttpClient` インスタンスは `static readonly` で共有する（接続プールの適切な管理）
- 通信は HTTPS 限定（設定エンドポイントが HTTPS URL である前提）

---

## 3. FR-G2: エンドポイント URL 入力欄のマスク解除

### 3.1 背景

設定ウィンドウ「LLM 連携」タブの「Microsoft Azure AI Foundry エンドポイント URL」
入力欄が `PasswordBox`（文字マスク）になっており、入力した URL を確認・コピーできない。
エンドポイント URL は API キーと異なり秘匿性が低く、視認できる状態が望ましい。

### 3.2 機能要件

| ID | 要件 |
|---|---|
| FR-G201 | 設定ウィンドウ「LLM 連携」タブのエンドポイント URL 入力欄を `PasswordBox` から `TextBox` に変更する |
| FR-G202 | コントロール名を `PwdLlmEndpoint` → `TxtLlmEndpoint` に変更する |
| FR-G203 | `LoadSettings` で `TxtLlmEndpoint.Text` に復号済み URL を表示する |
| FR-G204 | `ApplySettings` で `TxtLlmEndpoint.Text.Trim()` を DPAPI 暗号化して保存する |
| FR-G205 | API キー入力欄（`PwdLlmApiKey`）は引き続き `PasswordBox` のままとする |

---

## 4. FR-G3: before 画像フォールバック

### 4.1 背景

v1.6.0 で実装した「操作前後スクリーンショット分離」機能では、
before 画像を取得できなかったステップ（例: キーボード入力の長時間セッション、
アクティブウィンドウ切替など非対象トリガー）は `BeforeImagePath == null` となり、
エクスポート時に before 欄が空白になる。

ユーザーがアノテーションを付与した場合でも「アノテーション前 → アノテーション後」の
比較ができず、手順書の可読性が低下する。

### 4.2 フォールバック仕様

`BeforeImagePath` が null の場合、**素の after 画像**（アノテーション焼き込み前の元ファイル）を
before 画像として扱う。

```
BeforeImagePath != null  →  通常の before 画像を使用
BeforeImagePath == null  →  素の AfterImagePath（元ファイル）をフォールバックとして使用
```

「素の after 画像」とは、`AnnotationRenderer.Render()` を適用する前の
`{project}/images/{filename}` を指す。

### 4.3 適用箇所

フォールバックを適用する箇所:

| 箇所 | 対象エクスポート | 変更内容 |
|---|---|---|
| `ExportService.BuildAnnotatedSession()` | Markdown / Word / 動画 | `beforeImagePath = rawAfterPath` にフォールバック |
| `ExportService.ExportImagesAsync()` | 画像エクスポート | `beforeRelPath = step.BeforeImagePath ?? step.AfterImagePath` |
| `HtmlManualWriter.BuildBeforeImageTag()` | HTML | `relPath = step.BeforeImagePath ?? step.AfterImagePath` |
| `ProjectViewWindow.LoadAnnotationImage()` | UI 表示 | `beforeRelPath = step.BeforeImagePath ?? step.AfterImagePath` |

### 4.4 エクスポート時の表示

フォールバックが適用された場合の各エクスポートでの表示:

| エクスポート | before 欄 | after 欄 |
|---|---|---|
| HTML | 素の after 画像（アノテーションなし） | アノテーション焼き込み after 画像（またはそのまま） |
| Markdown / Word | 素の after 画像（ファイル参照） | アノテーション焼き込み after 画像（またはそのまま） |
| 画像エクスポート | `before/` フォルダに素の after 画像をコピー | 通常の after 画像（アノテーション焼き込み済み） |
| ProjectViewWindow | 素の after 画像（`PnlBeforeImage` に表示） | `AnnCanvas` オーバーレイ付き after 画像 |

### 4.5 機能要件

| ID | 要件 |
|---|---|
| FR-G301 | `ExportService.BuildAnnotatedSession()` で `BeforeImagePath == null` の場合、`rawAfterPath`（アノテーション焼き込み前の after パス）を `beforeImagePath` に設定する |
| FR-G302 | `ExportService.ExportImagesAsync()` の before フォルダコピー処理で `step.BeforeImagePath ?? step.AfterImagePath` を参照する |
| FR-G303 | `HtmlManualWriter.BuildBeforeImageTag()` で `step.BeforeImagePath ?? step.AfterImagePath` をフォールバックとして使用する |
| FR-G304 | `ProjectViewWindow.LoadAnnotationImage()` で before 画像の読み込みに `step.BeforeImagePath ?? step.AfterImagePath` を使用する |
| FR-G305 | フォールバック使用時も before 画像は素のファイルをそのまま参照する（`AnnotationRenderer.Render()` を適用しない） |

---

## 5. 非機能要件

| ID | 要件 | 実装方針 |
|---|---|---|
| NF-G01 | LLM 呼び出しの失敗は `Log.Warning` で記録し、ルールベース説明文を維持する（L-06 継承）| `CallLlmAsync` で HTTP エラー時に `null` を返す |
| NF-G02 | `HttpClient` はスタティックフィールドで管理し、接続プールを効率的に使用する | `private static readonly HttpClient s_http = new()` |
| NF-G03 | 既存プロジェクトのエクスポート動作（before なし → 空白）が before ありに変化しても、データの破壊や不整合は生じない | フォールバックは参照のみ（ファイルの移動・変更なし） |
| NF-G04 | エンドポイント URL の DPAPI 暗号化・復号は変更しない（NF-04 継承）| `DpapiHelper.Protect` / `Unprotect` を継続使用 |

---

## 6. 実装ガイド（変更ファイル一覧）

| ファイル | 変更種別 | 対象機能 | 概要 |
|---|---|---|---|
| `Services/LlmService.cs` | **変更** | FR-G1 | `Azure.AI.Inference` SDK を `HttpClient` に置き換え。Anthropic Messages API 形式で呼び出す |
| `Views/SettingsWindow.xaml` | **変更** | FR-G2 | `PasswordBox x:Name="PwdLlmEndpoint"` → `TextBox x:Name="TxtLlmEndpoint"` |
| `Views/SettingsWindow.xaml.cs` | **変更** | FR-G2 | `PwdLlmEndpoint.Password` → `TxtLlmEndpoint.Text` |
| `Services/ExportService.cs` | **変更** | FR-G3 | `BuildAnnotatedSession` と `ExportImagesAsync` に before フォールバックを追加 |
| `Services/HtmlManualWriter.cs` | **変更** | FR-G3 | `BuildBeforeImageTag` に before フォールバックを追加 |
| `Views/ProjectViewWindow.xaml.cs` | **変更** | FR-G3 | `LoadAnnotationImage` に before フォールバックを追加 |
| `AutoScreenshot.csproj` | **変更** | — | バージョン 1.6.0 → 1.6.1 |

---

*文書終端*
