namespace AutoScreenshot.Models;

/// <summary>動画生成機能の設定</summary>
public class VideoGenConfig
{
    // --- 出力形式 ---
    public bool OutputApng { get; set; } = true;
    public bool OutputMp4  { get; set; } = true;

    // --- 出力フォルダ（FR-H3） ---

    /// <summary>動画のベースフォルダ。</summary>
    public string VideoBaseFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "AutoScreenshot");

    /// <summary>日付テンプレートフォルダ名。</summary>
    public string VideoFolderTemplate { get; set; } = "{date_time}";

    /// <summary>第2ベースフォルダ（空文字 = フォールバックなし）。</summary>
    public string VideoFallbackBaseFolder { get; set; } = "";

    // ---- 後方互換 JSON 移行シム ----

    /// <summary>v1.6.x 以前の VideoOutputFolder を VideoBaseFolder へ移行する。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("videoOutputFolder")]
    public string? LegacyVideoOutputFolder
    {
        get => null;
        init
        {
            if (!string.IsNullOrWhiteSpace(value))
                VideoBaseFolder = value;
        }
    }


    // --- 構成単位 ---
    public VideoUnit VideoUnit { get; set; } = VideoUnit.Session;

    // --- フレーム表示時間 ---
    public FrameTimingMode FrameTimingMode { get; set; } = FrameTimingMode.Fixed;
    public double FixedFrameSeconds { get; set; } = 3.0;
    public double MinFrameSeconds   { get; set; } = 1.0;
    public double MaxFrameSeconds   { get; set; } = 10.0;

    // --- 解像度・品質 ---
    public VideoResolution OutputResolution { get; set; } = VideoResolution.Original;
    public int Mp4VideoBitrateMbps { get; set; } = 4;

    // --- 強調表示 ---
    public bool DrawRipple    { get; set; } = true;
    public bool DrawRectangle { get; set; } = true;

    // --- テロップ ---
    public bool ShowTelop         { get; set; } = true;
    public bool TelopShowEventLabel { get; set; } = true;
    public bool TelopShowInputText  { get; set; } = true;
    public bool TelopShowDescription { get; set; } = true;
    public bool TelopShowTimestamp  { get; set; } = true;
    public int  TelopFontSize       { get; set; } = 16;
    public byte TelopBgAlpha        { get; set; } = 160;

    // --- TTS ---
    public bool   TtsEnabled  { get; set; } = true;
    public string TtsVoice    { get; set; } = "";   // 空 = OS 既定
    public int    TtsRate     { get; set; } = 0;    // -10 〜 10
    public int    TtsVolume   { get; set; } = 100;  // 0 〜 100

    // --- 生成タイミング ---
    public bool AutoGenerateWithManual { get; set; } = false;

    // --- 完了時動作 ---
    public bool OpenFolderOnComplete { get; set; } = true;
}

public enum VideoUnit        { Session, Chapter }
public enum FrameTimingMode  { Fixed, RealTime }
public enum VideoResolution  { Original, Fhd, Hd }
