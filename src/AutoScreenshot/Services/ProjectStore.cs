using System.Text.Json;
using System.Text.Json.Serialization;
using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>プロジェクトフォルダ（.ascproj）の作成・project.json 読み書き・一覧取得</summary>
public class ProjectStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConfigStore _config;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ProjectStore(ConfigStore config)
    {
        _config = config;
    }

    /// <summary>プロジェクトフォルダを作成し、初期 project.json を書き込む。</summary>
    public async Task<ProjectInfo> CreateProjectAsync(string title)
    {
        string saveFolder = _config.Config.Storage.SaveFolder;
        Directory.CreateDirectory(saveFolder);

        string slug = MakeSlug(title);
        string folderName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{slug}.ascproj";
        string projectFolder = Path.Combine(saveFolder, folderName);

        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(Path.Combine(projectFolder, "images"));
        Directory.CreateDirectory(Path.Combine(projectFolder, "thumbs"));
        Directory.CreateDirectory(Path.Combine(projectFolder, "exports"));

        var info = new ProjectInfo
        {
            Title = string.IsNullOrWhiteSpace(title)
                ? $"操作手順書 {DateTime.Now:yyyy-MM-dd HH:mm}"
                : title,
            ProjectFolder = projectFolder,
        };

        await WriteProjectJsonAsync(info);
        Log.Information("プロジェクト作成: {Folder}", projectFolder);
        return info;
    }

    /// <summary>project.json を原子的に書き込む（.tmp → rename）。</summary>
    public async Task WriteProjectJsonAsync(ProjectInfo info)
    {
        info.LastModifiedAt = DateTimeOffset.Now;
        string jsonPath = Path.Combine(info.ProjectFolder, "project.json");
        string tmpPath  = jsonPath + ".tmp";

        await _writeLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(info, s_jsonOptions);
            await File.WriteAllTextAsync(tmpPath, json, System.Text.Encoding.UTF8);
            File.Move(tmpPath, jsonPath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>project.json を読み込む。</summary>
    public static async Task<ProjectInfo?> ReadProjectJsonAsync(string projectFolder)
    {
        string jsonPath = Path.Combine(projectFolder, "project.json");
        if (!File.Exists(jsonPath)) return null;
        try
        {
            string json = await File.ReadAllTextAsync(jsonPath, System.Text.Encoding.UTF8);
            var info = JsonSerializer.Deserialize<ProjectInfo>(json, s_jsonOptions);
            if (info != null) info.ProjectFolder = projectFolder;
            return info;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "project.json 読み込み失敗: {Path}", jsonPath);
            return null;
        }
    }

    /// <summary>SaveFolder 直下の .ascproj フォルダを作成日時降順で列挙する。</summary>
    public async Task<List<ProjectInfo>> ListProjectsAsync()
    {
        string saveFolder = _config.Config.Storage.SaveFolder;
        if (!Directory.Exists(saveFolder)) return [];

        var folders = Directory.GetDirectories(saveFolder, "*.ascproj")
            .OrderByDescending(f => Directory.GetCreationTime(f))
            .ToList();

        var result = new List<ProjectInfo>();
        foreach (var folder in folders)
        {
            var info = await ReadProjectJsonAsync(folder);
            if (info != null) result.Add(info);
        }
        return result;
    }

    /// <summary>ステップをプロジェクトに追記して project.json を更新する。</summary>
    public async Task AppendStepAsync(ProjectInfo info, ProjectStep step)
    {
        info.Steps.Add(step);
        await WriteProjectJsonAsync(info);
    }

    /// <summary>エクスポート記録を追記して project.json を更新する。</summary>
    public async Task RecordExportAsync(ProjectInfo info, ExportRecord record)
    {
        info.ExportHistory.Add(record);
        await WriteProjectJsonAsync(info);
    }

    private static string MakeSlug(string title)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in title)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else sb.Append('_');
        }
        string s = sb.ToString().Trim('_');
        return s.Length > 40 ? s[..40] : (s.Length == 0 ? "project" : s);
    }
}
