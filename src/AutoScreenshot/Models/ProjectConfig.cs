namespace AutoScreenshot.Models;

/// <summary>プロジェクトファイル機能の設定</summary>
public class ProjectConfig
{
    /// <summary>プロジェクト機能を有効にする（false = v1.1.0 以前の動作）</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>サムネイルの最大幅 (px)</summary>
    public int ThumbnailMaxWidth { get; set; } = 320;

    /// <summary>アプリ終了時に Markdown 手順書を自動エクスポートする</summary>
    public bool AutoExportMarkdown { get; set; } = true;

    /// <summary>アプリ終了時に Word 手順書を自動エクスポートする</summary>
    public bool AutoExportDocx { get; set; } = false;

    /// <summary>アプリ終了時に動画を自動エクスポートする</summary>
    public bool AutoExportVideo { get; set; } = false;

    /// <summary>エクスポート完了時に exports/ フォルダを自動で開く</summary>
    public bool OpenFolderOnExportComplete { get; set; } = true;
}
