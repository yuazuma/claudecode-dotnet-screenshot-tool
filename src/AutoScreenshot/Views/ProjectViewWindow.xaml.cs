using System.Windows;
using System.Windows.Media.Imaging;
using AutoScreenshot.Models;
using AutoScreenshot.Services;
using Serilog;
using WinMessageBox = System.Windows.MessageBox;
using WinSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace AutoScreenshot.Views;

public partial class ProjectViewWindow : Window
{
    private readonly ConfigStore _config;
    private readonly ProjectStore _projectStore;
    private readonly ExportService _exportService;

    private List<ProjectInfo> _projects = [];
    private ProjectInfo? _selectedProject;
    private List<StepViewModel> _stepVms = [];
    private int _selectedStepIndex = -1;

    public ProjectViewWindow(ConfigStore config, ProjectStore projectStore, ExportService exportService)
    {
        InitializeComponent();
        _config = config;
        _projectStore = projectStore;
        _exportService = exportService;
        Loaded += async (_, _) => await RefreshProjectListAsync();
    }

    // ---- プロジェクト一覧 ----

    private async Task RefreshProjectListAsync()
    {
        SetStatus("プロジェクト一覧を読み込み中...");
        _projects = await _projectStore.ListProjectsAsync();
        LstProjects.ItemsSource = _projects;
        SetStatus($"{_projects.Count} 件のプロジェクト");
    }

    private void LstProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedProject = LstProjects.SelectedItem as ProjectInfo;
        if (_selectedProject == null)
        {
            TxtProjectTitle.Text = "（プロジェクトを選択してください）";
            LstSteps.ItemsSource = null;
            return;
        }

