using System.Text.Json;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>テキスト/JSON サイドカーログの出力</summary>
public class MetadataLogger
{
    private readonly ConfigStore _config;

    public MetadataLogger(ConfigStore config)
    {
        _config = config;
    }

    public async Task LogEventAsync(TriggerEvent evt, string imagePath)
    {
        try
        {
            string logDir = ResolveLogDir(imagePath);
            string logFile = Path.Combine(logDir, $"events_{evt.Timestamp:yyyy-MM-dd}.log");

            string line = $"{evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{evt.Type}\t" +
                          $"{evt.ActiveWindowTitle}\t{evt.ActiveProcessName}\t" +
                          $"({evt.CursorPosition.X},{evt.CursorPosition.Y})\tmonitor{evt.MonitorIndex}\t{imagePath}";

            await File.AppendAllTextAsync(logFile, line + Environment.NewLine);

            if (_config.Config.Metadata.StructuredOutput)
                await WriteStructuredAsync(evt, imagePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "メタデータログの書き込み失敗");
        }
    }

    // プロジェクトモード（images/ サブフォルダ）に対応したログ出力先を返す
    private static string ResolveLogDir(string imagePath)
    {
        string dir = Path.GetDirectoryName(imagePath)!;
        return string.Equals(Path.GetFileName(dir), "images", StringComparison.OrdinalIgnoreCase)
            ? (Path.GetDirectoryName(dir) ?? dir)
            : dir;
    }

    private async Task WriteStructuredAsync(TriggerEvent evt, string imagePath)
    {
        string logDir = ResolveLogDir(imagePath);
        var cfg = _config.Config.Metadata;

        if (cfg.StructuredFormat == StructuredFormat.JsonLines)
        {
            string jsonFile = Path.Combine(logDir, $"events_{evt.Timestamp:yyyy-MM-dd}.jsonl");
            var record = new
            {
                event_id = evt.EventId.ToString("N"),
                timestamp = evt.Timestamp.ToString("O"),
                trigger = evt.Type.ToString(),
                window_title = evt.ActiveWindowTitle,
                process = evt.ActiveProcessName,
                cursor_x = evt.CursorPosition.X,
                cursor_y = evt.CursorPosition.Y,
                monitor = evt.MonitorIndex,
                image_path = imagePath,
            };
            string json = JsonSerializer.Serialize(record);
            await File.AppendAllTextAsync(jsonFile, json + Environment.NewLine);
        }
        else // CSV
        {
            string csvFile = Path.Combine(logDir, $"events_{evt.Timestamp:yyyy-MM-dd}.csv");
            if (!File.Exists(csvFile))
                await File.WriteAllTextAsync(csvFile,
                    "event_id,timestamp,trigger,window_title,process,cursor_x,cursor_y,monitor,image_path\n");

            string row = string.Join(",",
                evt.EventId.ToString("N"),
                evt.Timestamp.ToString("O"),
                evt.Type,
                $"\"{evt.ActiveWindowTitle.Replace("\"", "\"\"")}\"",
                evt.ActiveProcessName,
                evt.CursorPosition.X,
                evt.CursorPosition.Y,
                evt.MonitorIndex,
                imagePath);
            await File.AppendAllTextAsync(csvFile, row + Environment.NewLine);
        }
    }
}
