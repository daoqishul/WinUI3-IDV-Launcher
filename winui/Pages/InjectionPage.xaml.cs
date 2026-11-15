using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IdvLauncher.Pages;

public sealed partial class InjectionPage : Page
{
    private bool _loading;
    private bool _applying;
    private bool _unloaded;
    private int _detectVersion;

    public InjectionPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _unloaded = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _unloaded = false;
        var settings = Store.Instance.Settings;
        _loading = true;
        FrameToggle.IsOn = settings.FrameUnlockEnabled;
        FrameRateBox.Value = Math.Clamp(settings.FrameRateTarget, 30, 500);
        _loading = false;
        UpdateEnabledState();
        UpdateGameDetection();
    }

    private async void UpdateGameDetection()
    {
        if (_unloaded) return;
        int version = ++_detectVersion;
        GameDetectText.Text = "正在检测游戏进程…";
        GameDetectText.Foreground = new SolidColorBrush(Color.FromArgb(255, 158, 151, 138));

        int pid;
        try
        {
            string gamePath = Store.Instance.Settings.GamePath;
            pid = await Task.Run(() => GameLauncher.FindProcessId(gamePath));
        }
        catch
        {
            pid = 0;
        }

        if (_unloaded || version != _detectVersion) return;
        if (pid != 0)
        {
            GameDetectText.Text = $"已检测到游戏进程，PID {pid}";
            GameDetectText.Foreground = new SolidColorBrush(Color.FromArgb(255, 111, 197, 140));
        }
        else
        {
            GameDetectText.Text = "未检测到游戏进程，请先进入游戏";
            GameDetectText.Foreground = new SolidColorBrush(Color.FromArgb(255, 205, 90, 80));
        }
    }

    private void OnRefreshGameDetection(object sender, RoutedEventArgs e) => UpdateGameDetection();

    private void UpdateEnabledState()
    {
        ApplyButton.IsEnabled = !_applying;
        FrameRateBox.IsEnabled = !_applying;
    }

    private void OnFrameToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.FrameUnlockEnabled = FrameToggle.IsOn;
        Store.Instance.SaveSettings();
        UpdateEnabledState();

        if (!FrameToggle.IsOn)
        {
            ShowInfo("已关闭解帧；已注入的帧率需要重启游戏恢复。", InfoBarSeverity.Informational);
            return;
        }

        ShowInfo("解帧已开启。设置目标帧率后点「应用解帧」。", InfoBarSeverity.Informational);
    }

    private void OnFrameRateChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading) return;
        int fps = double.IsNaN(args.NewValue) ? 120 : (int)Math.Clamp(args.NewValue, 30, 500);
        Store.Instance.Settings.FrameRateTarget = fps;
        Store.Instance.SaveSettings();
    }

    private async void OnApplyFrame(object sender, RoutedEventArgs e)
    {
        if (!FrameToggle.IsOn)
        {
            FrameToggle.IsOn = true;
        }
        await ApplyAsync();
    }

    private async Task ApplyAsync()
    {
        if (_applying) return;
        _applying = true;
        UpdateEnabledState();

        try
        {
            int fps = double.IsNaN(FrameRateBox.Value) ? 120 : (int)Math.Clamp(FrameRateBox.Value, 30, 500);
            string result = await FrameUnlockService.ApplyAsync(fps);
            if (!_unloaded)
            {
                UpdateGameDetection();
                ShowInfo($"{result} 目标帧率 {fps}。", InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _applying = false;
            UpdateEnabledState();
        }
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        if (_unloaded) return;
        FrameInfo.Severity = severity;
        FrameInfo.Message = message;
        FrameInfo.IsOpen = true;
    }
}
