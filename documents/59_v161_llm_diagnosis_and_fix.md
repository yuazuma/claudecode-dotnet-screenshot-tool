# v1.6.1 LLM 連携診断・Anthropic Messages API 実装

## ユーザープロンプト（計2回）

```
（1回目）
LLM連携設定が正しく設定されているか確認してください。

（2回目）
RESTのサンプルを添付するので、それに合わせてください。

curl -X POST "https://proj-claude-aic-resource.services.ai.azure.com/anthropic/v1/messages" \
-H "Content-Type: application/json" \
-H "Authorization: Bearer $AZURE_API_KEY" \
-H "anthropic-version: 2023-06-01" \
-d '{
  "model": "claude-opus-4-7",
  "max_tokens": 1000,
  "temperature": 0.7,
  "system": "You are a helpful assistant.",
  "messages": [
    { "role": "user", "content": "What are 3 things to visit in Seattle?" }
  ]
}'
```

---

## 1. 設定値確認

config.json の確認結果:

| 設定項目 | 状態 |
|---|---|
| LlmEnabled | true |
| LlmDeploymentName | `claude-opus-4-7` |
| LlmEndpoint | 設定済み（DPAPI 暗号化 372 文字） |
| LlmApiKey | 設定済み（DPAPI 暗号化 416 文字） |
| IncrementalLlm | true |

---

## 2. LLM 呼び出し失敗の診断

### 2.1 初期症状

ログに全件 `[WRN] インクリメンタル LLM 呼び出し失敗` が出力されていた。

```
Azure.RequestFailedException: API version not supported
Status: 400 (BadRequest)
{"error":{"code":"BadRequest","message":"API version not supported"}}
```

### 2.2 API バージョン探索

`Azure.AI.Inference` v1.0.0-beta.5 が送信する `api-version=2024-05-01-preview` が拒否される。
NuGet でこれ以上新しいバージョンは存在しない（beta.5 が最新）。

パイプラインポリシーで api-version を各種値に置き換えて動作を確認:

| api-version | HTTP | エラー内容 | バックエンド到達 |
|---|---|---|---|
| `2024-05-01-preview`（SDK 既定） | 400 | `"API version not supported"` | ✅ 到達するが拒否 |
| なし（除去） | 404 | `"api_not_supported"` | APIM でブロック |
| `2024-09-01-preview` | 404 | `"Resource not found"` | APIM でブロック |
| `2024-12-01-preview` | 404 | `"Resource not found"` | APIM でブロック |
| `2025-03-01-preview` | 404 | `"Resource not found"` | APIM でブロック |

**結論**: APIM は `2024-05-01-preview` のみバックエンドにルーティングするが、
バックエンド（Claude モデル側）が OpenAI 互換形式を拒否していた。
他バージョンは APIM 自体が受け付けない。

### 2.3 実際の呼び出し URL の特定（デバッグログ追加）

パイプラインポリシーにデバッグログを追加して URL を確認:

```
LLM request URI: "https://proj-claude-aic-resource.services.ai.azure.com/models/chat/completions?api-version=..."
```

エンドポイント `/models/chat/completions` = Azure AI Inference（OpenAI 互換）形式。
しかし実際のエンドポイントが必要とするのは `/anthropic/v1/messages` = Anthropic Messages API 形式。

---

## 3. Anthropic Messages API への変更

### 3.1 REST サンプルから判明した正しい形式

```bash
curl -X POST "https://proj-claude-aic-resource.services.ai.azure.com/anthropic/v1/messages" \
-H "Authorization: Bearer $AZURE_API_KEY" \
-H "anthropic-version: 2023-06-01" \
-H "Content-Type: application/json" \
-d '{ "model": "claude-opus-4-7", "max_tokens": 1000, "system": "...", "messages": [...] }'
```

### 3.2 変更仕様

| 項目 | 旧（Azure AI Inference SDK） | 新（HttpClient + Anthropic Messages API） |
|---|---|---|
| クライアント | `ChatCompletionsClient` | `HttpClient` |
| URL | `{endpoint}/chat/completions?api-version=...` | `{base}/anthropic/v1/messages` |
| 認証 | `api-key: {key}` | `Authorization: Bearer {key}` |
| バージョン | `?api-version=2024-05-01-preview` | `anthropic-version: 2023-06-01` ヘッダー |
| ボディ | OpenAI Chat Completions 形式 | Anthropic Messages API 形式 |

### 3.3 実装のポイント

- `BuildMessagesUrl()`: 設定エンドポイントから `{scheme}://{host}` のみ抽出して `/anthropic/v1/messages` を付加
- `ByteArrayContent` を使用: `Content-Length` ヘッダーを確実に付与（`JsonContent` では省略されて 400 になる）
- `JsonNamingPolicy.SnakeCaseLower`: `max_tokens`、`anthropic_version` 等を自動変換

### 3.4 動作確認

LLM 成功後に `project.json` を確認:

```
Steps: 12
Steps with descriptionLlm: 12  ← 全ステップが LLM で改善済み
```

例:
```
step 1 llm: Visual Studio Code(管理者モード)の「claudecode-dotnet-screenshot-tool」...
step 2 llm: タスクバーまたは画面下部の項目（座標 1109, 1175）をクリック...
```

LLM 連携が正常に動作することを確認。

---

## 変更ファイル

`src/AutoScreenshot/Services/LlmService.cs` — 全面的に書き換え
