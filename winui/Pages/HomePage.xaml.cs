using System.Diagnostics;
using IdvLauncher.Dialogs;
using IdvLauncher.Models;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;

namespace IdvLauncher.Pages;

public sealed partial class HomePage : Page
{
    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _launchTimer;
    private DispatcherTimer? _closeTimer;
    private int _countdown;
    private bool _launching;
    private bool _loadingAccounts;
    private bool _unloaded;
    private bool _engineAccounts;
    private bool _engineSubscribed;
    private bool _engineStarting;
    private bool _gameBusy;
    private List<Announcement> _announcements = new();
    private List<HomeAccountOption> _accountOptions = new();

    public HomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _unloaded = false;
        if (!_engineSubscribed)
        {
            EngineService.Instance.StateChanged += OnEngineStateChanged;
            _engineSubscribed = true;
        }
        SetHeroImage();
        await LoadAnnouncementsAsync();
        await LoadAccountsAsync();
        Refresh();
        _ = PrewarmEngineAsync();
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _unloaded = true;
        if (_engineSubscribed)
        {
            EngineService.Instance.StateChanged -= OnEngineStateChanged;
            _engineSubscribed = false;
        }
        _statusTimer?.Stop();
        _launchTimer?.Stop();
        _closeTimer?.Stop();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!IsLoaded) return;
        await LoadAnnouncementsAsync();
        await LoadAccountsAsync();
        Refresh();
    }

    private void SetHeroImage()
    {
        try
        {
            string baseDir = AppContext.BaseDirectory;
            string path = Path.Combine(baseDir, "Assets", "cover.jpg");
            if (!File.Exists(path)) path = Path.Combine(baseDir, "Assets", "51.jpg");
            if (!File.Exists(path)) path = Path.Combine(baseDir, "Assets", "tp.png");
            if (!File.Exists(path)) return;
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = 1600,
                UriSource = new Uri(path, UriKind.Absolute),
            };
            HeroBorder.Background = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.UniformToFill,
            };
        }
        catch (Exception ex)
        {
            App.TryLog(ex.ToString());
        }
    }

    private async Task LoadAnnouncementsAsync()
    {
        try
        {
            var result = await OfficialNewsService.FetchAsync();
            if (_unloaded) return;
            _announcements = result.Items.ToList();
            AnnouncementSourceText.Text = result.FromNetwork
                ? "官方源 · id5.163.com"
                : "本地缓存";
        }
        catch
        {
            _announcements = Store.Instance.Announcements.ToList();
            AnnouncementSourceText.Text = "本地缓存";
        }
    }

    private async Task LoadAccountsAsync()
    {
        _loadingAccounts = true;
        try
        {
            var options = new List<HomeAccountOption>();
            string selected = "";

            var config = await EngineConfigStore.LoadAsync(Store.Instance.Settings.EngineGameId);
            _engineAccounts = config.Loaded;
            if (config.Loaded)
            {
                foreach (var account in config.Accounts)
                    options.Add(new HomeAccountOption(account.Uuid, $"{account.Display} · {account.ChannelLabel}"));
                // The starred account is stored in the engine config as auto-h55.
                selected = config.SelectedUuid;
            }
            else
            {
                foreach (var account in Store.Instance.Accounts)
                    options.Add(new HomeAccountOption(account.Uuid, $"{account.Name} · {ChannelCatalog.Find(account.Channel).Name}"));
                selected = "";
            }

            if (_unloaded) return;
            _accountOptions = options;
            HomeAccountCombo.ItemsSource = options;
            HomeAccountCombo.SelectedItem = options.FirstOrDefault(o => o.Uuid == selected);
            HomeAccountCombo.PlaceholderText = options.Count > 0
                ? "未收藏账号（手动登录）"
                : "未读取到渠道账号";
        }
        catch
        {
            _engineAccounts = false;
            _accountOptions.Clear();
            HomeAccountCombo.ItemsSource = _accountOptions;
            HomeAccountCombo.SelectedItem = null;
        }
        finally
        {
            _loadingAccounts = false;
        }
    }

    private void Refresh()
    {
        if (_unloaded) return;
        AnnouncementList.ItemsSource = _announcements.Take(3).ToList();
        RefreshStatus();
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshStatus);
    }

    private async Task PrewarmEngineAsync()
    {
        var settings = Store.Instance.Settings;
        if (!settings.UseLoginEngine || !settings.AutoStartEngine ||
            !EngineService.Instance.IsInstalled || EngineService.Instance.IsReady)
            return;
        try
        {
            await EngineService.Instance.StartAsync();
        }
        catch
        {
            // A manual launch will retry and surface a useful error.
        }
    }

    private void OnLocateGame(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null || _gameBusy) return;
        try
        {
            string? path = NativeFileDialog.PickExecutable(
                WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!string.Equals(Path.GetFileName(path), "dwrg.exe", StringComparison.OrdinalIgnoreCase))
            {
                App.MainWindow.ShowInfo("请选择第五人格的 dwrg.exe。", InfoBarSeverity.Warning);
                return;
            }
            Store.Instance.Settings.GamePath = path;
            Store.Instance.SaveSettings();
            RefreshStatus();
            App.MainWindow.ShowInfo("游戏路径已定位。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"定位游戏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnDownloadGame(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null || _gameBusy) return;
        if (!EngineService.Instance.IsInstalled)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://id5.163.com/") { UseShellExecute = true });
                App.MainWindow.ShowInfo("未找到登录引擎，已打开第五人格官方下载页。",
                    InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                App.MainWindow.ShowInfo($"无法打开官方下载页：{ex.Message}", InfoBarSeverity.Error);
            }
            return;
        }

        var dialog = new DownloadGameDialog();
        App.MainWindow.ShowModal(dialog);
        await dialog.WaitForCloseAsync();
        App.MainWindow.HideModal();
        string current = Store.Instance.Settings.GamePath;
        if (string.IsNullOrWhiteSpace(current) || !File.Exists(current))
        {
            string detected = Store.DetectGamePath();
            if (detected.Length > 0)
            {
                Store.Instance.Settings.GamePath = detected;
                Store.Instance.SaveSettings();
            }
        }
        RefreshStatus();
    }

    private void RefreshEngineStatus()
    {
        if (_unloaded) return;

        var engine = EngineService.Instance;
        bool installed = engine.IsInstalled;
        string text;
        Color color;

        if (!installed)
        {
            text = "登录服务未安装";
            color = Color.FromArgb(255, 224, 75, 75);
        }
        else if (_engineStarting || engine.State == EngineState.Starting)
        {
            text = "登录服务启动中…";
            color = Color.FromArgb(255, 216, 179, 108);
        }
        else if (engine.State == EngineState.Ready && engine.IsRunning)
        {
            text = "登录服务已就绪";
            color = Color.FromArgb(255, 111, 197, 140);
        }
        else if (engine.State == EngineState.Error)
        {
            text = "登录服务启动失败 · 点击重试";
            color = Color.FromArgb(255, 224, 75, 75);
        }
        else if (engine.State is EngineState.Stopping)
        {
            text = "登录服务停止中…";
            color = Color.FromArgb(255, 216, 179, 108);
        }
        else
        {
            text = "登录服务未启动 · 点击启动";
            color = Color.FromArgb(255, 216, 179, 108);
        }

        EngineStatusText.Text = text;
        EngineStatusDot.Fill = new SolidColorBrush(color);
        bool busy = _engineStarting || engine.State is EngineState.Starting or EngineState.Stopping;
        EngineQuickButton.IsEnabled = installed && !busy && engine.State != EngineState.Ready;
        EngineQuickIcon.Glyph = engine.State == EngineState.Ready ? "\uE73E" : "\uE768";
    }

    private async void OnManualStartEngine(object sender, RoutedEventArgs e)
    {
        if (_engineStarting) return;
        if (!EngineService.Instance.IsInstalled)
        {
            App.MainWindow?.ShowInfo("登录服务未安装，请确认程序目录完整。", InfoBarSeverity.Error);
            return;
        }

        _engineStarting = true;
        RefreshStatus();
        try
        {
            await EngineService.Instance.StartAsync();
            App.MainWindow?.ShowInfo("登录服务已启动。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"登录服务启动失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _engineStarting = false;
            RefreshStatus();
        }
    }

    private void RefreshStatus()
    {
        if (_launching || _gameBusy || _unloaded) return;

        var settings = Store.Instance.Settings;
        bool running = GameLauncher.IsRunning();
        bool hasGame = !string.IsNullOrWhiteSpace(settings.GamePath) && File.Exists(settings.GamePath);

        StatusText.Text = running ? "游戏运行中" : "游戏未运行";
        StatusDot.Fill = new SolidColorBrush(running
            ? Color.FromArgb(255, 111, 197, 140)
            : Color.FromArgb(255, 158, 151, 138));

        DownloadGameButton.Visibility = hasGame ? Visibility.Collapsed : Visibility.Visible;
        HomeAccountCombo.IsEnabled = true;
        HomeAccountCombo.PlaceholderText = _accountOptions.Count > 0
            ? "未收藏账号（手动登录）"
            : "未读取到渠道账号";

        var engine = EngineService.Instance;
        bool engineReady = !settings.UseLoginEngine ||
                           (engine.State == EngineState.Ready && engine.IsRunning);

        if (_countdown > 0)
        {
            RefreshEngineStatus();
            return;
        }

        if (running)
        {
            LaunchButton.IsEnabled = true;
            LaunchText.Text = "关闭游戏";
        }
        else if (!hasGame)
        {
            LaunchButton.IsEnabled = true;
            LaunchText.Text = "下载 / 定位游戏";
        }
        else if (!engineReady)
        {
            LaunchButton.IsEnabled = false;
            LaunchText.Text = _engineStarting || engine.State == EngineState.Starting
                ? "登录服务启动中…"
                : "登录服务未就绪";
        }
        else
        {
            LaunchButton.IsEnabled = true;
            LaunchText.Text = "启动游戏";
        }

        RefreshEngineStatus();
    }

    private async void OnAccountSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingAccounts || HomeAccountCombo.SelectedItem is not HomeAccountOption option)
            return;
        if (string.IsNullOrWhiteSpace(option.Uuid)) return;

        // With the toggle off, selecting an account is only a local/UI choice;
        // do not write auto-h55 and silently re-enable automatic login.
        if (!Store.Instance.Settings.AutoScanLogin)
        {
            Store.Instance.SetDefault(option.Uuid);
            return;
        }

        try
        {
            if (_engineAccounts)
            {
                var config = await EngineConfigStore.LoadAsync(Store.Instance.Settings.EngineGameId);
                if (config.Loaded)
                {
                    await EngineConfigStore.SaveAsync(new EngineConfigDraft(
                        config.GameId,
                        config.GamePath,
                        config.ProxyMode,
                        config.LoginDelay,
                        config.AutoClose,
                        option.Uuid));
                    Store.Instance.SetDefault(option.Uuid);
                    App.MainWindow?.ShowInfo("已选择账号；启动游戏会直接登录该账号。",
                        InfoBarSeverity.Success);
                }
            }
            else
            {
                Store.Instance.SetDefault(option.Uuid);
            }
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"切换账号失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnAnnouncementClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not Announcement item) return;
        if (string.IsNullOrWhiteSpace(item.Url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(item.Url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"打开公告失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnOpenAnnouncements(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(OfficialNewsService.SourceUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"打开官方公告失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void SetBusy(bool active, string text)
    {
        _gameBusy = active;
        LaunchRing.IsActive = active;
        LaunchRing.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        LaunchIcon.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        if (active)
        {
            LaunchButton.IsEnabled = false;
            LaunchText.Text = text;
        }
    }

    private async Task WaitForGameWindowAsync()
    {
        for (int i = 0; i < 60; i++)
        {
            if (_unloaded) return;
            int pid = GameLauncher.FindProcessId(Store.Instance.Settings.GamePath);
            if (pid != 0)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    if (process.MainWindowHandle != IntPtr.Zero) break;
                }
                catch { }
            }
            await Task.Delay(250);
        }
    }

    private async void OnLaunch(object sender, RoutedEventArgs e)
    {
        if (_gameBusy || _launching) return;

        if (GameLauncher.IsRunning())
        {
            SetBusy(true, "正在关闭游戏…");
            try
            {
                await GameLauncher.CloseGameAsync();
                App.MainWindow?.ShowInfo("游戏已关闭。", InfoBarSeverity.Informational);
            }
            catch (Exception ex)
            {
                App.MainWindow?.ShowInfo($"关闭游戏失败：{ex.Message}", InfoBarSeverity.Error);
            }
            finally
            {
                SetBusy(false, "启动游戏");
                RefreshStatus();
            }
            return;
        }

        var settings = Store.Instance.Settings;
        if (string.IsNullOrWhiteSpace(settings.GamePath) || !File.Exists(settings.GamePath))
        {
            OnDownloadGame(sender, e);
            return;
        }

        if (settings.UseLoginEngine && !EngineService.Instance.IsReady)
        {
            App.MainWindow?.ShowInfo("登录服务尚未就绪，请等待启动完成或点击登录服务按钮手动重试。",
                InfoBarSeverity.Warning);
            return;
        }

        if (settings.LaunchDelay > 0)
        {
            _countdown = settings.LaunchDelay;
            LaunchButton.IsEnabled = false;
            LaunchText.Text = $"{_countdown} 秒后启动";
            _launchTimer?.Stop();
            _launchTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _launchTimer.Tick += (_, _) =>
            {
                _countdown--;
                if (_countdown > 0)
                {
                    LaunchText.Text = $"{_countdown} 秒后启动";
                    return;
                }
                _launchTimer?.Stop();
                LaunchText.Text = "启动游戏";
                _ = StartGameAsync();
            };
            _launchTimer.Start();
            App.MainWindow?.ShowInfo($"将在 {_countdown} 秒后启动游戏");
            return;
        }

        _ = StartGameAsync();
    }

    private async Task StartGameAsync()
    {
        if (_launching) return;

        var settings = Store.Instance.Settings;
        bool engineUsed = false;
        bool autoLogin = false;
        long loginLogOffset = 0;
        _launching = true;
        SetBusy(true, "准备启动…");

        try
        {
            string selectedUuid = (HomeAccountCombo.SelectedItem as HomeAccountOption)?.Uuid ?? "";
            autoLogin = settings.AutoScanLogin && !string.IsNullOrWhiteSpace(selectedUuid);

            if (settings.UseLoginEngine && EngineService.Instance.IsInstalled)
            {
                SetBusy(true, "登录服务准备中…");
                App.MainWindow?.ShowInfo("正在准备登录服务，请稍候…", InfoBarSeverity.Informational);
                try
                {
                    var config = await EngineConfigStore.LoadAsync(settings.EngineGameId);
                    bool needRestart = !EngineService.Instance.IsReady;
                    if (!needRestart && config.Loaded)
                    {
                        bool configAuto = !string.IsNullOrWhiteSpace(config.SelectedUuid);
                        needRestart = autoLogin
                            ? !string.Equals(config.SelectedUuid, selectedUuid, StringComparison.OrdinalIgnoreCase)
                            : configAuto;
                    }

                    if (needRestart)
                    {
                        if (EngineService.Instance.IsRunning)
                            await EngineService.Instance.StopAsync();
                        await EngineConfigStore.ApplyAutoSelectionAsync(
                            settings.EngineGameId, autoLogin, selectedUuid);
                        await EngineService.Instance.StartAsync();
                    }
                    engineUsed = true;
                }
                catch (Exception ex)
                {
                    App.MainWindow?.ShowInfo($"登录引擎未启动，将直接启动游戏：{ex.Message}",
                        InfoBarSeverity.Warning);
                }
            }

            SetBusy(true, autoLogin ? "正在登录…" : "正在启动游戏…");
            loginLogOffset = GameLoginGate.CaptureLogOffset();
            bool started = engineUsed
                ? await EngineService.Instance.LaunchGameAsync(settings.GamePath, settings.GameArgs)
                : GameLauncher.Launch(settings.GamePath, settings.GameArgs);
            if (!started)
            {
                App.MainWindow?.ShowInfo("启动失败，请检查游戏路径与权限", InfoBarSeverity.Error);
                return;
            }

            if (engineUsed && autoLogin)
            {
                // Hide the game's login window until the engine reports login
                // success/failure, so the user does not see a login popup.
                await GameLoginGate.MinimizeUntilLoggedInAsync(
                    settings.GamePath, loginLogOffset, TimeSpan.FromSeconds(25));
            }
            else
            {
                await WaitForGameWindowAsync();
            }

            App.MainWindow?.ShowInfo(engineUsed ? "登录引擎已就绪，第五人格正在启动…" : "第五人格正在启动…",
                InfoBarSeverity.Success);
            SetBusy(false, "启动游戏");

            if (settings.AutoClose && !engineUsed)
            {
                _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
                _closeTimer.Tick += (_, _) =>
                {
                    _closeTimer?.Stop();
                    App.MainWindow?.Close();
                };
                _closeTimer.Start();
            }
            else if (settings.AutoMinimize)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    App.MainWindow?.Minimize();
                };
                timer.Start();
            }
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"启动失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _launching = false;
            if (_gameBusy) SetBusy(false, "启动游戏");
            LaunchButton.IsEnabled = true;
            RefreshStatus();
        }
    }
}
