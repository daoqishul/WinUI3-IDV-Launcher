using System.Security.Cryptography;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IdvLauncher.Pages;

public sealed partial class CloudSyncPage : Page
{
    private bool _loading;
    private bool _subscribed;
    private bool _unloaded;

    public CloudSyncPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _unloaded = false;
        if (!_subscribed)
        {
            EngineService.Instance.StateChanged += OnEngineStateChanged;
            _subscribed = true;
        }
        await LoadSettingsAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _unloaded = true;
        if (_subscribed)
        {
            EngineService.Instance.StateChanged -= OnEngineStateChanged;
            _subscribed = false;
        }
    }

    private void OnEngineStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () => await LoadSettingsAsync());
    }

    private async Task LoadSettingsAsync()
    {
        if (_unloaded) return;
        bool ready = EngineService.Instance.IsReady;
        CloudStatusText.Text = ready
            ? "登录服务已就绪。"
            : "登录服务未就绪，请回首页手动启动后重试。";
        PushButton.IsEnabled = ready;
        PullButton.IsEnabled = ready;
        SyncButton.IsEnabled = ready;
        DeleteButton.IsEnabled = ready;
        AutoSyncToggle.IsEnabled = ready;
        if (!ready) return;

        try
        {
            var settings = await EngineApiClient.Instance.GetCloudSyncSettingsAsync();
            if (_unloaded) return;
            if (!settings.Success)
            {
                ShowInfo(settings.Error, InfoBarSeverity.Warning);
                return;
            }
            _loading = true;
            ConsentCheck.IsChecked = settings.ConsentAck;
            AutoSyncToggle.IsOn = settings.AutoSync;
            if (!string.IsNullOrWhiteSpace(settings.SavedMasterKey))
                MasterKeyBox.Password = settings.SavedMasterKey;
            _loading = false;
        }
        catch (Exception ex)
        {
            ShowInfo($"读取云存档设置失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnMasterKeyChanged(object sender, RoutedEventArgs e)
    {
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
    }

    private async void OnAutoSyncToggled(object sender, RoutedEventArgs e)
    {
        if (_loading || !EngineService.Instance.IsReady) return;
        var result = await EngineApiClient.Instance.SaveCloudSyncSettingsAsync(
            ConsentCheck.IsChecked == true, AutoSyncToggle.IsOn, MasterKeyBox.Password);
        if (!result.Success)
        {
            ShowInfo(result.Error.Length > 0 ? result.Error : "保存自动同步失败。", InfoBarSeverity.Error);
        }
    }

    private void OnGenerateKey(object sender, RoutedEventArgs e)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        var bytes = RandomNumberGenerator.GetBytes(24);
        var sb = new System.Text.StringBuilder(24);
        foreach (byte b in bytes)
            sb.Append(chars[b % chars.Length]);
        MasterKeyBox.Password = sb.ToString();
        ShowInfo("已生成主密钥，请妥善保存。", InfoBarSeverity.Success);
    }

    private bool ValidateInput(out string error)
    {
        string key = MasterKeyBox.Password;
        if (key.Length < 12)
        {
            error = "主密钥至少需要 12 位。";
            return false;
        }
        int classes = 0;
        if (key.Any(char.IsLower)) classes++;
        if (key.Any(char.IsUpper)) classes++;
        if (key.Any(char.IsDigit)) classes++;
        if (key.Any(c => !char.IsLetterOrDigit(c))) classes++;
        if (classes < 3)
        {
            error = "主密钥至少需要包含 3 类字符。";
            return false;
        }
        if (ConsentCheck.IsChecked != true)
        {
            error = "请先勾选同意云同步存储与权限说明。";
            return false;
        }
        error = "";
        return true;
    }

    private async Task RunAsync(string action)
    {
        if (!ValidateInput(out string error))
        {
            ShowInfo(error, InfoBarSeverity.Warning);
            return;
        }

        try
        {
            var save = await EngineApiClient.Instance.SaveCloudSyncSettingsAsync(
                true, AutoSyncToggle.IsOn, MasterKeyBox.Password);
            if (!save.Success)
            {
                ShowInfo(save.Error.Length > 0 ? save.Error : "保存云同步设置失败。", InfoBarSeverity.Error);
                return;
            }

            var result = await EngineApiClient.Instance.RunCloudSyncAsync(
                action, MasterKeyBox.Password, true);
            if (!result.Success)
            {
                ShowInfo(result.Error.Length > 0 ? result.Error : "云同步失败。", InfoBarSeverity.Error);
                return;
            }

            string label = action switch
            {
                "push" => "上传",
                "pull" => "下载",
                _ => "双向同步",
            };
            ShowInfo($"{label}完成。", InfoBarSeverity.Success);
            CloudStatusText.Text = $"{label}完成。如果是下载，请回账号管理刷新列表。";
        }
        catch (Exception ex)
        {
            ShowInfo($"云同步失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void OnPush(object sender, RoutedEventArgs e) => await RunAsync("push");
    private async void OnPull(object sender, RoutedEventArgs e) => await RunAsync("pull");
    private async void OnSync(object sender, RoutedEventArgs e) => await RunAsync("sync");

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (!ValidateInput(out string error))
        {
            ShowInfo(error, InfoBarSeverity.Warning);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "删除云端存档",
            Content = "将删除云端保存的加密账号记录，且无法恢复。确定继续？",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var result = await EngineApiClient.Instance.DeleteCloudSyncAsync(MasterKeyBox.Password);
            ShowInfo(result.Success ? "云端存档已删除。" : result.Error,
                result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);
        }
        catch (Exception ex)
        {
            ShowInfo($"删除失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        if (_unloaded) return;
        CloudInfo.Severity = severity;
        CloudInfo.Message = string.IsNullOrWhiteSpace(message) ? "操作失败。" : message;
        CloudInfo.IsOpen = true;
    }
}
