namespace AutoScreenshot.Models;

/// <summary>タスクトレイアイコンの表示状態。優先度: Error > Processing > Paused/Recording</summary>
public enum IconState
{
    /// <summary>通常録画中（青）</summary>
    Recording,
    /// <summary>一時停止中（グレー）</summary>
    Paused,
    /// <summary>撮影成功フラッシュ — 200ms 後に基本状態に自動復帰（緑）</summary>
    Captured,
    /// <summary>LLM生成・動画エンコード等のバックグラウンド処理中（オレンジ）</summary>
    Processing,
    /// <summary>エラー発生 — 5秒後に基本状態に自動復帰（赤）</summary>
    Error,
}
