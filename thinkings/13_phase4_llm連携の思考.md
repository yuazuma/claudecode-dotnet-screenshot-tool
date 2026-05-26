# Phase 4 LLM連携 実装の思考

## 設計判断

### Azure.AI.Inference SDK API の変更点

`1.0.0-beta.2` の BREAKING CHANGES:
> `ChatCompletions.Choices` has been removed, and the underlying properties have been bubbled up to be on the `ChatCompletions` object instead.

つまり beta.1 の `response.Value.Choices[0].Message.Content` ではなく、
beta.2 以降は `response.Value.Content` が正しい。

README が古い記述を残していたため誤用しやすいが、CHANGELOG で確認して修正した。

### DPAPI スコープ選択

`DataProtectionScope.CurrentUser` を選択。
- `LocalMachine` だと同一マシンの他ユーザーも復号可能になるため不適切
- `CurrentUser` でログオン中ユーザーのみが復号できる

### LLM 呼び出し単位 (L-03)

全ステップを「1. ... \n 2. ... \n」形式で一括送信し、レスポンスを正規表現 `^(\d+)\.\s*(.+)$` でパース。
- API コールを1回に絞りコストを最小化
- パース失敗した行は DescriptionLlm = null のまま（ルールベースを維持）

### ImproveDescriptionsAsync → GenerateDigestAsync の順序

L-03でDescriptionLlmを設定してから L-04のGenerateDigestAsync を呼ぶ。
GenerateDigestAsync では `step.DescriptionLlm ?? step.DescriptionRuleBased` を使うため、
LLM改善済みテキストに基づいてダイジェストが生成される。

### PasswordBox (L-01)

WPF の PasswordBox は Password プロパティが DependencyProperty でないため、
通常のバインディングができない。コードビハインドで直接 `.Password` にアクセスする実装を採用した。

### LLM失敗時のフォールバック (L-06)

- `CallLlmAsync` 内の例外は `Log.Warning` して `null` を返す
- `ImproveDescriptionsAsync` は `null` を受け取ったら早期リターン（DescriptionLlm は null のまま）
- `GenerateDigestAsync` は `null` を返す → `session.Digest = null` のまま
- Markdown/docx ライターは Digest が null なら何も出力しない
- 手順書生成は正常続行（ルールベーステキストで出力）
