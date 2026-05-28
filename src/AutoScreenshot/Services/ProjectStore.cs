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
        catch
        {
            try { File.Delete(tmpPath); } catch { }
            throw;
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

    /// <summary>複数プロジェクトを時系列順に結合した新プロジェクトを生成する（FR-E01）。元プロジェクトは変更しない。</summary>
    public async Task<ProjectInfo> MergeProjectsAsync(IEnumerable<ProjectInfo> sources, string newTitle)
    {
        var sourceList = sources.OrderBy(p => p.CreatedAt).ToList();
        var merged = await CreateProjectAsync(newTitle);

        int stepNum = 1;
        foreach (var src in sourceList)
        {
            foreach (var step in src.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.StepNumber))
            {
                var newStep = CloneStep(step, stepNum);

                newStep.ImagePath = CopyImageFile(src.ProjectFolder, step.ImagePath, merged.ProjectFolder, "images", stepNum);
                newStep.ThumbPath = CopyImageFile(src.ProjectFolder, step.ThumbPath, merged.ProjectFolder, "thumbs", stepNum);

                merged.Steps.Add(newStep);
                stepNum++;
            }
        }

        await WriteProjectJsonAsync(merged);
        Log.Information("プロジェクト結合完了: {Title} ({Count} ステップ)", newTitle, merged.Steps.Count);
        return merged;
    }

    /// <summary>指定ステップ番号で既存プロジェクトを 2 つに分割する（FR-E02）。元プロジェクトは変更しない。</summary>
    public async Task<(ProjectInfo before, ProjectInfo after)> SplitProjectAsync(
        ProjectInfo source, int splitAtStepNumber, string titleBefore, string titleAfter)
    {
        var before = await CreateProjectAsync(titleBefore);
        var after = await CreateProjectAsync(titleAfter);

        var activeSteps = source.Steps.Where(s => !s.IsDeleted).OrderBy(s => s.StepNumber).ToList();
        int numBefore = 1, numAfter = 1;
        foreach (var step in activeSteps)
        {
            bool isAfter = step.StepNumber >= splitAtStepNumber;
            var dest = isAfter ? after : before;
            int num = isAfter ? numAfter++ : numBefore++;
            var newStep = CloneStep(step, num);
            newStep.ImagePath = CopyImageFile(source.ProjectFolder, step.ImagePath, dest.ProjectFolder, "images", num);
            newStep.ThumbPath = CopyImageFile(source.ProjectFolder, step.ThumbPath, dest.ProjectFolder, "thumbs", num);
            dest.Steps.Add(newStep);
        }

        await WriteProjectJsonAsync(before);
        await WriteProjectJsonAsync(after);
        Log.Information("プロジェクト分割完了: {B} / {A}", titleBefore, titleAfter);
        return (before, after);
    }

    private static string? CopyImageFile(string srcFolder, string? relPath, string destFolder, string subDir, int stepNum)
    {
        if (relPath == null) return null;
        string src = Path.Combine(srcFolder, relPath.Replace('/', '\\'));
        if (!File.Exists(src)) return null;
        string ext = Path.GetExtension(src);
        string newName = $"step_{stepNum:D3}{ext}";
        string destDir = Path.Combine(destFolder, subDir);
        Directory.CreateDirectory(destDir);
        string dest = Path.Combine(destDir, newName);
        File.Copy(src, dest, overwrite: true);
        return $"{subDir}/{newName}";
    }

    private static ProjectStep CloneStep(ProjectStep s, int newStepNumber) => new()
    {
        StepNumber           = newStepNumber,
        Timestamp            = s.Timestamp,
        TriggerType          = s.TriggerType,
        UiElementName        = s.UiElementName,
        UiControlType        = s.UiControlType,
        CursorX              = s.CursorX,
        CursorY              = s.CursorY,
        WindowTitle          = s.WindowTitle,
        ProcessName          = s.ProcessName,
        InputText            = s.InputText,
        KeyCodes             = s.KeyCodes,
        DescriptionRuleBased = s.DescriptionRuleBased,
        DescriptionLlm       = s.DescriptionLlm,
        DescriptionOverride  = s.DescriptionOverride,
        NeedsReview          = s.NeedsReview,
        Annotations          = s.Annotations?.Select(a => new AnnotationItem
        {
            Type = a.Type, X = a.X, Y = a.Y, X2 = a.X2, Y2 = a.Y2, Label = a.Label, Color = a.Color
        }).ToList(),
    };

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
