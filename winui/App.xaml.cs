using System.Diagnostics;
using System.Runtime.InteropServices;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;

namespace IdvLauncher;

public partial class App : Application
{
    private readonly Mutex _mutex;
    private bool _ownsMutex;

    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();

        _mutex = new Mutex(true, @"Local\IdvLauncher.WinUI.SingleInstance", out _ownsMutex);
        if (!_ownsMutex)
        {
            ActivateExistingInstance();
            Environment.Exit(0);
            return;
        }

        UnhandledException += (_, e) =>
        {
            TryLog(e.Exception?.ToString() ?? e.Message);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            TryLog(e.ExceptionObject?.ToString() ?? "AppDomain unhandled exception");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            TryLog(e.Exception.ToString());
            e.SetObserved();
        };
    }

    public static void ClearMainWindow() => MainWindow = null;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Store.Instance.Load();

        string[] cli = Environment.GetCommandLineArgs();
        string? pageArg = cli.FirstOrDefault(a => a.StartsWith("--page=", StringComparison.OrdinalIgnoreCase));
        if (pageArg is not null)
        {
            Store.Instance.Settings.LastPage = pageArg.ToLowerInvariant() switch
            {
                "--page=accounts" => 1,
                "--page=settings" => 2,
                "--page=engine" => 3,
                "--page=injection" => 4,
                "--page=cloud" => 5,
                _ => 0,
            };
        }

        string? themeArg = cli.FirstOrDefault(a => a.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
        if (themeArg is not null)
        {
            Store.Instance.Settings.Theme = themeArg.ToLowerInvariant() switch
            {
                "--theme=dark" => 1,
                "--theme=light" => 2,
                _ => 0,
            };
        }

        if (cli.Any(a => string.Equals(a, "--collapsed", StringComparison.OrdinalIgnoreCase)))
            Store.Instance.Settings.SidebarCollapsed = true;

        MainWindow = new MainWindow();
        MainWindow.Activate();

        if (cli.Any(a => string.Equals(a, "--dialog=add", StringComparison.OrdinalIgnoreCase)))
            MainWindow.OpenAddAccountDialog();
        else if (cli.Any(a => string.Equals(a, "--dialog=announcements", StringComparison.OrdinalIgnoreCase)))
            MainWindow.OpenAnnouncementsDialog();

        _ = InitializeAfterLaunchAsync();
    }

    private static async Task InitializeAfterLaunchAsync()
    {
        var settings = Store.Instance.Settings;
        if (!settings.DisclaimerAccepted)
        {
            bool accepted = MainWindow is not null && await MainWindow.ShowStartupDisclaimerAsync();
            if (!accepted)
            {
                MainWindow?.CloseForExit();
                return;
            }
            settings.DisclaimerAccepted = true;
            Store.Instance.SaveSettings();
        }

        StartEngineQuietly();
    }

    private static async void StartEngineQuietly()
    {
        try
        {
            if (!Store.Instance.Settings.UseLoginEngine) return;
            if (!EngineService.Instance.IsInstalled || !EnginePaths.IsElevated) return;
            await EngineService.Instance.StartAsync();
        }
        catch (Exception ex)
        {
            TryLog("自动启动登录引擎失败：" + ex);
        }
    }

    public static void TryLog(string message)
    {
        try
        {
            File.AppendAllText(Path.Combine(Store.Instance.DataDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            Debug.WriteLine(message);
        }
    }

    private static void ActivateExistingInstance()
    {
        foreach (var process in Process.GetProcessesByName("IdvLauncher"))
        {
            if (process.Id == Environment.ProcessId) continue;
            IntPtr hwnd = process.MainWindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 9);
                SetForegroundWindow(hwnd);
            }
            process.Dispose();
            break;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
