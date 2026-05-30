using System.ComponentModel;
using System.Windows;
using AutoScreenshot.Models;

namespace AutoScreenshot.Views;

/// <summary>エクスポート進捗ウィンドウ（ノンモーダル・FR-H6）</summary>
public partial class ExportProgressWindow : Window
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>キャンセルトークン。エクスポートメソッドに渡す。</summary>
    public CancellationToken CancellationToken => _cts.Token;

    public ExportProgressWindow()
    {
        InitializeComponent();
        PositionBottomRight();
    }

    /// <summary>進捗を更新する（UI スレッドから呼ぶこと）。</summary>
    public void UpdateProgress(ExportProgress progress)
    {
        TxtOperation.Text = progress.OperationName;

        if (progress.IsIndeterminate)
        {
            PbProgress.IsIndeterminate = true;
            TxtCounter.Text = progress.Current > 0 ? $"{progress.Current} 件処理済み" : "";
        }
        else
        {
            PbProgress.IsIndeterminate = false;
            PbProgress.Value = progress.Fraction * 100;
            TxtCounter.Text = $"{progress.Current} / {progress.Total}";
        }

        TxtOutputPath.Text = progress.OutputPath != null
            ? System.IO.Path.GetFileName(progress.OutputPath)
            : "";
    }

    /// <summary>完了時の表示更新（ウィンドウは自動クローズ）。</summary>
    public void MarkCompleted()
    {
        PbProgress.IsIndeterminate = false;
        PbProgress.Value = 100;
        BtnCancel.IsEnabled = false;
        Close();
    }

    /// <summary>キャンセル完了の表示（2秒後に自動クローズ）。</summary>
    public void MarkCancelled()
    {
        TxtOperation.Text = "キャンセルしました";
        BtnCancel.IsEnabled = false;
        Task.Delay(2000).ContinueWith(_ =>
            Dispatcher.BeginInvoke(Close));
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        BtnCancel.IsEnabled = false;
        TxtOperation.Text = "キャンセル中...";
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // × ボタンでもキャンセル
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();
    }

    private void PositionBottomRight()
    {
        var screen = System.Windows.SystemParameters.WorkArea;
        Left = screen.Right - Width - 16;
        Top  = screen.Bottom - 120;
    }
}
