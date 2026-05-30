using System.Globalization;
using System.Text.RegularExpressions;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// フォルダ名テンプレートを評価するサービス。
/// {placeholder} 形式と .NET DateTime 書式指定子（yyyy, MM, dd, HH, mm, ss）を組み合わせて使用できる。
/// </summary>
public static class FolderTemplateService
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    // テンプレート内のプレースホルダーにマッチする正規表現
    private static readonly Regex PlaceholderRegex = new Regex(@"\{([^}]+)\}", RegexOptions.Compiled);

    // 安全な複数文字 DateTime トークンのみを対象とする正規表現
    // 単一文字（s, d, m など）は誤マッチを防ぐため除外する
    private static readonly Regex DateTimeTokenRegex = new Regex(
        @"yyyy|yy|MM|dd|HH|hh|mm|ss|fff",
        RegexOptions.Compiled);

    /// <summary>
    /// テンプレート文字列をセッション開始日時・タイトル等を使って評価し、フォルダ名を返す。
    /// </summary>
    /// <param name="template">テンプレート文字列。例: "{date_time}_{title_short}.ascproj"</param>
    /// <param name="sessionStart">セッション開始日時</param>
    /// <param name="title">セッション/プロジェクトタイトル（null の場合は空文字）</param>
    /// <param name="sessionId">セッション ID（null の場合は id プレースホルダーが空文字になる）</param>
    /// <returns>評価済みフォルダ名（パス区切り文字は除去済み）。テンプレートが空の場合は空文字を返す。</returns>
    public static string Evaluate(string? template, DateTime sessionStart,
        string? title = null, Guid? sessionId = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        // Step 1: {placeholder} を展開する
        string expanded = PlaceholderRegex.Replace(template, m =>
        {
            string key = m.Groups[1].Value;
            return key switch
            {
                // --- 日時短縮形（直接文字列に展開） ---
                "date"       => sessionStart.ToString("yyyyMMdd",   CultureInfo.InvariantCulture),
                "datetime"   => sessionStart.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                "date_time"  => sessionStart.ToString("yyyyMMdd",   CultureInfo.InvariantCulture)
                                + "_"
                                + sessionStart.ToString("HHmmss",   CultureInfo.InvariantCulture),
                "date-time"  => sessionStart.ToString("yyyyMMdd",   CultureInfo.InvariantCulture)
                                + "-"
                                + sessionStart.ToString("HHmmss",   CultureInfo.InvariantCulture),
                "hour"       => sessionStart.ToString("HH",         CultureInfo.InvariantCulture) + "00",

                // --- セッション情報 ---
                // ProtectFromDateTime() で展開済み値に含まれる DateTime トークン（MM/dd 等）が
                // Step 2 の正規表現で誤置換されないようエスケープする
                "title"       => ProtectFromDateTime(SanitizeSegment(title ?? string.Empty)),
                "title_short" => ProtectFromDateTime(SanitizeSegment(Truncate(title ?? string.Empty, 40))),
                "id"          => sessionId.HasValue
                                    ? sessionId.Value.ToString("N")[..8]
                                    : string.Empty,

                // --- 環境情報 ---
                "username"     => ProtectFromDateTime(SanitizeSegment(Environment.UserName)),
                "computername" => ProtectFromDateTime(SanitizeSegment(Environment.MachineName)),

                // 未知プレースホルダーはそのまま残す（後段で DateTime 書式として処理されない）
                _ => m.Value,
            };
        });

        // Step 2: 安全な既知の複数文字 DateTime トークンのみを置換する
        // 展開済み値は ProtectFromDateTime() により \x02xxx\x03 でエスケープ済み
        expanded = DateTimeTokenRegex.Replace(expanded, m =>
            sessionStart.ToString(m.Value, CultureInfo.InvariantCulture));

        // Step 3: エスケープを解除する
        expanded = UnprotectFromDateTime(expanded);

        // Step 3: パスとして使用できない文字を '_' に置換
        return SanitizePath(expanded);
    }

    // Step 2 の正規表現で誤置換されないよう、展開済み値内の DateTime トークンをエスケープする。
    // \x02 と \x03 はパス名に現れない制御文字をセンチネルとして使用する。
    private static string ProtectFromDateTime(string s) =>
        string.IsNullOrEmpty(s) ? s : DateTimeTokenRegex.Replace(s, m => $"\x02{m.Value}\x03");

    private static string UnprotectFromDateTime(string s) =>
        string.IsNullOrEmpty(s) ? s : s.Replace("\x02", "").Replace("\x03", "");

    /// <summary>フォルダ名セグメント内の無効文字を '_' に置換する（パス区切り文字は除去）。</summary>
    private static string SanitizeSegment(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var buf = s.ToCharArray();
        for (int i = 0; i < buf.Length; i++)
        {
            if (InvalidChars.Contains(buf[i])) buf[i] = '_';
        }
        return new string(buf);
    }

    /// <summary>フォルダ名全体からパス区切り文字を除去し、無効文字を '_' に置換する。</summary>
    private static string SanitizePath(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var buf = s.ToCharArray();
        for (int i = 0; i < buf.Length; i++)
        {
            char c = buf[i];
            // パス区切り文字（\ /）は '_' に置換（ただし .ascproj の '.' はそのまま）
            if (c == '\\' || c == '/') buf[i] = '_';
            else if (Array.IndexOf(InvalidChars, c) >= 0 && c != '.') buf[i] = '_';
        }
        return new string(buf).Trim();
    }

    private static string Truncate(string s, int maxLength)
        => s.Length <= maxLength ? s : s[..maxLength];
}
