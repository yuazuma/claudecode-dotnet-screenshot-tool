using System.Windows;
using System.Windows.Forms;
using AutoScreenshot.Models;
using AutoScreenshot.Services;

namespace AutoScreenshot.Views;

public partial class SettingsWindow : Window
{
    private readonly ConfigStore _config;

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
        TxtHotkey.Text = cfg.HotkeyPause ?? "(未設定)";

        // トリガー
        ChkMouseLeft.IsChecked   = cfg.Triggers.MouseLeftClick;
        ChkMouseRight.IsChecked  = cfg.Triggers.MouseRightClick;
        ChkMouseMiddle.IsChecked = cfg.Triggers.MouseMiddleClick;
        ChkMouseWheel.IsChecked  = cfg.Triggers.MouseWheel;
        ChkKeyboard.IsChecked    = cfg.Triggers.Keyboard;
        ChkActiveWindow.IsChecked = cfg.Triggers.ActiveWindowChange;
        ChkScreenDiff.IsChecked  = cfg.Triggers.ScreenDiff;

        SldrKeyboardIdle.Value  = cfg.Triggers.KeyboardIdleSeconds;
        SldrDiffInterval.Value  = cfg.Triggers.ScreenDiffIntervalSeconds;
        SldrDiffThreshold.Value = cfg.Triggers.ScreenDiffThresholdPercent;

        // 保存
        TxtSaveFolder.Text = cfg.Storage.SaveFolder;
        RdoPng.IsChecked  = cfg.Storage.ImageFormat == ImageFormat.Png;
        RdoJpeg.IsChecked = cfg.Storage.ImageFormat == ImageFormat.Jpeg;
        RdoWebP.IsChecked = cfg.Storage.ImageFormat == ImageFormat.WebP;
        CmbNaming.SelectedIndex = (int)cfg.Storage.FolderNaming;

        // プライバシー
        ChkMaskPassword.IsChecked = cfg.Privacy.MaskPasswordFields;
        TxtExcludeApps.Text = string.Join(Environment.NewLine, cfg.Privacy.ExcludeApps);

        // 通知
        ChkFlash.IsChecked   = cfg.Notification.IconFlash;
        ChkToast.IsChecked   = cfg.Notification.Toast;
        ChkCounter.IsChecked = cfg.Notification.ShowCounter;
    }

    private void ApplySettings()
    {
        _config.Update(cfg =>
        {
            cfg.AutoStart = ChkAutoStart.IsChecked == true;

            cfg.Triggers.MouseLeftClick   = ChkMouseLeft.IsChecked == true;
            cfg.Triggers.MouseRightClick  = ChkMouseRight.IsChecked == true;
            cfg.Triggers.MouseMiddleClick = ChkMouseMiddle.IsChecked == true;
            cfg.Triggers.MouseWheel       = ChkMouseWheel.IsChecked == true;
            cfg.Triggers.Keyboard         = ChkKeyboard.IsChecked == true;
            cfg.Triggers.ActiveWindowChange = ChkActiveWindow.IsChecked == true;
            cfg.Triggers.ScreenDiff       = ChkScreenDiff.IsChecked == true;

            cfg.Triggers.KeyboardIdleSeconds          = SldrKeyboardIdle.Value;
            cfg.Triggers.ScreenDiffIntervalSeconds    = (int)SldrDiffInterval.Value;
            cfg.Triggers.ScreenDiffThresholdPercent   = SldrDiffThreshold.Value;

            cfg.Storage.SaveFolder   = TxtSaveFolder.Text;
            cfg.Storage.ImageFormat  = RdoJpeg.IsChecked == true ? ImageFormat.Jpeg
                                     : RdoWebP.IsChecked == true ? ImageFormat.WebP
                                     : ImageFormat.Png;
            cfg.Storage.FolderNaming = (FolderNamingRule)CmbNaming.SelectedIndex;

            cfg.Privacy.MaskPasswordFields = ChkMaskPassword.IsChecked == true;
            cfg.Privacy.ExcludeApps = TxtExcludeApps.Text
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            cfg.Notification.IconFlash   = ChkFlash.IsChecked == true;
            cfg.Notification.Toast       = ChkToast.IsChecked == true;
            cfg.Notification.ShowCounter = ChkCounter.IsChecked == true;
        });
    }

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
}
