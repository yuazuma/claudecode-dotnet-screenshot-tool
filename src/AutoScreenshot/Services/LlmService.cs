using System.Text;
using System.Text.RegularExpressions;
using AutoScreenshot.Models;
using Azure;
using Azure.AI.Inference;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>Azure AI Foundry 経由で Claude モデルを呼び出す LLM サービス (L-03, L-04, L-08)</summary>
public class LlmService
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;

    public LlmService(string endpoint, string apiKey, string deploymentName)
    {
        _endpoint       = endpoint;
        _apiKey         = apiKey;
        _deploymentName = deploymentName;
    }

    /// <summary>
    /// 全ステップの操作目的テキストを一括で LLM に送り、より自然な日本語に改善する (L-03)。
    /// 成功した場合は DescriptionLlm を更新する。失敗時はルールベースを維持する (L-06)。
    /// </summary>
    public async Task ImproveDescriptionsAsync(ManualSession session)
    {
        if (session.Steps.Count == 0) return;

        var sb = new StringBuilder();
        for (int i = 0; i < session.Steps.Count; i++)
            sb.AppendLine($"{i + 1}. {session.Steps[i].DescriptionRuleBased}");

        const string system = """
            あなたは業務操作手順書の文書化アシスタントです。
            与えられた操作ステップの説明文リストを、より自然で分かりやすい日本語に改善してください。
            元の番号順を保持し、各行を「番号. 説明文」の形式で返してください。
            説明文は簡潔かつ具体的にしてください。番号と説明文以外の文は出力しないでください。
            """;

        string user = $"以下の操作ステップの説明文を改善してください:\n\n{sb}";

        string? result = await CallLlmAsync(system, user);
        if (result == null) return;

        foreach (var line in result.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(line.Trim(), @"^(\d+)\.\s*(.+)$");
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out int num)) continue;
            int idx = num - 1;
            if (idx >= 0 && idx < session.Steps.Count)
                session.Steps[idx].DescriptionLlm = m.Groups[2].Value.Trim();
        }
    }

    /// <summary>
    /// 単一ステップの説明文を LLM で改善して返す（インクリメンタル LLM 用・FR-B）。
    /// 失敗時は null を返す。
    /// </summary>
    public async Task<string?> ImproveStepDescriptionAsync(
        string triggerType, string? uiElementName, string? windowTitle,
        string? ruleBasedDescription, CancellationToken ct = default)
    {
        string context = $"操作種別: {triggerType}\n" +
                         $"UI要素: {uiElementName ?? "(不明)"}\n" +
                         $"ウィンドウ: {windowTitle ?? "(不明)"}\n" +
                         $"ルールベース説明: {ruleBasedDescription ?? "(なし)"}";

        const string system = """
            あなたは業務操作手順書の文書化アシスタントです。
            1つの操作ステップの説明文を、より自然で分かりやすい日本語に改善してください。
            改善した説明文のみを1行で出力してください。前置きや後書きは不要です。
            """;

        try
        {
            var client  = new ChatCompletionsClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));
            var options = new ChatCompletionsOptions
            {
                Model    = _deploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(system),
                    new ChatRequestUserMessage($"以下の操作ステップの説明文を改善してください:\n\n{context}"),
                },
                MaxTokens = 256,
            };
            Response<ChatCompletions> response = await client.CompleteAsync(options, ct);
            string? result = response.Value.Content?.Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "インクリメンタル LLM 呼び出し失敗");
            return null;
        }
    }

    /// <summary>
    /// セッション全体の操作ログを LLM に渡し、3〜5 行の要約文を生成する (L-04)。
    /// 失敗時は null を返す (L-06)。
    /// </summary>
    public async Task<string?> GenerateDigestAsync(ManualSession session)
    {
        if (session.Steps.Count == 0) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"タイトル: {session.Title}");
        sb.AppendLine($"開始: {session.StartedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("操作ステップ:");
        foreach (var step in session.Steps)
        {
            string desc = step.DescriptionLlm ?? step.DescriptionRuleBased;
            sb.AppendLine($"- {desc}");
        }

        const string system = """
            あなたは業務操作手順書の文書化アシスタントです。
            与えられた操作ログを分析し、3〜5行の簡潔な要約文を日本語で生成してください。
            要約のみを出力し、前置きや後書きは不要です。
            """;

        string user = $"以下の操作ログを3〜5行で要約してください:\n\n{sb}";

        return await CallLlmAsync(system, user);
    }

    // ── 内部呼び出し ───────────────────────────────────────────────────────────

    private async Task<string?> CallLlmAsync(string systemPrompt, string userPrompt)
    {
        try
        {
            var client  = new ChatCompletionsClient(new Uri(_endpoint), new AzureKeyCredential(_apiKey));
            var options = new ChatCompletionsOptions
            {
                Model    = _deploymentName,
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt),
                },
                MaxTokens = 4096,
            };

            Response<ChatCompletions> response = await client.CompleteAsync(options);
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLM 呼び出し失敗: {Endpoint}", _endpoint);
            return null;
        }
    }
}
