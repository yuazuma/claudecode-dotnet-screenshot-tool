using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AutoScreenshot.Models;
using AutoScreenshot.Services;
using AutoStartService = AutoScreenshot.Services.AutoStartService;

namespace AutoScreenshot.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _config;
    private string? _pendingHotkey;

    public SettingsWindow(ConfigStore config)
    {
        InitializeComponent();
        _config = config;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var cfg = _config.Config;

        ChkAutoStart.IsChecked = cfg.AutoStart;
        _pendingHotkey = cfg.HotkeyPause;
        TxtHotkey.Text = cfg.HotkeyPause ?? "(未設定)";

        // トリガー
        ChkMouseLeft.IsChecked    = cfg.Triggers.MouseLeftClick;
        ChkMouseRight.IsChecked   = cfg.Triggers.MouseRightClick;
        ChkMouseMiddle.IsChecked  = cfg.Triggers.MouseMiddleClick;
        ChkMouseDrag.IsChecked    = cfg.Triggers.MouseDragDrop;
        ChkMouseWheel.IsChecked   = cfg.Triggers.MouseWheel;
        ChkKeyboard.IsChecked     = cfg.Triggers.Keyboard;
        ChkActiveWindow.IsChecked = cfg.Triggers.ActiveWindowChange;
        ChkScreenDiff.IsChecked   = cfg.Triggers.ScreenDiff;

        SldrKeyboardIdle.Value  = cfg.Triggers.KeyboardIdleSeconds;
        SldrDiffInterval.Value  = cfg.Triggers.ScreenDiffIntervalSeconds;
        SldrDiffThreshold.Value = cfg.Triggers.ScreenDiffThresholdPercent;
        SldrDragThreshold.Value = cfg.Triggers.DragThresholdMs;
        SldrWheelIdle.Value     = cfg.Triggers.WheelIdleMs;

        // クールダウン
        SldrCooldownClick.Value    = cfg.Triggers.CooldownMouseClick;
        SldrCooldownDrag.Value     = cfg.Triggers.CooldownMouseDragDrop;
        SldrCooldownWheel.Value    = cfg.Triggers.CooldownMouseWheel;
        SldrCooldownKeyboard.Value = cfg.Triggers.CooldownKeyboard;
        SldrCooldownWindow.Value   = cfg.Triggers.CooldownActiveWindow;
        SldrCooldownDiff.Value     = cfg.Triggers.CooldownScreenDiff;

        // 保存
        TxtSaveFolder.Text = cfg.Storage.SaveFolder;
        RdoPng.IsChecked   = cfg.Storage.ImageFormat == ImageFormat.Png;
        RdoJpeg.IsChecked  = cfg.Storage.ImageFormat == ImageFormat.Jpeg;
        RdoWebP.IsChecked  = cfg.Storage.ImageFormat == ImageFormat.WebP;
        SldrJpegQuality.Value = cfg.Storage.JpegQuality;
        PnlJpegQuality.Visibility = cfg.Storage.ImageFormat != ImageFormat.Png
            ? Visibility.Visible : Visibility.Collapsed;
        CmbNaming.SelectedIndex = (int)cfg.Storage.FolderNaming;

        // メタデータ
        ChkSidecarLog.IsChecked    = cfg.Metadata.SidecarTextLog;
        ChkBurnTimestamp.IsChecked = cfg.Metadata.BurnTimestamp;
        ChkImageOverlay.IsChecked  = cfg.Metadata.ImageOverlay;
        ChkStructuredOutput.IsChecked = cfg.Metadata.StructuredOutput;
        RdoJsonLines.IsChecked = cfg.Metadata.StructuredFormat == StructuredFormat.JsonLines;
        RdoCsv.IsChecked       = cfg.Metadata.StructuredFormat == StructuredFormat.Csv;
        PnlStructFmt.IsEnabled = cfg.Metadata.StructuredOutput;

        // プライバシー
        ChkMaskPassword.IsChecked = cfg.Privacy.MaskPasswordFields;
        TxtExcludeApps.Text = string.Join(Environment.NewLine, cfg.Privacy.ExcludeApps);

        // 通知
        ChkFlash.IsChecked   = cfg.Notification.IconFlash;
        ChkToast.IsChecked   = cfg.Notification.Toast;
        ChkCounter.IsChecked = cfg.Notification.ShowCounter;

        // 手順書生成
        ChkManualEnabled.IsChecked        = cfg.ManualGen.Enabled;
        TxtManualOutputFolder.Text        = cfg.ManualGen.OutputFolder;
        CmbScreenshotMode.SelectedIndex   = (int)cfg.ManualGen.ScreenshotMode;
        CmbKeyboardMode.SelectedIndex     = (int)cfg.ManualGen.KeyboardMode;
        SldrChapterTimeGap.Value          = cfg.ManualGen.ChapterTimeGapMinutes;
        ChkManualShowTitleDialog.IsChecked = cfg.ManualGen.ShowTitleDialogOnStart;
        ChkOutputMarkdown.IsChecked       = cfg.ManualGen.OutputMarkdown;
        ChkOutputDocx.IsChecked           = cfg.ManualGen.OutputDocx;
        TxtTemplateMarkdownPath.Text      = cfg.ManualGen.TemplateMarkdownPath;
        TxtTemplateDotxPath.Text          = cfg.ManualGen.TemplateDotxPath;

        // LLM連携 (NF-04: DPAPI 復号して表示)
        ChkLlmEnabled.IsChecked       = cfg.ManualGen.LlmEnabled;
        PwdLlmEndpoint.Password       = DpapiHelper.Unprotect(cfg.ManualGen.LlmEndpoint);
        PwdLlmApiKey.Password         = DpapiHelper.Unprotect(cfg.ManualGen.LlmApiKey);
        TxtLlmDeploymentName.Text     = cfg.ManualGen.LlmDeploymentName;

        // 動画生成
        var vc = cfg.VideoGen;
        ChkVideoApng.IsChecked          = vc.OutputApng;
        ChkVideoMp4.IsChecked           = vc.OutputMp4;
        TxtVideoFolder.Text             = vc.VideoOutputFolder;
        RdoVideoSession.IsChecked       = vc.VideoUnit == VideoUnit.Session;
        RdoVideoChapter.IsChecked       = vc.VideoUnit == VideoUnit.Chapter;
        RdoFrameFixed.IsChecked         = vc.FrameTimingMode == FrameTimingMode.Fixed;
        RdoFrameRealTime.IsChecked      = vc.FrameTimingMode == FrameTimingMode.RealTime;
        TxtFixedFrameSec.Text           = vc.FixedFrameSeconds.ToString();
        TxtMinFrameSec.Text             = vc.MinFrameSeconds.ToString();
        TxtMaxFrameSec.Text             = vc.MaxFrameSeconds.ToString();
        RdoResOriginal.IsChecked        = vc.OutputResolution == VideoResolution.Original;
        RdoResFhd.IsChecked             = vc.OutputResolution == VideoResolution.Fhd;
        RdoResHd.IsChecked              = vc.OutputResolution == VideoResolution.Hd;
        TxtMp4Bitrate.Text              = vc.Mp4VideoBitrateMbps.ToString();
        ChkDrawRipple.IsChecked         = vc.DrawRipple;
        ChkDrawRectangle.IsChecked      = vc.DrawRectangle;
        ChkShowTelop.IsChecked          = vc.ShowTelop;
        ChkTelopEventLabel.IsChecked    = vc.TelopShowEventLabel;
        ChkTelopInputText.IsChecked     = vc.TelopShowInputText;
        ChkTelopDescription.IsChecked   = vc.TelopShowDescription;
        ChkTelopTimestamp.IsChecked     = vc.TelopShowTimestamp;
        TxtTelopFontSize.Text           = vc.TelopFontSize.ToString();
        TxtTelopBgAlpha.Text            = vc.TelopBgAlpha.ToString();
        ChkTtsEnabled.IsChecked         = vc.TtsEnabled;
        TxtTtsVoice.Text                = vc.TtsVoice;
        TxtTtsRate.Text                 = vc.TtsRate.ToString();
        TxtTtsVolume.Text               = vc.TtsVolume.ToString();
        ChkAutoGenerateVideo.IsChecked  = vc.AutoGenerateWithManual;
        ChkOpenFolderOnComplete.IsChecked = vc.OpenFolderOnComplete;
        UpdateFrameTimingPanels();

        // プロジェクト
        var pc = _config.Config.Project;
        ChkProjectEnabled.IsChecked       = pc.Enabled;
        SldrThumbnailWidth.Value          = pc.ThumbnailMaxWidth;
        ChkAutoExportMarkdown.IsChecked   = pc.AutoExportMarkdown;
        ChkAutoExportHtml.IsChecked       = pc.AutoExportHtml;
        ChkAutoExportDocx.IsChecked       = pc.AutoExportDocx;
        ChkAutoExportVideo.IsChecked      = pc.AutoExportVideo;
        ChkIncrementalLlm.IsChecked       = pc.IncrementalLlm;
        ChkOpenFolderOnExport.IsChecked   = pc.OpenFolderOnExportComplete;
    }

    private void ApplySettings()
    {
        bool autoStart = ChkAutoStart.IsChecked == true;
        string? hotkey = string.IsNullOrWhiteSpace(_pendingHotkey) ? null : _pendingHotkey;

        _config.Update(cfg =>
        {
            cfg.AutoStart    = autoStart;
            cfg.HotkeyPause  = hotkey;

            cfg.Triggers.MouseLeftClick     = ChkMouseLeft.IsChecked == true;
            cfg.Triggers.MouseRightClick    = ChkMouseRight.IsChecked == true;
            cfg.Triggers.MouseMiddleClick   = ChkMouseMiddle.IsChecked == true;
            cfg.Triggers.MouseDragDrop      = ChkMouseDrag.IsChecked == true;
            cfg.Triggers.MouseWheel         = ChkMouseWheel.IsChecked == true;
            cfg.Triggers.Keyboard           = ChkKeyboard.IsChecked == true;
            cfg.Triggers.ActiveWindowChange = ChkActiveWindow.IsChecked == true;
            cfg.Triggers.ScreenDiff         = ChkScreenDiff.IsChecked == true;

            cfg.Triggers.KeyboardIdleSeconds        = SldrKeyboardIdle.Value;
            cfg.Triggers.ScreenDiffIntervalSeconds  = (int)SldrDiffInterval.Value;
            cfg.Triggers.ScreenDiffThresholdPercent = SldrDiffThreshold.Value;
            cfg.Triggers.DragThresholdMs            = (int)SldrDragThreshold.Value;
            cfg.Triggers.WheelIdleMs                = (int)SldrWheelIdle.Value;

            cfg.Triggers.CooldownMouseClick    = SldrCooldownClick.Value;
            cfg.Triggers.CooldownMouseDragDrop = SldrCooldownDrag.Value;
            cfg.Triggers.CooldownMouseWheel    = SldrCooldownWheel.Value;
            cfg.Triggers.CooldownKeyboard      = SldrCooldownKeyboard.Value;
            cfg.Triggers.CooldownActiveWindow  = SldrCooldownWindow.Value;
            cfg.Triggers.CooldownScreenDiff    = SldrCooldownDiff.Value;

            cfg.Storage.SaveFolder  = TxtSaveFolder.Text;
            cfg.Storage.ImageFormat = RdoJpeg.IsChecked == true ? ImageFormat.Jpeg
                                    : RdoWebP.IsChecked == true ? ImageFormat.WebP
                                    : ImageFormat.Png;
            cfg.Storage.JpegQuality  = (int)SldrJpegQuality.Value;
            cfg.Storage.FolderNaming = (FolderNamingRule)CmbNaming.SelectedIndex;

            cfg.Metadata.SidecarTextLog    = ChkSidecarLog.IsChecked == true;
            cfg.Metadata.BurnTimestamp     = ChkBurnTimestamp.IsChecked == true;
            cfg.Metadata.ImageOverlay      = ChkImageOverlay.IsChecked == true;
            cfg.Metadata.StructuredOutput  = ChkStructuredOutput.IsChecked == true;
            cfg.Metadata.StructuredFormat  = RdoCsv.IsChecked == true
                                           ? StructuredFormat.Csv : StructuredFormat.JsonLines;

            cfg.Privacy.MaskPasswordFields = ChkMaskPassword.IsChecked == true;
            cfg.Privacy.ExcludeApps = TxtExcludeApps.Text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            cfg.Notification.IconFlash   = ChkFlash.IsChecked == true;
            cfg.Notification.Toast       = ChkToast.IsChecked == true;
            cfg.Notification.ShowCounter = ChkCounter.IsChecked == true;

            cfg.ManualGen.Enabled               = ChkManualEnabled.IsChecked == true;
            cfg.ManualGen.OutputFolder          = TxtManualOutputFolder.Text.Trim();
            cfg.ManualGen.ScreenshotMode        = (AutoScreenshot.Models.ScreenshotMode)CmbScreenshotMode.SelectedIndex;
            cfg.ManualGen.KeyboardMode          = (AutoScreenshot.Models.KeyboardMode)CmbKeyboardMode.SelectedIndex;
            cfg.ManualGen.ChapterTimeGapMinutes = (int)SldrChapterTimeGap.Value;
            cfg.ManualGen.ShowTitleDialogOnStart = ChkManualShowTitleDialog.IsChecked == true;
            cfg.ManualGen.OutputMarkdown         = ChkOutputMarkdown.IsChecked == true;
            cfg.ManualGen.OutputDocx             = ChkOutputDocx.IsChecked == true;
            cfg.ManualGen.TemplateMarkdownPath   = TxtTemplateMarkdownPath.Text.Trim();
            cfg.ManualGen.TemplateDotxPath       = TxtTemplateDotxPath.Text.Trim();

            // LLM連携 (NF-04: DPAPI 暗号化して保存)
            cfg.ManualGen.LlmEnabled        = ChkLlmEnabled.IsChecked == true;
            cfg.ManualGen.LlmEndpoint       = DpapiHelper.Protect(PwdLlmEndpoint.Password);
            cfg.ManualGen.LlmApiKey         = DpapiHelper.Protect(PwdLlmApiKey.Password);
            cfg.ManualGen.LlmDeploymentName = TxtLlmDeploymentName.Text.Trim();

            // 動画生成
            cfg.VideoGen.OutputApng          = ChkVideoApng.IsChecked == true;
            cfg.VideoGen.OutputMp4           = ChkVideoMp4.IsChecked == true;
            cfg.VideoGen.VideoOutputFolder   = TxtVideoFolder.Text.Trim();
            cfg.VideoGen.VideoUnit           = RdoVideoChapter.IsChecked == true ? VideoUnit.Chapter : VideoUnit.Session;
            cfg.VideoGen.FrameTimingMode     = RdoFrameRealTime.IsChecked == true ? FrameTimingMode.RealTime : FrameTimingMode.Fixed;
            cfg.VideoGen.FixedFrameSeconds   = double.TryParse(TxtFixedFrameSec.Text, out double f) ? f : 3.0;
            cfg.VideoGen.MinFrameSeconds     = double.TryParse(TxtMinFrameSec.Text,   out double mn) ? mn : 1.0;
            cfg.VideoGen.MaxFrameSeconds     = double.TryParse(TxtMaxFrameSec.Text,   out double mx) ? mx : 10.0;
            cfg.VideoGen.OutputResolution    = RdoResFhd.IsChecked == true ? VideoResolution.Fhd
                                             : RdoResHd.IsChecked  == true ? VideoResolution.Hd
                                             : VideoResolution.Original;
            cfg.VideoGen.Mp4VideoBitrateMbps = int.TryParse(TxtMp4Bitrate.Text, out int br) ? br : 4;
            cfg.VideoGen.DrawRipple          = ChkDrawRipple.IsChecked == true;
            cfg.VideoGen.DrawRectangle       = ChkDrawRectangle.IsChecked == true;
            cfg.VideoGen.ShowTelop           = ChkShowTelop.IsChecked == true;
            cfg.VideoGen.TelopShowEventLabel = ChkTelopEventLabel.IsChecked == true;
            cfg.VideoGen.TelopShowInputText  = ChkTelopInputText.IsChecked == true;
            cfg.VideoGen.TelopShowDescription = ChkTelopDescription.IsChecked == true;
            cfg.VideoGen.TelopShowTimestamp  = ChkTelopTimestamp.IsChecked == true;
            cfg.VideoGen.TelopFontSize       = int.TryParse(TxtTelopFontSize.Text,  out int fs) ? fs : 16;
            cfg.VideoGen.TelopBgAlpha        = byte.TryParse(TxtTelopBgAlpha.Text,  out byte ba) ? ba : (byte)160;
            cfg.VideoGen.TtsEnabled          = ChkTtsEnabled.IsChecked == true;
            cfg.VideoGen.TtsVoice            = TxtTtsVoice.Text.Trim();
            cfg.VideoGen.TtsRate             = int.TryParse(TxtTtsRate.Text,   out int tr) ? Math.Clamp(tr, -10, 10) : 0;
            cfg.VideoGen.TtsVolume           = int.TryParse(TxtTtsVolume.Text, out int tv) ? Math.Clamp(tv, 0, 100) : 100;
            cfg.VideoGen.AutoGenerateWithManual  = ChkAutoGenerateVideo.IsChecked == true;
            cfg.VideoGen.OpenFolderOnComplete    = ChkOpenFolderOnComplete.IsChecked == true;

            // プロジェクト
            cfg.Project.Enabled                   = ChkProjectEnabled.IsChecked == true;
            cfg.Project.ThumbnailMaxWidth         = (int)SldrThumbnailWidth.Value;
            cfg.Project.AutoExportMarkdown        = ChkAutoExportMarkdown.IsChecked == true;
            cfg.Project.AutoExportHtml            = ChkAutoExportHtml.IsChecked == true;
            cfg.Project.AutoExportDocx            = ChkAutoExportDocx.IsChecked == true;
            cfg.Project.AutoExportVideo           = ChkAutoExportVideo.IsChecked == true;
            cfg.Project.IncrementalLlm            = ChkIncrementalLlm.IsChecked == true;
            cfg.Project.OpenFolderOnExportComplete = ChkOpenFolderOnExport.IsChecked == true;
        });
        // ConfigChanged イベント → NotifyIconWrapper が HotkeyService.Register() を再呼び出し

        // 自動起動レジストリを設定と同期
        if (autoStart && !AutoStartService.IsEnabled())
            AutoStartService.Enable();
        else if (!autoStart && AutoStartService.IsEnabled())
            AutoStartService.Disable();
    }

    // --- フォーマット選択 ---

    private void RdoFormat_Checked(object sender, RoutedEventArgs e)
    {
        if (PnlJpegQuality == null) return;
        PnlJpegQuality.Visibility = (RdoPng.IsChecked == true)
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ChkStructuredOutput_Changed(object sender, RoutedEventArgs e)
    {
        if (PnlStructFmt == null) return;
        PnlStructFmt.IsEnabled = ChkStructuredOutput.IsChecked == true;
    }

    // --- ホットキー入力処理 ---

    private void TxtHotkey_GotFocus(object sender, RoutedEventArgs e)
    {
        TxtHotkey.Text = "(キーを押してください)";
    }

    private void TxtHotkey_LostFocus(object sender, RoutedEventArgs e)
    {
        // フォーカスを失ったとき、未確定なら元の値を表示
        TxtHotkey.Text = _pendingHotkey ?? "(未設定)";
    }

    private void TxtHotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 修飾キー単体は無視
        if (key is Key.LeftCtrl or Key.RightCtrl
                or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift
                or Key.LWin or Key.RWin)
            return;

        // Escape でクリア
        if (key == Key.Escape)
        {
            _pendingHotkey = null;
            TxtHotkey.Text = "(未設定)";
            return;
        }

        string combo = HotkeyService.KeyToString(Keyboard.Modifiers, key);
        _pendingHotkey = combo;
        TxtHotkey.Text = combo;
    }

    private void BtnClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        _pendingHotkey = null;
        TxtHotkey.Text = "(未設定)";
    }

    // --- ボタンハンドラ ---

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        Close();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e) => ApplySettings();

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = TxtSaveFolder.Text,
            Description = "保存先フォルダを選択してください"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtSaveFolder.Text = dialog.SelectedPath;
    }

    private void BtnBrowseManualFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = TxtManualOutputFolder.Text,
            Description = "手順書の出力先フォルダを選択してください"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtManualOutputFolder.Text = dialog.SelectedPath;
    }

    private void BtnBrowseMarkdownTemplate_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Markdown テンプレートファイルを選択",
            Filter = "Markdown ファイル (*.md)|*.md|すべてのファイル (*.*)|*.*",
            FileName = TxtTemplateMarkdownPath.Text,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtTemplateMarkdownPath.Text = dialog.FileName;
    }

    private void BtnBrowseDotxTemplate_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Word テンプレートファイルを選択",
            Filter = "Word テンプレート (*.dotx)|*.dotx|すべてのファイル (*.*)|*.*",
            FileName = TxtTemplateDotxPath.Text,
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtTemplateDotxPath.Text = dialog.FileName;
    }

    // --- 動画生成タブ ---

    private void BtnVideoFolderBrowse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = TxtVideoFolder.Text,
            Description  = "動画の出力先フォルダを選択してください"
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtVideoFolder.Text = dialog.SelectedPath;
    }

    private void RdoFrameTiming_Changed(object sender, RoutedEventArgs e)
        => UpdateFrameTimingPanels();

    private void UpdateFrameTimingPanels()
    {
        if (PnlFixedFrame == null || PnlRealTimeFrame == null) return;
        bool isFixed = RdoFrameFixed.IsChecked == true;
        PnlFixedFrame.Visibility    = isFixed ? Visibility.Visible : Visibility.Collapsed;
        PnlRealTimeFrame.Visibility = isFixed ? Visibility.Collapsed : Visibility.Visible;
    }
}
