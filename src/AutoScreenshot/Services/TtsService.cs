using System.Speech.Synthesis;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>Windows SAPI を使って各ステップの説明文を WAV バイト列に変換するサービス</summary>
public class TtsService : IDisposable
{
    private readonly SpeechSynthesizer _synth = new();

    public TtsService(VideoGenConfig cfg)
    {
        if (!string.IsNullOrWhiteSpace(cfg.TtsVoice))
        {
            try { _synth.SelectVoice(cfg.TtsVoice); }
            catch (Exception ex) { Log.Warning(ex, "TTS ボイス '{Voice}' が見つかりません。OS 既定を使用します。", cfg.TtsVoice); }
        }
        _synth.Rate   = Math.Clamp(cfg.TtsRate,   -10, 10);
        _synth.Volume = Math.Clamp(cfg.TtsVolume, 0,   100);
    }

    /// <summary>テキストを WAV バイト列に変換する。失敗時は null を返す。</summary>
    public byte[]? Synthesize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var ms = new System.IO.MemoryStream();
            _synth.SetOutputToWaveStream(ms);
            _synth.Speak(text);
            _synth.SetOutputToNull();
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TTS 音声生成に失敗しました: {Text}", text[..Math.Min(text.Length, 50)]);
            return null;
        }
    }

    /// <summary>全ステップの WAV バイト列を生成して返す。パスワードステップは固定テキストを使用する。</summary>
    public byte[]?[] SynthesizeAll(IReadOnlyList<ManualStep> steps)
    {
        var result = new byte[]?[steps.Count];
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            // FR-V06-7: パスワードフィールドは固定テキスト
            bool isPassword = step.UiControlType is "Edit" &&
                              step.TriggerType == TriggerType.Keyboard &&
                              step.InputText == null && step.KeyCodes == null;
            string text = isPassword
                ? "パスワードを入力しました。"
                : step.DescriptionLlm ?? step.DescriptionRuleBased;
            result[i] = Synthesize(text);
        }
        return result;
    }

    /// <summary>WAV バイト列の再生時間（秒）を取得する。PCM WAV ヘッダーを解析する。</summary>
    public static double GetWavDurationSeconds(byte[] wav)
    {
        if (wav.Length < 44) return 0;
        try
        {
            // WAV ヘッダー: サンプルレート(offset 24, 4byte), チャンネル(22, 2byte), ビット深度(34, 2byte)
            int sampleRate  = BitConverter.ToInt32(wav, 24);
            short channels  = BitConverter.ToInt16(wav, 22);
            short bitsPerSample = BitConverter.ToInt16(wav, 34);
            int dataSize    = BitConverter.ToInt32(wav, 40);
            if (sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0) return 0;
            int bytesPerSample = bitsPerSample / 8;
            double totalSamples = (double)dataSize / (channels * bytesPerSample);
            return totalSamples / sampleRate;
        }
        catch { return 0; }
    }

    public void Dispose() => _synth.Dispose();
}
