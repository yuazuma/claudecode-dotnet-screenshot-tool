namespace AutoScreenshot.Models;

/// <summary>エクスポート処理の進捗情報（FR-H6）</summary>
public record ExportProgress(
    /// <summary>現在実行中の操作説明（例: "Markdown 手順書を生成中..."）</summary>
    string OperationName,
    /// <summary>現在の処理数。0 の場合は不確定（IsIndeterminate）</summary>
    int Current,
    /// <summary>合計処理数。0 の場合は不確定</summary>
    int Total,
    /// <summary>出力先ファイルパス（null = 非表示）</summary>
    string? OutputPath = null
)
{
    /// <summary>進捗バーを不確定モードにするかどうか。</summary>
    public bool IsIndeterminate => Total <= 0;

    /// <summary>進捗率（0.0〜1.0）。不確定の場合は 0.0。</summary>
    public double Fraction => IsIndeterminate ? 0.0 : Math.Clamp((double)Current / Total, 0.0, 1.0);

    /// <summary>トレイツールヒント用の短縮ラベル。</summary>
    public string ToTooltipLine() => IsIndeterminate
        ? OperationName
        : $"{OperationName.TrimEnd('.', '。')} {Current}/{Total}";
}
