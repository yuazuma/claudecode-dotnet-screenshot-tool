using Microsoft.Win32;
using Serilog;

namespace AutoScreenshot.Services;

/// <summary>Windows ログオン時の自動起動エントリ管理 (HKCU レジストリ)</summary>
public static class AutoStartService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "AutoScreenshot";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(AppName) != null;
    }

    public static void Enable()
    {
        string exePath = Environment.ProcessPath ?? AppContext.BaseDirectory + "AutoScreenshot.exe";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            key.SetValue(AppName, $"\"{exePath}\"");
            Log.Information("自動起動を有効化: {Path}", exePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自動起動の有効化に失敗");
        }
    }

    public static void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
            key.DeleteValue(AppName, throwOnMissingValue: false);
            Log.Information("自動起動を無効化");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "自動起動の無効化に失敗");
        }
    }
}
