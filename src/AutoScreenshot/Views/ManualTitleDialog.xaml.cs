using System.Windows;

namespace AutoScreenshot.Views;

public partial class ManualTitleDialog : Window
{
    /// <summary>ユーザーが入力したタイトル（OK で確定、Cancel なら null）</summary>
    public string? EnteredTitle { get; private set; }

    public ManualTitleDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtTitle.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        EnteredTitle = TxtTitle.Text.Trim();
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
