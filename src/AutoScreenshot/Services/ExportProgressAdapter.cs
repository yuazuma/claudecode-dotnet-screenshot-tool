using AutoScreenshot.Models;

namespace AutoScreenshot.Services;

/// <summary>エクスポート進捗ウィンドウと ExportService をつなぐアダプター（FR-H6）</summary>
public sealed class ExportProgressAdapter
{
    private readonly Action<ExportProgress> _onReport;

    public IProgress<ExportProgress> Progress { get; }
    public CancellationToken Token { get; }

    public ExportProgressAdapter(Action<ExportProgress> onReport, CancellationToken ct)
    {
        _onReport = onReport;
        Token     = ct;
        Progress  = new Progress<ExportProgress>(onReport);
    }
}