        TxtProjectTitle.Text = _selectedProject.Title;
        LoadSteps(_selectedProject);
    }

    // ---- ステップ一覧 ----

    private void LoadSteps(ProjectInfo project)
    {
        _stepVms = project.Steps.Select((s, i) => new StepViewModel(s, project.ProjectFolder)).ToList();
        LstSteps.ItemsSource = _stepVms;
        _selectedStepIndex = -1;
        UpdateStepDetail();
    }

    private void LstSteps_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selectedStepIndex = LstSteps.SelectedIndex;
        UpdateStepDetail();
    }

    private void UpdateStepDetail()
    {
        if (_selectedStepIndex < 0 || _selectedStepIndex >= _stepVms.Count)
        {
            TxtStepInfo.Text = "";
            TxtDescription.Text = "";
            TxtStepNav.Text = "";
            return;
        }

        var vm = _stepVms[_selectedStepIndex];
        TxtStepInfo.Text = $"ステップ {vm.Step.StepNumber} — {vm.Step.TriggerType} — {vm.Step.Timestamp.LocalDateTime:HH:mm:ss}" +
                           (vm.Step.IsDeleted ? " [削除済み]" : "");
        TxtDescription.Text = vm.Step.DescriptionOverride ?? vm.Step.DescriptionLlm ?? vm.Step.DescriptionRuleBased;
        TxtStepNav.Text = $"{_selectedStepIndex + 1} / {_stepVms.Count}";
    }

    // ---- ステップ操作 ----

    private async void BtnConfirmDesc_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        var step = _selectedProject.Steps[_selectedStepIndex];
        string val = TxtDescription.Text.Trim();
        step.DescriptionOverride = string.IsNullOrEmpty(val) ? null : val;

        await SaveProjectAsync();
        SetStatus("説明文を更新しました。");
    }

    private async void BtnDeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        var step = _selectedProject.Steps[_selectedStepIndex];
        if (step.IsDeleted)
        {
            SetStatus("すでに削除済みです。");
            return;
        }

        var result = WinMessageBox.Show(
            $"ステップ {step.StepNumber} を削除しますか？\n画像は _deleted/ フォルダに移動されます。",
            "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        step.IsDeleted = true;

        // 画像を _deleted/ へ移動
        if (step.ImagePath != null)
        {
            MoveToDeleted(_selectedProject.ProjectFolder, step.ImagePath, "images/_deleted");
            step.ImagePath = step.ImagePath.Replace("images/", "images/_deleted/");
        }
        if (step.ThumbPath != null)
        {
            MoveToDeleted(_selectedProject.ProjectFolder, step.ThumbPath, "thumbs/_deleted");
            step.ThumbPath = step.ThumbPath.Replace("thumbs/", "thumbs/_deleted/");
        }

        await SaveProjectAsync();
        LoadSteps(_selectedProject);
        SetStatus($"ステップ {step.StepNumber} を削除しました。");
    }

    private static void MoveToDeleted(string projectFolder, string relPath, string deletedSubDir)
    {
        try
        {
            string src = Path.Combine(projectFolder, relPath.Replace('/', '\\'));
            string deletedDir = Path.Combine(projectFolder, deletedSubDir.Replace('/', '\\'));
            Directory.CreateDirectory(deletedDir);
            string dest = Path.Combine(deletedDir, Path.GetFileName(src));
            if (File.Exists(src)) File.Move(src, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "削除ファイル移動失敗: {Path}", relPath);
        }
    }

    private void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStepIndex > 0)
        {
            _selectedStepIndex--;
            LstSteps.SelectedIndex = _selectedStepIndex;
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStepIndex < _stepVms.Count - 1)
        {
            _selectedStepIndex++;
            LstSteps.SelectedIndex = _selectedStepIndex;
        }
    }

    // ---- エクスポート ----

    private async void BtnExportImages_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("画像をエクスポート中...");
        await _exportService.ExportImagesAsync(_selectedProject);
        SetStatus("画像エクスポート完了。");
    }

    private async void BtnExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("Markdown 手順書を生成中...");
        await _exportService.ExportMarkdownAsync(_selectedProject);
        SetStatus("Markdown エクスポート完了。");
    }

    private async void BtnExportDocx_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("Word 手順書を生成中...");
        await _exportService.ExportDocxAsync(_selectedProject);
        SetStatus("Word エクスポート完了。");
    }

    private void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        _ = _exportService.ExportVideoAsync(_selectedProject);
        SetStatus("動画生成をバックグラウンドで開始しました。");
    }

    private async void BtnExportZip_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var dlg = new WinSaveFileDialog
        {
            Title = "ZIP の保存先を選択",
            Filter = "ZIP アーカイブ (*.zip)|*.zip",
            FileName = $"{Path.GetFileNameWithoutExtension(_selectedProject.ProjectFolder)}.zip",
        };
        if (dlg.ShowDialog() != true) return;

        SetStatus("ZIP を作成中...");
        await _exportService.ExportZipAsync(_selectedProject, dlg.FileName);
        SetStatus($"ZIP エクスポート完了: {dlg.FileName}");
    }

    // ---- その他 ----

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        System.Diagnostics.Process.Start("explorer.exe", _selectedProject.ProjectFolder);
    }

    private async Task SaveProjectAsync()
    {
        if (_selectedProject == null) return;
        try
        {
            await _projectStore.WriteProjectJsonAsync(_selectedProject);
            // ViewModel を再構築
            if (_selectedProject != null) LoadSteps(_selectedProject);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "project.json 保存失敗");
            WinMessageBox.Show("保存中にエラーが発生しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetStatus(string msg) => TxtStatus.Text = msg;
}

// ---- ViewModel ----

internal class StepViewModel
{
    public ProjectStep Step { get; }
    private readonly string _projectFolder;
    private BitmapImage? _thumbImage;

    public StepViewModel(ProjectStep step, string projectFolder)
    {
        Step = step;
        _projectFolder = projectFolder;
    }

    public string StepLabel => $"#{Step.StepNumber}" + (Step.IsDeleted ? " [削除]" : "");

    public string EffectiveDescription => Step.EffectiveDescription;

    public BitmapImage? ThumbImageSource
    {
        get
        {
            if (_thumbImage != null) return _thumbImage;
            if (Step.ThumbPath == null) return null;
            try
            {
                string path = Path.Combine(_projectFolder, Step.ThumbPath.Replace('/', '\\'));
                if (!File.Exists(path)) return null;
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(path, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                _thumbImage = img;
                return img;
            }
            catch { return null; }
        }
    }

    public double Opacity => Step.IsDeleted ? 0.4 : 1.0;
}
