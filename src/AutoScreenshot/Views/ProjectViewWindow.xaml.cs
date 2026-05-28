using System.Windows;
using System.Windows.Media.Imaging;
using AutoScreenshot.Models;
using AutoScreenshot.Services;
using Serilog;
using WinBrushes = System.Windows.Media.Brushes;
using WinDragEventArgs = System.Windows.DragEventArgs;
using WinButton = System.Windows.Controls.Button;
using WinCanvas = System.Windows.Controls.Canvas;
using WinColor = System.Windows.Media.Color;
using WinColorConverter = System.Windows.Media.ColorConverter;
using WinDoubleCollection = System.Windows.Media.DoubleCollection;
using WinEllipse = System.Windows.Shapes.Ellipse;
using WinLine = System.Windows.Shapes.Line;
using WinMessageBox = System.Windows.MessageBox;
using WinMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WinMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WinPoint = System.Windows.Point;
using WinPointCollection = System.Windows.Media.PointCollection;
using WinPolygon = System.Windows.Shapes.Polygon;
using WinRectangle = System.Windows.Shapes.Rectangle;
using WinSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WinSize = System.Windows.Size;
using WinSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WinStackPanel = System.Windows.Controls.StackPanel;
using WinTextBlock = System.Windows.Controls.TextBlock;
using WinTextBox = System.Windows.Controls.TextBox;

namespace AutoScreenshot.Views;

public partial class ProjectViewWindow : Window
{
    private readonly ConfigStore _config;
    private readonly ProjectStore _projectStore;
    private readonly ExportService _exportService;

    private List<ProjectInfo> _projects = [];
    private List<ProjectInfo> _allProjects = [];
    private readonly HashSet<string> _selectedTags = [];
    private ProjectInfo? _selectedProject;
    private List<StepViewModel> _stepVms = [];
    private int _selectedStepIndex = -1;
    private int _dragStepSourceIndex = -1;
    private WinPoint _dragStartPoint;

