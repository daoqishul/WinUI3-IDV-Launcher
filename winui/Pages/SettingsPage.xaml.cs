using System.Diagnostics;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI;

namespace IdvLauncher.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loading;
    private bool _gameSettingsLoading;
    private bool _gameAutoSaving;
    private int _selectedQuality = 2;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadState();
    }

    private void LoadState()
    {
        _loading = true;
        var settings = Store.Instance.Settings;

        GamePathText.Text = string.IsNullOrWhiteSpace(settings.GamePath) ? "尚未设置，请选择 dwrg.exe" : settings.GamePath;
        GamePathText.Foreground = string.IsNullOrWhiteSpace(settings.GamePath)
            ? new SolidColorBrush(Color.FromArgb(255, 205, 90, 80))
            : null;
        if (!string.IsNullOrWhiteSpace(settings.GamePath))
            GamePathText.ClearValue(TextBlock.ForegroundProperty);

        MinimizeToggle.IsOn = settings.AutoMinimize;
        CloseToggle.IsOn = settings.AutoClose;
        DelayBox.Value = settings.LaunchDelay;
        EngineToggle.IsOn = settings.UseLoginEngine;
        SidebarToggle.IsOn = settings.SidebarCollapsed;
        RememberBoundsToggle.IsOn = settings.RememberWindowBounds;
        TrayToggle.IsOn = settings.EnableTray;

        SelectTag(ThemeCombo, settings.Theme);
        SelectTag(BackdropCombo, settings.BackdropMode);
        SelectTag(CornerCombo, settings.SquareCorners ? 1 : 0);
        SelectTag(PcModeCombo, settings.PcEmulatorMode);

        _loading = false;
        _ = LoadGameSettingsAsync();
    }

    private async Task LoadGameSettingsAsync()
    {
        _gameSettingsLoading = true;
        try
        {
            var snapshot = await GameSettingsService.LoadAsync();
            if (!snapshot.Loaded)
            {
                GameSettingsHint.Text = snapshot.Error;
                ApplyGameSettingsButton.IsEnabled = false;
                GameDisplayCombo.IsEnabled = false;
                ResolutionScaleCombo.IsEnabled = false;
                HighFpsBattleToggle.IsEnabled = false;
                HighFpsOutBattleToggle.IsEnabled = false;
                AntiAliasToggle.IsEnabled = false;
                FrameCheckToggle.IsEnabled = false;
                FovCombo.IsEnabled = false;
                QualityLowButton.IsEnabled = false;
                QualityMidButton.IsEnabled = false;
                QualityHighButton.IsEnabled = false;
                return;
            }

            _selectedQuality = snapshot.Quality switch
            {
                "low" => 0,
                "mid" => 1,
                _ => 2,
            };
            ApplyQualitySelection(_selectedQuality);
            SelectTag(GameDisplayCombo, snapshot.Fullscreen ? 1 : 0);
            SelectTag(ResolutionScaleCombo, snapshot.ResolutionScale);
            SelectTag(FovCombo, snapshot.WideFov ? 1 : 0);
            HighFpsBattleToggle.IsOn = snapshot.HighFpsBattle;
            HighFpsOutBattleToggle.IsOn = snapshot.HighFpsOutBattle;
            AntiAliasToggle.IsOn = snapshot.AntiAlias;
            FrameCheckToggle.IsOn = snapshot.FrameCheck;
            GameSettingsHint.Text = "关闭游戏后应用；下次启动游戏生效。";
            ApplyGameSettingsButton.IsEnabled = !GameLauncher.IsRunning();
            QualityLowButton.IsEnabled = true;
            QualityMidButton.IsEnabled = true;
            QualityHighButton.IsEnabled = true;
            GameDisplayCombo.IsEnabled = true;
            ResolutionScaleCombo.IsEnabled = true;
            HighFpsBattleToggle.IsEnabled = true;
            HighFpsOutBattleToggle.IsEnabled = true;
            AntiAliasToggle.IsEnabled = true;
            FrameCheckToggle.IsEnabled = true;
            FovCombo.IsEnabled = true;
        }
        finally
        {
            _gameSettingsLoading = false;
        }
    }

    private void OnOpenKeyEditor(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.NavigateTo("keys");
    }

    private void OnQualityCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || !int.TryParse(tag, out int quality)) return;
        _selectedQuality = Math.Clamp(quality, 0, 2);
        ApplyQualitySelection(_selectedQuality);
        _ = AutoSaveGameSettingsAsync();
    }

    private void OnGameSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_gameSettingsLoading || _gameAutoSaving) return;
        _ = AutoSaveGameSettingsAsync();
    }

    private void OnGameComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_gameSettingsLoading || _gameAutoSaving) return;
        _ = AutoSaveGameSettingsAsync();
    }

    private void OnPcModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.PcEmulatorMode = SelectedTag(PcModeCombo, 0);
        Store.Instance.SaveSettings();
    }

    private GameSettingsOptions ReadGameSettingsOptions() => new(
        _selectedQuality switch
        {
            0 => GameQualityPreset.Smooth,
            1 => GameQualityPreset.Balanced,
            _ => GameQualityPreset.High,
        },
        SelectedTag(GameDisplayCombo, 1) == 1,
        Math.Clamp(SelectedTag(ResolutionScaleCombo, 5), 1, 5),
        HighFpsBattleToggle.IsOn,
        HighFpsOutBattleToggle.IsOn,
        AntiAliasToggle.IsOn,
        FrameCheckToggle.IsOn,
        SelectedTag(FovCombo, 1) == 1);

    private async Task AutoSaveGameSettingsAsync()
    {
        if (_gameSettingsLoading || _gameAutoSaving) return;
        _gameAutoSaving = true;
        try
        {
            if (GameLauncher.IsRunning())
            {
                GameSettingsHint.Text = "游戏运行中，关闭游戏后修改才会写入；当前选择尚未保存。";
                return;
            }
            await GameSettingsService.ApplyAsync(ReadGameSettingsOptions());
            GameSettingsHint.Text = "选择已保存；下次启动游戏生效。";
        }
        catch (Exception ex)
        {
            GameSettingsHint.Text = $"保存失败：{ex.Message}";
        }
        finally
        {
            _gameAutoSaving = false;
        }
    }

    private void ApplyQualitySelection(int quality)
    {
        Brush selected = Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? res) &&
                         res is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(255, 60, 150, 230));
        Brush transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        QualityLowButton.BorderBrush = quality == 0 ? selected : transparent;
        QualityMidButton.BorderBrush = quality == 1 ? selected : transparent;
        QualityHighButton.BorderBrush = quality == 2 ? selected : transparent;
        QualitySelectionText.Text = quality switch
        {
            0 => "当前画质：性能",
            1 => "当前画质：效率",
            _ => "当前画质：精美",
        };
    }

    private async void OnApplyGameSettings(object sender, RoutedEventArgs e)
    {
        if (_gameSettingsLoading) return;
        if (GameLauncher.IsRunning())
        {
            App.MainWindow?.ShowInfo("请先关闭游戏，再应用游戏设置。", InfoBarSeverity.Warning);
            return;
        }

        var options = ReadGameSettingsOptions();

        try
        {
            await GameSettingsService.ApplyAsync(options);
            App.MainWindow?.ShowInfo("游戏设置已保存，下次启动游戏生效。", InfoBarSeverity.Success);
            await LoadGameSettingsAsync();
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"应用游戏设置失败：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private static void SelectTag(ComboBox combo, int tag)
    {
        foreach (var item in combo.Items.OfType<ComboBoxItem>())
        {
            if (int.TryParse(item.Tag as string, out int value) && value == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
    }

    private static int SelectedTag(ComboBox combo, int fallback)
    {
        return combo.SelectedItem is ComboBoxItem { Tag: string text } &&
               int.TryParse(text, out int value)
            ? value
            : fallback;
    }

    private void OnBrowseGame(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;
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
            LoadState();
            App.MainWindow.ShowInfo("游戏路径已更新", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            App.TryLog(ex.ToString());
            App.MainWindow.ShowInfo($"无法打开文件选择器：{ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnDetectGame(object sender, RoutedEventArgs e)
    {
        string path = Store.DetectGamePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            App.MainWindow?.ShowInfo("没有找到游戏，请手动选择 dwrg.exe", InfoBarSeverity.Warning);
            return;
        }
        Store.Instance.Settings.GamePath = path;
        Store.Instance.SaveSettings();
        LoadState();
        App.MainWindow?.ShowInfo("已找到游戏路径", InfoBarSeverity.Success);
    }

    private void OnMinimizeToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.AutoMinimize = MinimizeToggle.IsOn;
        Store.Instance.SaveSettings();
    }

    private void OnCloseToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.AutoClose = CloseToggle.IsOn;
        Store.Instance.SaveSettings();
    }

    private void OnDelayChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        Store.Instance.Settings.LaunchDelay = (int)Math.Clamp(args.NewValue, 0, 30);
        Store.Instance.SaveSettings();
    }

    private void OnEngineToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.UseLoginEngine = EngineToggle.IsOn;
        Store.Instance.SaveSettings();
        if (EngineToggle.IsOn && EnginePaths.IsElevated && EngineService.Instance.IsInstalled)
            _ = EngineService.Instance.StartAsync();
    }

    private void OnSidebarToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.SidebarCollapsed = SidebarToggle.IsOn;
        Store.Instance.SaveSettings();
        App.MainWindow?.SetPaneOpen(!SidebarToggle.IsOn);
    }

    private void OnRememberBoundsToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.RememberWindowBounds = RememberBoundsToggle.IsOn;
        Store.Instance.SaveSettings();
        if (RememberBoundsToggle.IsOn)
            App.MainWindow?.CaptureWindowBounds();
    }

    private void OnTrayToggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.EnableTray = TrayToggle.IsOn;
        Store.Instance.SaveSettings();
        App.MainWindow?.SetTrayEnabled(TrayToggle.IsOn);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.Theme = SelectedTag(ThemeCombo, 0);
        Store.Instance.SaveSettings();
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(120);
            App.MainWindow?.ApplyTheme();
        });
    }

    private void OnBackdropChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.BackdropMode = SelectedTag(BackdropCombo, 1);
        Store.Instance.SaveSettings();
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(120);
            App.MainWindow?.ApplyTheme();
        });
    }

    private void OnCornerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        Store.Instance.Settings.SquareCorners = SelectedTag(CornerCombo, 0) == 1;
        Store.Instance.SaveSettings();
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(120);
            App.MainWindow?.ApplyCornerStyle();
        });
    }

    private async void OnShowDisclaimer(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not null)
            await App.MainWindow.ShowDisclaimerDocumentAsync();
    }

    private void OnDownloadGame(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                "https://wiki.biligame.com/dwrg/PC%E4%BA%92%E9%80%9A%E7%89%88")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            App.MainWindow?.ShowInfo($"无法打开下载页：{ex.Message}", InfoBarSeverity.Error);
        }
    }
}
