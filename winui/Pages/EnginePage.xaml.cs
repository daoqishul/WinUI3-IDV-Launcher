using System.Diagnostics;
using IdvLauncher.Models;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace IdvLauncher.Pages;

public sealed partial class EnginePage : Page
{
    private DispatcherTimer? _pollTimer;
    private bool _subscribed;
    private bool _loadingConfig;

    public EnginePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            EngineService.Instance.StateChanged += OnEngineStateChanged;
            _subscribed = true;
        }

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _pollTimer.Tick += (_, _) =>
        {
            EngineService.Instance.Poll();
            UpdateStatus();
            if (EngineService.Instance.IsRunning)
                LogText.Text = EngineService.Instance.LogTail;
        };
        _pollTimer.Start();

        await RefreshAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _pollTimer?.Stop();
        if (_subscribed)
        {
            EngineService.Instance.StateChanged -= OnEngineStateChanged;
            _subscribed = false;
        }
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateStatus();
            LogText.Text = EngineService.Instance.LogTail;
        });
    }

    private async Task RefreshAsync()
    {
        try
        {
            var settings = Store.Instance.Settings;
            if (string.IsNullOrWhiteSpace(settings.EnginePath))
                settings.EnginePath = EnginePaths.DetectInstallRoot();

            var config = await EngineConfigStore.LoadAsync(settings.EngineGameId);
            if (config.Loaded && !string.IsNullOrWhiteSpace(config.GameId))
                settings.EngineGameId = config.GameId;
            _loadingConfig = true;
            ProxyModeCombo.SelectedIndex = config.ProxyMode switch
            {
                "process" => 1,
                "global" => 2,
                _ => 0,
            };
            LoginDelayBox.Value = config.LoginDelay;
            AutoCloseToggle.IsOn = config.AutoClose;
            AutoScanToggle.IsOn = settings.AutoScanLogin;
            AutoStartEngineToggle.IsOn = settings.AutoStartEngine;
            GameArgsBox.Text = settings.GameArgs;
            GamePathBox.Text = !string.IsNullOrWhiteSpace(config.GamePath)
                ? config.GamePath
                : settings.GamePath;

            var accounts = config.Accounts.ToList();
            AccountCombo.ItemsSource = accounts;
            if (!string.IsNullOrWhiteSpace(config.SelectedUuid))
            {
                var selected = accounts.FirstOrDefault(a => a.Uuid == config.SelectedUuid);
                if (selected is null)
                {
                    selected = new EngineAccount
                    {
                        Uuid = config.SelectedUuid,
                        Name = "（配置中的账号，记录未读取到）",
                        Channel = "",
                    };
                    accounts.Insert(0, selected);
                    AccountCombo.ItemsSource = accounts;
                }
                AccountCombo.SelectedValue = selected.Uuid;
            }
            else
            {
                AccountCombo.SelectedIndex = -1;
            }

            AccountCombo.PlaceholderText = accounts.Count == 0
                ? "未读取到渠道账号"
                : "未选择（手动登录）";
            _loadingConfig = false;

            if (!EnginePaths.IsElevated)
            {
                ShowInfo("当前调试版未以管理员身份运行：只能预览页面。", InfoBarSeverity.Warning);
            }
            else if (!EngineService.Instance.IsInstalled)
            {
                ShowInfo("登录服务未安装或目录不完整。", InfoBarSeverity.Error);
            }
            else if (!config.Loaded)
            {
                ShowInfo(config.Error, InfoBarSeverity.Warning);
            }
            else if (!config.HasConfig)
            {
                ShowInfo("登录配置尚未生成，启动游戏时自动创建。", InfoBarSeverity.Informational);
            }
            else
            {
                EngineInfo.IsOpen = false;
            }

            UpdateStatus();
            LogText.Text = string.IsNullOrWhiteSpace(EngineService.Instance.LogTail)
                ? EngineConfigStore.ReadLogTail()
                : EngineService.Instance.LogTail;
        }
        catch (Exception ex)
        {
            ShowInfo($"读取登录引擎配置失败：{ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            _loadingConfig = false;
        }
    }

    private void UpdateStatus()
    {
        var engine = EngineService.Instance;
        StatusText.Text = engine.StatusText;

        (string badge, Color color) = engine.State switch
        {
            EngineState.Ready => ("已就绪", Color.FromArgb(255, 111, 197, 140)),
            EngineState.Starting => ("启动中", Color.FromArgb(255, 216, 179, 108)),
            EngineState.Stopping => ("停止中", Color.FromArgb(255, 216, 179, 108)),
            EngineState.Error => ("出错", Color.FromArgb(255, 232, 106, 96)),
            _ => ("未运行", Color.FromArgb(255, 158, 151, 138)),
        };
        StateBadgeText.Text = badge;
        StatusDot.Fill = new SolidColorBrush(color);
        StateBadge.Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
        StateBadgeText.Foreground = new SolidColorBrush(color);

        bool elevated = EnginePaths.IsElevated;
        bool installed = engine.IsInstalled;
        AccountsButton.IsEnabled = elevated && installed;
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        EngineInfo.Severity = severity;
        EngineInfo.Message = message;
        EngineInfo.IsOpen = true;
    }

    private void SaveUiSettings()
    {
        var settings = Store.Instance.Settings;
        settings.GamePath = GamePathBox.Text.Trim();
        settings.GameArgs = GameArgsBox.Text.Trim();
        settings.AutoStartEngine = AutoStartEngineToggle.IsOn;
        Store.Instance.SaveSettings();
    }

    private async Task<bool> SaveConfigAsync()
    {
        SaveUiSettings();
        string mode = (ProxyModeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "compat";
        string selected = AutoScanToggle.IsOn
            ? AccountCombo.SelectedValue as string
              ?? (AccountCombo.SelectedItem as EngineAccount)?.Uuid
              ?? ""
            : "";
        int delay = double.IsNaN(LoginDelayBox.Value) ? 6 : (int)LoginDelayBox.Value;

        await EngineConfigStore.SaveAsync(new EngineConfigDraft(
            Store.Instance.Settings.EngineGameId,
            GamePathBox.Text.Trim(),
            mode,
            delay,
            AutoCloseToggle.IsOn,
            selected));

        RestartNeededText.Visibility = EngineService.Instance.IsRunning ? Visibility.Visible : Visibility.Collapsed;
        return true;
    }

    private async void OnAutoScanToggled(object sender, RoutedEventArgs e)
    {
        if (_loadingConfig) return;
        Store.Instance.Settings.AutoScanLogin = AutoScanToggle.IsOn;
        Store.Instance.SaveSettings();

        try
        {
            string selected = AccountCombo.SelectedValue as string
                              ?? (AccountCombo.SelectedItem as EngineAccount)?.Uuid
                              ?? "";
            await EngineConfigStore.ApplyAutoSelectionAsync(
                Store.Instance.Settings.EngineGameId,
                AutoScanToggle.IsOn,
                AutoScanToggle.IsOn ? selected : "");
            ShowInfo(AutoScanToggle.IsOn
                    ? "自动扫码登录已开启；启动游戏会自动登录当前账号。"
                    : "已关闭自动扫码；启动游戏后可在内置登录界面手动选号。",
                InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowInfo($"同步自动登录设置失败：{ex.Message}", InfoBarSeverity.Warning);
        }
    }

    private async void OnSaveConfig(object sender, RoutedEventArgs e)
    {
        try
        {
            await SaveConfigAsync();
            ShowInfo("登录配置已保存；下次启动游戏时自动应用。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
    }

    private void OnOpenAccounts(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.NavigateTo("accounts");
    }

    private void OnBrowseGame(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;
        try
        {
            string? path = NativeFileDialog.PickExecutable(
                WindowNative.GetWindowHandle(App.MainWindow));
            if (string.IsNullOrWhiteSpace(path)) return;
            GamePathBox.Text = path;
            Store.Instance.Settings.GamePath = path;
            Store.Instance.SaveSettings();
            ShowInfo("游戏路径已更新。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo($"选择游戏失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnDetectGame(object sender, RoutedEventArgs e)
    {
        string path = Store.DetectGamePath();
        if (path.Length == 0)
        {
            ShowInfo("没有自动检测到 dwrg.exe，请点「浏览」手动定位。", InfoBarSeverity.Warning);
            return;
        }
        GamePathBox.Text = path;
        Store.Instance.Settings.GamePath = path;
        Store.Instance.SaveSettings();
        ShowInfo("已检测到游戏路径。", InfoBarSeverity.Success);
    }

    private void OnRefreshLog(object sender, RoutedEventArgs e)
    {
        EngineService.Instance.RefreshLog();
        LogText.Text = string.IsNullOrWhiteSpace(EngineService.Instance.LogTail)
            ? EngineConfigStore.ReadLogTail()
            : EngineService.Instance.LogTail;
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(EnginePaths.LogPath))
            {
                ShowInfo("日志文件尚未生成。", InfoBarSeverity.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{EnginePaths.LogPath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ShowInfo($"打开日志失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }
}
