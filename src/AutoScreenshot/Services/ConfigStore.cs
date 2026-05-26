using System.Text.Json;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>設定の読み込み・保存・変更通知</summary>
public class ConfigStore
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private AppConfig _config = new();

    public AppConfig Config => _config;

    public event EventHandler? ConfigChanged;

    private string GetConfigPath()
    {
        // ポータブル運用: exe 同一フォルダの config.json を優先
        string exeDir = AppContext.BaseDirectory;
        string portablePath = Path.Combine(exeDir, "config.json");
        if (File.Exists(portablePath))
            return portablePath;

        string appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoScreenshot", "config.json");
        return appDataPath;
    }

    public void Load()
    {
        string path = GetConfigPath();
        if (!File.Exists(path))
        {
            Log.Information("設定ファイルが存在しないためデフォルト設定を使用: {Path}", path);
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            _config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
            Log.Information("設定を読み込みました: {Path}", path);
            // モデルに新規追加されたフィールドを config.json に書き出す
            Save();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "設定の読み込みに失敗しました。デフォルト設定を使用します: {Path}", path);
            _config = new AppConfig();
        }
    }

    public void Save()
    {
        string path = GetConfigPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(_config, _jsonOptions);
            File.WriteAllText(path, json);
            Log.Debug("設定を保存しました: {Path}", path);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "設定の保存に失敗しました: {Path}", path);
        }
    }

    public void Update(Action<AppConfig> updater)
    {
        updater(_config);
        Save();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
