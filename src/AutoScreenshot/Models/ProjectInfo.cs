using System.Text.Json.Serialization;

namespace AutoScreenshot.Models;

/// <summary>プロジェクトフォルダ内の project.json に対応するデータモデル</summary>
public class ProjectInfo
{
    public string FormatVersion { get; set; } = "1.0";
    public Guid ProjectId { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.Now;
    public string OsInfo { get; set; } = $"{Environment.OSVersion} / {Environment.UserName}";
    public DateTimeOffset? EndedAt { get; set; }
    public string? Digest { get; set; }
    public List<ProjectStep> Steps { get; set; } = [];
    public List<ExportRecord> ExportHistory { get; set; } = [];

    /// <summary>project.json が格納されているフォルダへの絶対パス（JSON には保存しない）</summary>
    [JsonIgnore]
    public string ProjectFolder { get; set; } = "";
}

public class ProjectStep
{
    public int StepNumber { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string TriggerType { get; set; } = "";
    public string? UiElementName { get; set; }
    public string? UiControlType { get; set; }
    public int CursorX { get; set; }
    public int CursorY { get; set; }
    public string WindowTitle { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public string? InputText { get; set; }
    public string? KeyCodes { get; set; }

    /// <summary>images/ 配下への相対パス</summary>
    public string? ImagePath { get; set; }

    /// <summary>thumbs/ 配下への相対パス</summary>
    public string? ThumbPath { get; set; }

    public string DescriptionRuleBased { get; set; } = "";
    public string? DescriptionLlm { get; set; }

    /// <summary>ユーザーが手修正した説明文。非 null の場合はエクスポートで最優先使用。</summary>
    public string? DescriptionOverride { get; set; }

    public bool NeedsReview { get; set; }

    /// <summary>削除フラグ。true のステップはエクスポート対象から除外。画像は _deleted/ へ移動。</summary>
    public bool IsDeleted { get; set; }

    /// <summary>エクスポート時に使用する実効説明文を返す</summary>
    [JsonIgnore]
    public string EffectiveDescription =>
        DescriptionOverride ?? DescriptionLlm ?? DescriptionRuleBased;
}

public class ExportRecord
{
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;
    public string Type { get; set; } = "";
    public string OutputPath { get; set; } = "";
}

public enum ExportType
{
    Images,
    Markdown,
    Docx,
    Video,
    Zip,
}