    // ---- アノテーション ----
    private string _annTool = "Number";
    private string _annColor = "#FF0000";
    private readonly List<AnnotationItem> _pendingAnnotations = [];
    private bool _annDragging;
    private WinPoint _annDragStart;
    private UIElement? _annPreview;
    private int _nextBadgeNum = 1;
    private BitmapImage? _annImageSource;

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
        _allProjects = await _projectStore.ListProjectsAsync();
        _selectedTags.Clear();
        RefreshTagPanel();
        FilterAndDisplayProjects();
        SetStatus($"{_projects.Count} 件のプロジェクト");
    }

    private void FilterAndDisplayProjects()
    {
        string search = TxtSearch?.Text?.Trim() ?? "";
        var filtered = _allProjects.AsEnumerable();

        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(p =>
                p.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                p.Steps.Any(s => s.EffectiveDescription.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (_selectedTags.Count > 0)
            filtered = filtered.Where(p => _selectedTags.All(t => p.Tags.Contains(t)));

        _projects = filtered.ToList();
        LstProjects.ItemsSource = null;
        LstProjects.ItemsSource = _projects;
    }

    private void RefreshTagPanel()
    {
        TagPanel.Children.Clear();
        var allTags = _allProjects.SelectMany(p => p.Tags).Distinct().OrderBy(t => t).ToList();
        foreach (var tag in allTags)
        {
            bool selected = _selectedTags.Contains(tag);
            var btn = new WinButton
            {
                Content = tag,
                Margin = new Thickness(0, 0, 3, 3),
                Padding = new Thickness(6, 2, 6, 2),
                FontSize = 10,
                FontWeight = selected ? FontWeights.Bold : FontWeights.Normal,
                BorderThickness = new Thickness(selected ? 2 : 1),
                Tag = tag,
            };
            btn.Click += (s, _) =>
            {
                if (s is WinButton b && b.Tag is string t)
                {
                    if (_selectedTags.Contains(t)) _selectedTags.Remove(t);
                    else _selectedTags.Add(t);
                    RefreshTagPanel();
                    FilterAndDisplayProjects();
                }
            };
            TagPanel.Children.Add(btn);
        }
    }

    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => FilterAndDisplayProjects();

    private void BtnSearchClear_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = "";
        FilterAndDisplayProjects();
    }

    private void ChkShowDeleted_Changed(object sender, RoutedEventArgs e)
    {
        if (_selectedProject != null) LoadSteps(_selectedProject);
    }

    private void LstProjects_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        BtnMergeProjects.IsEnabled = LstProjects.SelectedItems.Count >= 2;

        _selectedProject = LstProjects.SelectedItem as ProjectInfo;
        BtnExportMenu.IsEnabled = _selectedProject != null;

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
        bool showDeleted = ChkShowDeleted?.IsChecked == true;
        var steps = project.Steps.AsEnumerable();
        if (!showDeleted) steps = steps.Where(s => !s.IsDeleted);
        _stepVms = steps.Select((s, i) => new StepViewModel(s, project.ProjectFolder)).ToList();
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
            LoadAnnotationImage();
            return;
        }

        var vm = _stepVms[_selectedStepIndex];
        TxtStepInfo.Text = $"ステップ {vm.Step.StepNumber} — {vm.Step.TriggerType} — {vm.Step.Timestamp.LocalDateTime:HH:mm:ss}" +
                           (vm.Step.IsDeleted ? " [削除済み]" : "");
        TxtDescription.Text = vm.Step.DescriptionOverride ?? vm.Step.DescriptionLlm ?? vm.Step.DescriptionRuleBased;
        TxtStepNav.Text = $"{_selectedStepIndex + 1} / {_stepVms.Count}";
        LoadAnnotationImage();
    }

    // ---- アノテーション ----

    private void LoadAnnotationImage()
    {
        ImgAnnotation.Source = null;
        AnnCanvas.Children.Clear();
        _pendingAnnotations.Clear();
        _annImageSource = null;
        _nextBadgeNum = 1;

        if (_selectedStepIndex < 0 || _selectedStepIndex >= _stepVms.Count || _selectedProject == null) return;
        var step = _stepVms[_selectedStepIndex].Step;
        if (step.ImagePath == null) return;

        string path = Path.Combine(_selectedProject.ProjectFolder, step.ImagePath.Replace('/', '\\'));
        if (!File.Exists(path)) return;

        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            _annImageSource = img;
            ImgAnnotation.Source = img;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "アノテーション画像読み込み失敗: {Path}", path);
            return;
        }

        if (step.Annotations != null)
        {
            _pendingAnnotations.AddRange(step.Annotations.Select(a => new AnnotationItem
            {
                Type = a.Type, X = a.X, Y = a.Y, X2 = a.X2, Y2 = a.Y2, Label = a.Label, Color = a.Color
            }));
            _nextBadgeNum = _pendingAnnotations.Count(a => a.Type == "Number") + 1;
        }

        ExpAnnotation.IsExpanded = _pendingAnnotations.Count > 0;
        Dispatcher.InvokeAsync(RenderAnnotationsOnCanvas, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void AnnCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => RenderAnnotationsOnCanvas();

    private void RenderAnnotationsOnCanvas()
    {
        AnnCanvas.Children.Clear();
        foreach (var a in _pendingAnnotations)
            DrawAnnotationOnCanvas(a);
    }

    private void DrawAnnotationOnCanvas(AnnotationItem a)
    {
        var color = (WinColor)WinColorConverter.ConvertFromString(a.Color);
        var brush = new WinSolidColorBrush(color);

        switch (a.Type)
        {
            case "Number":
            {
                var p = ImageToCanvas(a.X, a.Y);
                const double r = 12;
                var circle = new WinEllipse
                {
                    Width = r * 2, Height = r * 2,
                    Fill = brush, Stroke = WinBrushes.White, StrokeThickness = 1.5,
                };
                WinCanvas.SetLeft(circle, p.X - r);
                WinCanvas.SetTop(circle, p.Y - r);
                AnnCanvas.Children.Add(circle);

                var tb = new WinTextBlock
                {
                    Text = a.Label ?? "?",
                    Width = r * 2, Height = r * 2,
                    TextAlignment = TextAlignment.Center,
                    Foreground = WinBrushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Padding = new Thickness(0, 4, 0, 0),
                };
                WinCanvas.SetLeft(tb, p.X - r);
                WinCanvas.SetTop(tb, p.Y - r);
                AnnCanvas.Children.Add(tb);
                break;
            }
            case "Arrow":
            {
                var p1 = ImageToCanvas(a.X, a.Y);
                var p2 = ImageToCanvas(a.X2, a.Y2);
                var line = new WinLine
                {
                    X1 = p1.X, Y1 = p1.Y, X2 = p2.X, Y2 = p2.Y,
                    Stroke = brush, StrokeThickness = 2.5,
                };
                AnnCanvas.Children.Add(line);

                double angle = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                const double headLen = 12;
                const double headAngle = 0.45;
                var head = new WinPolygon
                {
                    Points = new WinPointCollection
                    {
                        p2,
                        new(p2.X - headLen * Math.Cos(angle - headAngle), p2.Y - headLen * Math.Sin(angle - headAngle)),
                        new(p2.X - headLen * Math.Cos(angle + headAngle), p2.Y - headLen * Math.Sin(angle + headAngle)),
                    },
                    Fill = brush,
                };
                AnnCanvas.Children.Add(head);
                break;
            }
            case "Rect":
            {
                var p1 = ImageToCanvas(a.X, a.Y);
                var p2 = ImageToCanvas(a.X2, a.Y2);
                double rx = Math.Min(p1.X, p2.X), ry = Math.Min(p1.Y, p2.Y);
                double rw = Math.Abs(p2.X - p1.X), rh = Math.Abs(p2.Y - p1.Y);
                if (rw < 2 || rh < 2) break;
                var rect = new WinRectangle
                {
                    Width = rw, Height = rh,
                    Fill = new WinSolidColorBrush(WinColor.FromArgb(50, color.R, color.G, color.B)),
                    Stroke = brush, StrokeThickness = 2,
                    StrokeDashArray = new WinDoubleCollection { 5, 3 },
                };
                WinCanvas.SetLeft(rect, rx);
                WinCanvas.SetTop(rect, ry);
                AnnCanvas.Children.Add(rect);
                break;
            }
            case "Callout":
            {
                var p = ImageToCanvas(a.X, a.Y);
                string text = a.Label ?? "";
                if (string.IsNullOrEmpty(text)) break;

                var tb = new WinTextBlock
                {
                    Text = text, FontSize = 11,
                    Foreground = WinBrushes.White,
                    Background = brush,
                    Padding = new Thickness(6, 3, 6, 3),
                };
                tb.Measure(new WinSize(double.PositiveInfinity, double.PositiveInfinity));
                double bh = tb.DesiredSize.Height;

                var tri = new WinPolygon
                {
                    Points = new WinPointCollection
                    {
                        new(p.X + 8, p.Y - 4),
                        new(p.X + 20, p.Y - 4),
                        new(p.X, p.Y),
                    },
                    Fill = brush,
                };
                WinCanvas.SetLeft(tb, p.X + 8);
                WinCanvas.SetTop(tb, p.Y - bh - 4);
                AnnCanvas.Children.Add(tri);
                AnnCanvas.Children.Add(tb);
                break;
            }
        }
    }

    private (double scale, double offsetX, double offsetY) GetAnnTransform()
    {
        if (_annImageSource == null) return (1, 0, 0);
        double nw = _annImageSource.PixelWidth;
        double nh = _annImageSource.PixelHeight;
        double cw = AnnCanvas.ActualWidth;
        double ch = AnnCanvas.ActualHeight;
        if (nw <= 0 || nh <= 0 || cw <= 0 || ch <= 0) return (1, 0, 0);
        double scale = Math.Min(cw / nw, ch / nh);
        double ox = (cw - nw * scale) / 2;
        double oy = (ch - nh * scale) / 2;
        return (scale, ox, oy);
    }

    private WinPoint ImageToCanvas(int ix, int iy)
    {
        var (s, ox, oy) = GetAnnTransform();
        return new WinPoint(ix * s + ox, iy * s + oy);
    }

    private (int x, int y) CanvasToImage(WinPoint cp)
    {
        var (s, ox, oy) = GetAnnTransform();
        if (s <= 0) return (0, 0);
        return ((int)((cp.X - ox) / s), (int)((cp.Y - oy) / s));
    }

    private void AnnCanvas_MouseLeftButtonDown(object sender, WinMouseButtonEventArgs e)
    {
        if (_annImageSource == null || _selectedStepIndex < 0) return;
        var cp = e.GetPosition(AnnCanvas);
        var (ix, iy) = CanvasToImage(cp);

        if (_annTool == "Number")
        {
            string label = _nextBadgeNum.ToString();
            _nextBadgeNum++;
            _pendingAnnotations.Add(new AnnotationItem { Type = "Number", X = ix, Y = iy, Label = label, Color = _annColor });
            RenderAnnotationsOnCanvas();
        }
        else if (_annTool == "Callout")
        {
            var dlg = new InputDialog("吹き出しに表示するテキストを入力してください") { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.InputText))
            {
                _pendingAnnotations.Add(new AnnotationItem { Type = "Callout", X = ix, Y = iy, Label = dlg.InputText.Trim(), Color = _annColor });
                RenderAnnotationsOnCanvas();
            }
        }
        else
        {
            _annDragging = true;
            _annDragStart = cp;
            AnnCanvas.CaptureMouse();
        }
    }

    private void AnnCanvas_MouseMove(object sender, WinMouseEventArgs e)
    {
        if (!_annDragging) return;
        var cp = e.GetPosition(AnnCanvas);

        if (_annPreview != null) { AnnCanvas.Children.Remove(_annPreview); _annPreview = null; }

        var color = (WinColor)WinColorConverter.ConvertFromString(_annColor);
        var brush = new WinSolidColorBrush(color);

        if (_annTool == "Arrow")
        {
            _annPreview = new WinLine
            {
                X1 = _annDragStart.X, Y1 = _annDragStart.Y, X2 = cp.X, Y2 = cp.Y,
                Stroke = brush, StrokeThickness = 2,
                StrokeDashArray = new WinDoubleCollection { 4, 3 },
            };
        }
        else if (_annTool == "Rect")
        {
            double rx = Math.Min(_annDragStart.X, cp.X), ry = Math.Min(_annDragStart.Y, cp.Y);
            double rw = Math.Abs(cp.X - _annDragStart.X), rh = Math.Abs(cp.Y - _annDragStart.Y);
            var rect = new WinRectangle
            {
                Width = rw, Height = rh,
                Fill = new WinSolidColorBrush(WinColor.FromArgb(40, color.R, color.G, color.B)),
                Stroke = brush, StrokeThickness = 2,
            };
            WinCanvas.SetLeft(rect, rx);
            WinCanvas.SetTop(rect, ry);
            _annPreview = rect;
        }

        if (_annPreview != null) AnnCanvas.Children.Add(_annPreview);
    }

    private void AnnCanvas_MouseLeftButtonUp(object sender, WinMouseButtonEventArgs e)
    {
        if (!_annDragging) return;
        _annDragging = false;
        AnnCanvas.ReleaseMouseCapture();

        if (_annPreview != null) { AnnCanvas.Children.Remove(_annPreview); _annPreview = null; }

        var cp = e.GetPosition(AnnCanvas);
        var (ix1, iy1) = CanvasToImage(_annDragStart);
        var (ix2, iy2) = CanvasToImage(cp);

        if (Math.Abs(ix2 - ix1) < 5 && Math.Abs(iy2 - iy1) < 5) return;

        _pendingAnnotations.Add(new AnnotationItem
        {
            Type = _annTool, X = ix1, Y = iy1, X2 = ix2, Y2 = iy2, Color = _annColor
        });
        RenderAnnotationsOnCanvas();
    }

    private void BtnTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WinButton btn && btn.Tag is string tool)
        {
            _annTool = tool;
            UpdateToolHighlight();
        }
    }

    private void UpdateToolHighlight()
    {
        foreach (var (btn, name) in new[] {
            (BtnToolNumber, "Number"), (BtnToolArrow, "Arrow"),
            (BtnToolRect, "Rect"), (BtnToolCallout, "Callout") })
        {
            btn.FontWeight = _annTool == name ? FontWeights.Bold : FontWeights.Normal;
            btn.BorderThickness = new Thickness(_annTool == name ? 2 : 1);
        }
    }

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WinButton btn && btn.Tag is string color)
        {
            _annColor = color;
            UpdateColorHighlight();
        }
    }

    private void UpdateColorHighlight()
    {
        foreach (var btn in new[] { BtnColorRed, BtnColorBlue, BtnColorYellow, BtnColorGreen })
        {
            bool selected = (string)btn.Tag == _annColor;
            btn.BorderThickness = new Thickness(selected ? 3 : 1);
            btn.BorderBrush = selected ? WinBrushes.Black : WinBrushes.Gray;
        }
    }

    private void BtnAnnClear_Click(object sender, RoutedEventArgs e)
    {
        _pendingAnnotations.Clear();
        _nextBadgeNum = 1;
        AnnCanvas.Children.Clear();
    }

    private async void BtnAnnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        var step = _stepVms[_selectedStepIndex].Step;
        step.Annotations = _pendingAnnotations.Count > 0 ? [.. _pendingAnnotations] : null;
        await SaveProjectAsync();
        SetStatus($"ステップ {step.StepNumber} のアノテーションを保存しました。");
    }

    // ---- ステップ操作 ----

    private async void BtnConfirmDesc_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        var step = _stepVms[_selectedStepIndex].Step;
        string val = TxtDescription.Text.Trim();
        step.DescriptionOverride = string.IsNullOrEmpty(val) ? null : val;

        await SaveProjectAsync();
        SetStatus("説明文を更新しました。");
    }

    private async void BtnDeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        var step = _stepVms[_selectedStepIndex].Step;
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

    // ---- ステップ ドラッグ＆ドロップ並び替え ----

    private void LstSteps_PreviewMouseLeftButtonDown(object sender, WinMouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(LstSteps);
        _dragStepSourceIndex = LstSteps.SelectedIndex;
    }

    private void LstSteps_PreviewMouseMove(object sender, WinMouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
        if (_dragStepSourceIndex < 0) return;

        var pos = e.GetPosition(LstSteps);
        if (Math.Abs(pos.X - _dragStartPoint.X) < 8 && Math.Abs(pos.Y - _dragStartPoint.Y) < 8) return;

        var vm = _stepVms[_dragStepSourceIndex];
        DragDrop.DoDragDrop(LstSteps, _dragStepSourceIndex, System.Windows.DragDropEffects.Move);
        _dragStepSourceIndex = -1;
    }

    private async void LstSteps_Drop(object sender, WinDragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int))) return;
        int srcIdx = (int)e.Data.GetData(typeof(int));
        int destIdx = HitTestStepIndex(e.GetPosition(LstSteps));
        if (destIdx < 0 || destIdx == srcIdx) return;

        var vm = _stepVms[srcIdx];
        _stepVms.RemoveAt(srcIdx);
        _stepVms.Insert(destIdx, vm);

        // Re-number steps and persist
        if (_selectedProject != null)
        {
            _selectedProject.Steps.Clear();
            for (int i = 0; i < _stepVms.Count; i++)
            {
                _stepVms[i].Step.StepNumber = i + 1;
                _selectedProject.Steps.Add(_stepVms[i].Step);
            }
            await SaveProjectAsync();
            SetStatus("ステップを並び替えました。");
        }
    }

    private int HitTestStepIndex(WinPoint pos)
    {
        int result = -1;
        System.Windows.Media.VisualTreeHelper.HitTest(
            LstSteps, null,
            ht =>
            {
                var item = FindVisualParent<System.Windows.Controls.ListBoxItem>(ht.VisualHit);
                if (item != null)
                {
                    result = LstSteps.ItemContainerGenerator.IndexFromContainer(item);
                    return System.Windows.Media.HitTestResultBehavior.Stop;
                }
                return System.Windows.Media.HitTestResultBehavior.Continue;
            },
            new System.Windows.Media.PointHitTestParameters(pos));
        return result;
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
        if (parent == null) return null;
        return parent is T t ? t : FindVisualParent<T>(parent);
    }

    // ---- ステップ分割・追加 ----

    private async void BtnSplitHere_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null || _selectedStepIndex < 0) return;
        int splitAt = _stepVms[_selectedStepIndex].Step.StepNumber;
        if (splitAt <= 1) { SetStatus("先頭ステップでは分割できません。"); return; }

        var dlgBefore = new InputDialog("分割前プロジェクトのタイトルを入力してください",
            _selectedProject.Title + "（前半）") { Owner = this };
        if (dlgBefore.ShowDialog() != true) return;

        var dlgAfter = new InputDialog("分割後プロジェクトのタイトルを入力してください",
            _selectedProject.Title + "（後半）") { Owner = this };
        if (dlgAfter.ShowDialog() != true) return;

        SetStatus("プロジェクトを分割中...");
        try
        {
            var (before, after) = await _projectStore.SplitProjectAsync(
                _selectedProject, splitAt, dlgBefore.InputText.Trim(), dlgAfter.InputText.Trim());
            await RefreshProjectListAsync();
            SetStatus($"分割完了: 「{before.Title}」と「{after.Title}」を作成しました。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "プロジェクト分割失敗");
            WinMessageBox.Show("分割中にエラーが発生しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("分割失敗。");
        }
    }

    private async void BtnAddStep_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;

        var dlg = new InputDialog("追加するステップの説明文を入力してください") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;

        // オプション: 画像ファイルの選択
        string? imagePath = null;
        var openDlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "画像ファイルを選択（省略可: キャンセルで画像なし）",
            Filter = "画像ファイル (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|すべてのファイル|*.*",
        };
        if (openDlg.ShowDialog() == true)
        {
            string ext = Path.GetExtension(openDlg.FileName);
            int newNum = (_selectedProject.Steps.Count > 0 ? _selectedProject.Steps.Max(s => s.StepNumber) : 0) + 1;
            string destName = $"step_{newNum:D3}{ext}";
            string destPath = Path.Combine(_selectedProject.ProjectFolder, "images", destName);
            Directory.CreateDirectory(Path.Combine(_selectedProject.ProjectFolder, "images"));
            File.Copy(openDlg.FileName, destPath, overwrite: true);
            imagePath = $"images/{destName}";
        }

        int insertAfter = _selectedStepIndex >= 0 ? _selectedStepIndex : _stepVms.Count - 1;
        int newStepNumber = insertAfter + 2; // 1-based, insert after current

        // Shift step numbers for steps after insertion point
        foreach (var s in _selectedProject.Steps.Where(s => s.StepNumber >= newStepNumber))
            s.StepNumber++;

        var newStep = new ProjectStep
        {
            StepNumber = newStepNumber,
            Timestamp = DateTimeOffset.Now,
            TriggerType = "Manual",
            WindowTitle = "",
            ProcessName = "",
            DescriptionRuleBased = dlg.InputText.Trim(),
            ImagePath = imagePath,
        };
        _selectedProject.Steps.Insert(insertAfter + 1, newStep);

        await SaveProjectAsync();
        SetStatus($"ステップ {newStepNumber} を追加しました。");
    }

    // ---- プロジェクト結合 ----

    private async void BtnMergeProjects_Click(object sender, RoutedEventArgs e)
    {
        var selected = LstProjects.SelectedItems.Cast<ProjectInfo>().ToList();
        if (selected.Count < 2) return;

        var dlg = new InputDialog("結合後のプロジェクト名を入力してください",
            selected[0].Title + "（結合）") { Owner = this };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.InputText)) return;

        SetStatus("プロジェクトを結合中...");
        try
        {
            var merged = await _projectStore.MergeProjectsAsync(selected, dlg.InputText.Trim());
            await RefreshProjectListAsync();
            SetStatus($"結合完了: 「{merged.Title}」を作成しました（{merged.Steps.Count} ステップ）。");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "プロジェクト結合失敗");
            WinMessageBox.Show("結合中にエラーが発生しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            SetStatus("結合失敗。");
        }
    }

    // ---- エクスポート ----

    private void BtnExportMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WinButton btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private async void BtnExportImages_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("画像をエクスポート中...", busy: true);
        await _exportService.ExportImagesAsync(_selectedProject);
        SetStatus("画像エクスポート完了。");
    }

    private async void BtnExportMarkdown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("Markdown 手順書を生成中...", busy: true);
        await _exportService.ExportMarkdownAsync(_selectedProject);
        SetStatus("Markdown エクスポート完了。");
    }

    private async void BtnExportHtml_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("HTML 手順書を生成中...", busy: true);
        await _exportService.ExportHtmlAsync(_selectedProject);
        SetStatus("HTML エクスポート完了。");
    }

    private async void BtnExportDocx_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("Word 手順書を生成中...", busy: true);
        await _exportService.ExportDocxAsync(_selectedProject);
        SetStatus("Word エクスポート完了。");
    }

    private async void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedProject == null) return;
        SetStatus("動画生成中...", busy: true);
        await _exportService.ExportVideoAsync(_selectedProject);
        SetStatus("動画生成完了。");
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

        SetStatus("ZIP を作成中...", busy: true);
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

    private void SetStatus(string msg, bool busy = false)
    {
        TxtStatus.Text = msg;
        PbStatus.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
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

internal class InputDialog : Window
{
    internal readonly WinTextBox _tb = new();
    public string InputText => _tb.Text;

    public InputDialog(string prompt, string initialText = "")
    {
        Title = "テキスト入力";
        Width = 340; Height = 120;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var sp = new WinStackPanel { Margin = new Thickness(10) };
        sp.Children.Add(new WinTextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 6) });
        sp.Children.Add(_tb);

        var btns = new WinStackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var ok = new WinButton { Content = "OK", Width = 60, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
        var cancel = new WinButton { Content = "キャンセル", Width = 80, IsCancel = true };
        ok.Click += (_, _) => { DialogResult = true; };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        sp.Children.Add(btns);
        Content = sp;
        _tb.Text = initialText;
        Loaded += (_, _) => { _tb.Focus(); _tb.SelectAll(); };
    }
}
