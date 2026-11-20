using System.Diagnostics;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace IdvLauncher.Dialogs;

public sealed partial class DownloadGameDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _closed = new();
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool _starting;
    private bool _downloading;

    public DownloadGameDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _poll.Tick += async (_, _) => await PollAsync();
    }

    public Task<bool> WaitForCloseAsync() => _closed.Task;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        string saved = Store.Instance.Settings.GameDownloadDir;
        FolderBox.Text = !string.IsNullOrWhiteSpace(saved)
            ? saved
            : Directory.Exists(@"D:\") ? @"D:\IDV" : @"C:\IDV";
        await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            if (!EngineService.Instance.IsReady && EngineService.Instance.IsInstalled)
            {
                StatusText.Text = "正在启动登录引擎…";
                await EngineService.Instance.StartAsync();
            }
            var options = await GameDownloadService.GetOptionsAsync();
            VersionCombo.ItemsSource = options;
            VersionCombo.SelectedIndex = 0;
            StatusText.Text = options.Count > 1
                ? "请选择要下载的版本。"
                : "将下载官方默认版本。";
        }
        catch (Exception ex)
        {
            VersionCombo.ItemsSource = new[]
            {
                new GameDownloadOption(-1, "官方 PC 版", "默认版本"),
            };
            VersionCombo.SelectedIndex = 0;
            StatusText.Text = $"读取版本失败，将使用官方默认版本：{ex.Message}";
        }
    }

    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;
        try
        {
            string? folder = NativeFileDialog.PickFolder(
                WindowNative.GetWindowHandle(App.MainWindow));
            if (!string.IsNullOrWhiteSpace(folder)) FolderBox.Text = folder;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"选择目录失败：{ex.Message}";
        }
    }

    private async void OnStart(object sender, RoutedEventArgs e)
    {
        if (_starting || _downloading) return;
        if (VersionCombo.SelectedItem is not GameDownloadOption option)
        {
            StatusText.Text = "请选择要下载的版本。";
            return;
        }

        string folder = FolderBox.Text.Trim().Trim('"');
        if (folder.Length == 0)
        {
            StatusText.Text = "请选择安装目录。";
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            Store.Instance.Settings.GameDownloadDir = folder;
            Store.Instance.SaveSettings();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"安装目录不可用：{ex.Message}";
            return;
        }

        _starting = true;
        StartButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.IsIndeterminate = true;
        StatusText.Text = "正在启动登录引擎并准备下载…";
        try
        {
            await GameDownloadService.StartAsync(option, folder);
            _downloading = true;
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = 0;
            StatusText.Text = "下载任务已启动，引擎正在连接官方 CDN…";
            _poll.Start();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"下载启动失败：{ex.Message}";
            StartButton.IsEnabled = true;
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task PollAsync()
    {
        try
        {
            var progress = await GameDownloadService.GetProgressAsync();
            DownloadProgress.IsIndeterminate = false;
            DownloadProgress.Value = progress.Percent;
            StatusText.Text = progress.Running
                ? $"{progress.Status} · {progress.Percent:0.0}%"
                : progress.Installed
                    ? "下载完成，游戏已安装。"
                    : progress.Status;

            if (!string.IsNullOrWhiteSpace(progress.Error))
            {
                _poll.Stop();
                _downloading = false;
                StartButton.IsEnabled = true;
                StatusText.Text = $"下载失败：{progress.Error}";
                return;
            }

            if (progress.Installed)
            {
                _poll.Stop();
                _downloading = false;
                if (!string.IsNullOrWhiteSpace(progress.GamePath))
                {
                    Store.Instance.Settings.GamePath = progress.GamePath;
                    Store.Instance.SaveSettings();
                }
                StatusText.Text = "游戏已安装，可以关闭此窗口并启动游戏。";
                StartButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"读取下载进度失败：{ex.Message}";
        }
    }

    private void OnOpenOfficial(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://id5.163.com/") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"打开官方页面失败：{ex.Message}";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        _poll.Stop();
        _closed.TrySetResult(true);
        App.MainWindow?.HideModal();
    }
}
