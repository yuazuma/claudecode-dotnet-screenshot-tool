using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// Azure AI Foundry 経由で Claude モデルを呼び出す LLM サービス (L-03, L-04, L-08)。
/// エンドポイントが Anthropic Messages API 形式（/anthropic/v1/messages）を使用するため
/// HttpClient で直接呼び出す。認証・エンドポイントは Azure AI Foundry の DPAPI 暗号化設定値を使用する。
/// </summary>
public class LlmService
{
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;

    private static readonly HttpClient s_http = new();
    private static readonly JsonSerializerOptions s_jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy   = JsonNamingPolicy.SnakeCaseLower,
    };

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
            return await CallLlmAsync(system,
                $"以下の操作ステップの説明文を改善してください:\n\n{context}",
                maxTokens: 256,
                ct: ct);
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

    private async Task<string?> CallLlmAsync(
        string systemPrompt, string userPrompt,
        int maxTokens = 4096, CancellationToken ct = default)
    {
        try
        {
            string url = BuildMessagesUrl();
            var reqBody = new AnthropicRequest
            {
                Model     = _deploymentName,
                MaxTokens = maxTokens,
                System    = systemPrompt,
                Messages  = [new AnthropicMessage { Role = "user", Content = userPrompt }],
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", $"Bearer {_apiKey}");
            req.Headers.Add("anthropic-version", "2023-06-01");

            // ByteArrayContent を使うことで Content-Length ヘッダーが自動的に付与される
            byte[] bodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reqBody, s_jsonOpts));
            req.Content = new ByteArrayContent(bodyBytes);
            req.Content.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            using var resp = await s_http.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                string err = await resp.Content.ReadAsStringAsync(ct);
                Log.Warning("LLM 呼び出し失敗: {Status} {Body}", resp.StatusCode, err);
                return null;
            }

            var result = await resp.Content.ReadFromJsonAsync<AnthropicResponse>(s_jsonOpts, ct);
            return result?.Content?.FirstOrDefault(c => c.Type == "text")?.Text?.Trim();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLM 呼び出し失敗: {Endpoint}", _endpoint);
            return null;
        }
    }

    // エンドポイント URL から host を取り出し、Anthropic Messages API パスを組み立てる。
    // 設定例: "https://proj-xxx.services.ai.azure.com/models"
    // → "https://proj-xxx.services.ai.azure.com/anthropic/v1/messages"
    private string BuildMessagesUrl()
    {
        var uri = new Uri(_endpoint);
        return $"{uri.Scheme}://{uri.Host}/anthropic/v1/messages";
    }

    // ── Anthropic Messages API リクエスト / レスポンスモデル ──────────────────

    private sealed class AnthropicRequest
    {
        public string Model { get; set; } = "";
        public int MaxTokens { get; set; }
        public string? System { get; set; }
        public List<AnthropicMessage> Messages { get; set; } = [];
    }

    private sealed class AnthropicMessage
    {
        public string Role    { get; set; } = "";
        public string Content { get; set; } = "";
    }

    private sealed class AnthropicResponse
    {
        public List<AnthropicContent>? Content { get; set; }
    }

    private sealed class AnthropicContent
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
    }
}
