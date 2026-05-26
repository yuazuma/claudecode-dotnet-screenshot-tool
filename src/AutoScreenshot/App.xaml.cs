using System.Threading;
using AutoScreenshot.Services;
using Serilog;

// WPF と WinForms の Application が衝突するためエイリアスで解消
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using ShutdownMode = System.Windows.ShutdownMode;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;

namespace AutoScreenshot;

public partial class App : Application
{
    private static Mutex? _mutex;
    private NotifyIconWrapper? _notifyIconWrapper;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 二重起動抑止
        _mutex = new Mutex(true, "AutoScreenshot_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("AutoScreenshot はすでに起動しています。", "AutoScreenshot",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ConfigureLogging();
        Log.Information("AutoScreenshot 起動");

        _notifyIconWrapper = new NotifyIconWrapper();
        _notifyIconWrapper.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIconWrapper?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        Log.Information("AutoScreenshot 終了");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AutoScreenshot", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: Path.Combine(logDir, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
