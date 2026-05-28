namespace AutoScreenshot.Models;

/// <summary>
/// ステップ画像上に重ねるアノテーション 1 項目（FR-C）。
/// 座標は元画像の原寸ピクセル単位。
/// </summary>
public class AnnotationItem
{
    /// <summary>種別: "Number" | "Arrow" | "Rect" | "Callout"</summary>
    public string Type { get; set; } = "";

    /// <summary>配置基点 X（番号・矢印始点・矩形左端・吹き出し先端）</summary>
    public int X { get; set; }

    /// <summary>配置基点 Y</summary>
    public int Y { get; set; }

    /// <summary>終点 X（矢印先端・矩形右端）。Number/Callout では未使用</summary>
    public int X2 { get; set; }

    /// <summary>終点 Y（矢印先端・矩形下端）。Number/Callout では未使用</summary>
    public int Y2 { get; set; }

    /// <summary>Number: 表示数値文字列 / Callout: テキスト / それ以外: null</summary>
    public string? Label { get; set; }

    /// <summary>描画色（CSS hex 形式: "#FF0000"）</summary>
    public string Color { get; set; } = "#FF0000";
}
