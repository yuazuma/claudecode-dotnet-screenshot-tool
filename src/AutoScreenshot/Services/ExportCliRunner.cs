using AutoScreenshot.Models;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>
/// コマンドラインエクスポートモードの実行エンジン（--export フラグ用）。
/// UI を表示せずにプロジェクトを指定種別でエクスポートして終了する。
/// </summary>
public static class ExportCliRunner
{
    // ---- 使い方 ---------------------------------------------------------------

    public static string Usage => """
        使い方:
          AutoScreenshot.exe --export <project_path> [--type <types>] [--zip-out <path>]

        オプション:
          --export <path>     エクスポートするプロジェクトのフォルダパス
                              (.ascproj フォルダまたは project.json のどちらでも可)
          --type <types>      エクスポート種別をカンマ区切りで指定
                              省略時: md,html,docx,video,images,zip
                              指定例: --type md,video
          --zip-out <path>    ZIP 出力先ファイルパス（--type に zip が含まれる場合）
                              省略時: <project_folder>/<title>.zip

        エクスポート種別:
          md       Markdown 手順書
          html     HTML 手順書
          docx     Word 手順書
          video    動画 (APNG/MP4)
          images   画像を exports/ フォルダに書き出す
          zip      プロジェクトを ZIP に圧縮

        終了コード:
          0  すべて成功
          1  1件以上のエクスポートが失敗
          2  引数エラー（プロジェクトが見つからない等）
        """;

    // ---- エントリポイント -----------------------------------------------------

    /// <summary>
    /// エクスポートを実行して終了コードを返す。
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        // --export の値（プロジェクトパス）
        int exportIdx = Array.IndexOf(args, "--export");
        if (exportIdx < 0 || exportIdx + 1 >= args.Length)
        {
            Console.Error.WriteLine("エラー: --export の後にプロジェクトパスを指定してください。");
            Console.Error.WriteLine(Usage);
            return 2;
        }

        string rawPath = args[exportIdx + 1];

        // project.json が指定された場合はその親フォルダをプロジェクトフォルダとして扱う
        string projectFolder = rawPath.EndsWith("project.json", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(rawPath)!
            : rawPath;

        if (!Directory.Exists(projectFolder))
        {
            Console.Error.WriteLine($"エラー: プロジェクトフォルダが見つかりません: {projectFolder}");
            return 2;
        }

        // --type の解析
        var types = ParseTypes(args);

        // --zip-out の解析
        string? zipOut = null;
        int zipIdx = Array.IndexOf(args, "--zip-out");
        if (zipIdx >= 0 && zipIdx + 1 < args.Length)
            zipOut = args[zipIdx + 1];

        // プロジェクト読み込み
        var project = await ProjectStore.ReadProjectJsonAsync(projectFolder);
        if (project == null)
        {
            Log.Error("プロジェクトを読み込めませんでした: {Path}", projectFolder);
            Console.Error.WriteLine($"エラー: project.json の読み込みに失敗しました。");
            return 2;
        }

        Log.Information("[CLI] エクスポート開始: {Title}  types={Types}",
            project.Title, string.Join(",", types));
        Console.WriteLine($"プロジェクト: {project.Title}  ({project.Steps.Count} ステップ)");
        Console.WriteLine($"エクスポート種別: {string.Join(", ", types)}");
        Console.WriteLine();

        // サービス初期化（通知なし・進捗なし）
        var config       = new ConfigStore();
        config.Load();
        var projectStore = new ProjectStore(config);
        var videoGen     = new VideoGenerator(config, null);
        var exporter     = new ExportService(config, projectStore, videoGen, null);

        // 実行
        int errorCount = 0;
        if (types.Contains("md"))      errorCount += await RunOne("Markdown",  () => exporter.ExportMarkdownAsync(project));
        if (types.Contains("html"))    errorCount += await RunOne("HTML",       () => exporter.ExportHtmlAsync(project));
        if (types.Contains("docx"))    errorCount += await RunOne("Word",       () => exporter.ExportDocxAsync(project));
        if (types.Contains("images"))  errorCount += await RunOne("画像",       () => exporter.ExportImagesAsync(project));
        if (types.Contains("video"))   errorCount += await RunOne("動画",       () => exporter.ExportVideoAsync(project));
        if (types.Contains("zip"))
        {
            zipOut ??= Path.Combine(
                Path.GetDirectoryName(projectFolder)!,
                Path.GetFileNameWithoutExtension(projectFolder) + ".zip");
            errorCount += await RunOne("ZIP", () => exporter.ExportZipAsync(project, zipOut));
            if (errorCount == 0)
                Console.WriteLine($"  → {zipOut}");
        }

        Console.WriteLine();
        if (errorCount == 0)
        {
            Console.WriteLine("エクスポート完了 ✓");
            Log.Information("[CLI] エクスポート完了");
        }
        else
        {
            Console.Error.WriteLine($"エクスポート完了 ({errorCount} 件のエラーあり)");
            Log.Warning("[CLI] エクスポート完了 (エラー {Count} 件)", errorCount);
        }

        return errorCount == 0 ? 0 : 1;
    }

    // ---- ヘルパー -------------------------------------------------------------

    private static HashSet<string> ParseTypes(string[] args)
    {
        int typeIdx = Array.IndexOf(args, "--type");
        if (typeIdx >= 0 && typeIdx + 1 < args.Length)
        {
            return new HashSet<string>(
                args[typeIdx + 1].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim().ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }
        // 省略時は全種別
        return new HashSet<string>(
            ["md", "html", "docx", "video", "images", "zip"],
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<int> RunOne(string label, Func<Task> action)
    {
        Console.Write($"  {label,-10} ... ");
        try
        {
            await action();
            Console.WriteLine("OK");
            Log.Information("[CLI] {Label}: 完了", label);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAILED  ({ex.Message})");
            Log.Error(ex, "[CLI] {Label}: 失敗", label);
            return 1;
        }
    }
}
