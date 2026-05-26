# Microsoft Azure AI Foundry API への変更

## 日時
2026-05-26

## プロンプト要約
「外部LLMの機能を呼び出す際には、Claude APIのAPIキーを利用するのではなく、Microsoft Foundryで払い出されたAPIキーを利用してClaudeモデルを呼び出すような構成とするように要件定義書に明記してください。」

---

## 変更対象ファイル

`requirements/追加要件_手順書作成支援.md`

---

## 変更内容

### セクション 3.6 LLM 連携 — 冒頭方針ブロック追加

```markdown
> **API 呼び出し方針**: LLM の呼び出しには **Anthropic の Claude API を直接使用しない**。
> 社内または契約環境の **Microsoft Azure AI Foundry** で払い出された API キー・エンドポイント URL を使用し、
> Foundry 上にデプロイされた Claude モデルを呼び出す構成とする。
> クライアント実装には NuGet パッケージ `Azure.AI.Inference`（Azure AI Inference SDK）を使用する。
```

### 変更した要件

| 要件 | 変更内容 |
|------|---------|
| **L-01** | 入力欄を①Foundry エンドポイント URL・②Foundry API キーの2つに変更 |
| **L-02** | 有効化条件を「エンドポイント URL と API キーの両方が設定されている場合」に変更 |
| **L-03** | 送信先を「Azure AI Foundry エンドポイント（Claude モデル）」に明記 |
| **L-07** | 「モデル ID」→「Foundry 上のデプロイメント名」に変更（既定: `claude-haiku-4-5`） |
| **L-08** *(新規)* | `Azure.AI.Inference` の `ChatCompletionsClient` + `AzureKeyCredential` を使う実装方針を明記。Anthropic SDK (`Anthropic.*` 系 NuGet) は使用しないと明示。 |

### 設定テーブル変更（セクション 3.7）

| 変更前 | 変更後 |
|--------|--------|
| `LlmApiKey` — Claude API キー | `LlmEndpoint` — Foundry エンドポイント URL（DPAPI 暗号化）|
| `LlmModel` — モデル ID | `LlmApiKey` — Foundry API キー（DPAPI 暗号化）|
| —  | `LlmDeploymentName` — Foundry デプロイメント名 |

### NF-04 変更

「API キー」→「Azure AI Foundry の **API キーおよびエンドポイント URL**」の両方を Windows DPAPI 暗号化の対象として明記。
