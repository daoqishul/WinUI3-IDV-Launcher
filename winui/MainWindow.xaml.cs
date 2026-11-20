using System.Diagnostics;
using System.Runtime.InteropServices;
using IdvLauncher.Pages;
using IdvLauncher.Services;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;
using Color = Windows.UI.Color;
using Colors = Microsoft.UI.Colors;
using WinRT.Interop;

namespace IdvLauncher;

public sealed partial class MainWindow : Window
{
    public static MainWindow? Instance { get; private set; }

    private DispatcherTimer? _infoTimer;
    private bool _suppressNav;
    private bool _frameDisclaimerPrompting;
    private bool _disclaimerOpen;
    private TrayService? _tray;
    private bool _forceClose;
    private bool _shutdownStarted;
    private int _appliedBackdropMode = -1;
    private bool _applyingTheme;
    private int _settingsSaveQueued;

    public const string FrameAgreementText = "我同意并且后果自负封号由我来承担";

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        Title = "IDV 启动器";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarGrid);

        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        catch
        {
            // Mica is optional; the tinted background still works.
        }

        AppWindow.Title = "IDV";
        try
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
                presenter.IsAlwaysOnTop = false;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        }
        catch
        {
            // Older Windows App SDK builds keep the default caption height.
        }

        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "777.ico");
        if (File.Exists(iconPath))
        {
            try { AppWindow.SetIcon(iconPath); } catch { }
        }

        NavView.IsPaneOpen = !Store.Instance.Settings.SidebarCollapsed;
        ConfigureSettingsNavigation();
        SetTrayEnabled(Store.Instance.Settings.EnableTray);
        ApplyTheme();
        ApplyCornerStyle();
        RootGrid.ActualThemeChanged += (_, _) => ApplyTheme();
        RestoreBounds();
        ContentFrame.Navigated += (_, _) => ApplyCornerStyle();
        InitializeNavigation();

        RootGrid.Loaded += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (FindDescendant<Control>(NavView, "TogglePaneButton") is { } toggle)
                {
                    toggle.IsTabStop = false;
                    toggle.UseSystemFocusVisuals = false;
                }
            });
        };

        AppWindow.Closing += (_, args) =>
        {
            if (_forceClose) return;

            if (_tray is { IsVisible: true })
            {
                args.Cancel = true;
                AppWindow.Hide();
                TrimWorkingSet();
                _tray.ShowBalloon();
                return;
            }

            if (EngineService.Instance.IsRunning)
            {
                // The window must disappear immediately while the engine
                // flushes DNS/proxy state in the background.
                args.Cancel = true;
                BeginFastShutdown();
                return;
            }

            SaveBeforeClose();
        };

        Closed += (_, _) =>
        {
            _tray?.Dispose();
            _tray = null;
            App.ClearMainWindow();
        };
    }

    public void ApplyTheme()
    {
        if (_applyingTheme) return;
        _applyingTheme = true;
        int themeMode = Store.Instance.Settings.Theme;
        bool dark = themeMode switch
        {
            1 => true,
            2 => false,
            _ => Application.Current.RequestedTheme == ApplicationTheme.Dark,
        };

#if DEBUG
        string? fakeTheme = Environment.GetEnvironmentVariable("IDVLAUNCHER_FAKE_THEME");
        if (fakeTheme is "light" or "dark")
        {
            themeMode = fakeTheme == "light" ? 2 : 1;
            dark = fakeTheme == "dark";
        }
#endif

        RootGrid.RequestedTheme = themeMode switch
        {
            1 => ElementTheme.Dark,
            2 => ElementTheme.Light,
            _ => ElementTheme.Default,
        };

        int backdropMode = Store.Instance.Settings.BackdropMode;
        bool material = backdropMode is 1 or 2;
        if (backdropMode != _appliedBackdropMode)
        {
            try
            {
                SystemBackdrop = backdropMode switch
                {
                    1 => new MicaBackdrop { Kind = MicaKind.BaseAlt },
                    2 => new DesktopAcrylicBackdrop(),
                    _ => null,
                };
                _appliedBackdropMode = backdropMode;
            }
            catch
            {
                material = false;
                SystemBackdrop = null;
                _appliedBackdropMode = backdropMode;
            }
        }

        var rootBackground = material
            ? Color.FromArgb(0, 0, 0, 0)
            : (dark ? Color.FromArgb(255, 31, 31, 31) : Color.FromArgb(255, 243, 243, 243));
        RootGrid.Background = new SolidColorBrush(rootBackground);
        NavView.Background = new SolidColorBrush(Colors.Transparent);
        ContentFrame.Background = new SolidColorBrush(Colors.Transparent);

        var foreground = dark ? Colors.White : Color.FromArgb(255, 34, 31, 26);
        var inactive = dark ? Color.FromArgb(160, 255, 255, 255) : Color.FromArgb(150, 0, 0, 0);
        var hover = dark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(20, 0, 0, 0);
        var pressed = dark ? Color.FromArgb(40, 255, 255, 255) : Color.FromArgb(30, 0, 0, 0);

        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = inactive;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressed;
        titleBar.ButtonPressedForegroundColor = foreground;
        _applyingTheme = false;
    }

    public void CaptureWindowBounds() => SaveBounds();

    public void ApplyCornerStyle()
    {
        bool square = Store.Instance.Settings.SquareCorners;
        try
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            int preference = square ? 1 : 2; // DWMWCP_DONOTROUND / DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, 33, ref preference, sizeof(int));
        }
        catch { }

        if (square || CornerStyleService.HasOriginals)
            CornerStyleService.Apply(RootGrid, square);
    }

    private void SaveSettingsSoon()
    {
        if (Interlocked.Exchange(ref _settingsSaveQueued, 1) != 0) return;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            Interlocked.Exchange(ref _settingsSaveQueued, 0);
            try { Store.Instance.SaveSettings(); } catch { }
        });
    }

    public void SetPaneOpen(bool open)
    {
        NavView.IsPaneOpen = open;
    }

    public void SetTrayEnabled(bool enabled)
    {
        try
        {
            if (enabled && _tray is null)
            {
                _tray = new TrayService(WindowNative.GetWindowHandle(this));
                _tray.ShowRequested += (_, _) => DispatcherQueue.TryEnqueue(ShowFromTray);
                _tray.ExitRequested += (_, _) => DispatcherQueue.TryEnqueue(ExitFromTray);
            }
            else if (!enabled && _tray is not null)
            {
                _tray.Dispose();
                _tray = null;
                ShowFromTray();
            }
        }
        catch (Exception ex)
        {
            App.TryLog("创建小托盘失败：" + ex);
        }
    }

    private void ShowFromTray()
    {
        try
        {
            AppWindow.Show();
            Activate();
        }
        catch { }
    }

    public void CloseForExit() => BeginFastShutdown();

    private void SaveBeforeClose()
    {
        try { SaveBounds(); } catch { }
        try { Store.Instance.SaveSettings(); } catch { }
        try { Store.Instance.SaveAccounts(); } catch { }
    }

    private void BeginFastShutdown()
    {
        if (_shutdownStarted) return;
        _shutdownStarted = true;

        SaveBeforeClose();
        try { AppWindow.Hide(); } catch { }
        TrimWorkingSet();

        if (!EngineService.Instance.IsRunning)
        {
            _forceClose = true;
            try { Close(); } catch { }
            return;
        }

        _ = Task.Run(() =>
        {
            try { EngineService.Instance.StopSync(); } catch { }
            DispatcherQueue.TryEnqueue(() =>
            {
                _forceClose = true;
                _tray?.Dispose();
                _tray = null;
                try { Close(); } catch { }
            });
        });
    }

    private void ExitFromTray() => CloseForExit();

    public void ShowModal(UIElement content)
    {
        ModalContent.Content = content;
        ModalLayer.Visibility = Visibility.Visible;
    }

    public void HideModal()
    {
        ModalLayer.Visibility = Visibility.Collapsed;
        ModalContent.Content = null;
    }

    public void Minimize()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
            presenter.Minimize();
        TrimWorkingSet();
    }

    public void TrimWorkingSet()
    {
        try
        {
            SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
        }
        catch { }
        try
        {
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: false);
        }
        catch { }
    }

    [DllImport("kernel32.dll")]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);

    public void ShowInfo(string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        GlobalInfoBar.Severity = severity;
        GlobalInfoBar.Message = message;
        GlobalInfoBar.IsOpen = true;

        _infoTimer?.Stop();
        _infoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        _infoTimer.Tick += (_, _) =>
        {
            GlobalInfoBar.IsOpen = false;
            _infoTimer?.Stop();
        };
        _infoTimer.Start();
    }

    public void OpenAddAccountDialog() => NavigateTo("accounts");

    public void OpenAnnouncementsDialog()
    {
        NavigateTo("home");
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(350);
            ShowModal(new IdvLauncher.Dialogs.AnnouncementsDialog(Store.Instance.Announcements));
        });
    }

    private async Task WaitForXamlRootAsync()
    {
        for (int i = 0; i < 50 && RootGrid.XamlRoot is null; i++)
            await Task.Delay(50);
    }

    public async Task<bool> ShowStartupDisclaimerAsync()
    {
        if (_disclaimerOpen) return false;
        _disclaimerOpen = true;
        try
        {
            await WaitForXamlRootAsync();
            if (RootGrid.XamlRoot is null) return false;

            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "免责声明",
                Content = new TextBlock
                {
                    Text = "本软件为非官方第三方工具，仅用于个人账号管理与游戏启动辅助。\n\n"
                         + "使用本软件可能违反游戏用户协议，存在账号封禁、数据丢失或其他风险；"
                         + "因使用本软件产生的全部后果由你自行承担。请勿用于商业用途或非法目的。\n\n"
                         + "继续使用即表示你已阅读、理解并接受上述内容。",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 460,
                },
                PrimaryButtonText = "同意并继续",
                CloseButtonText = "不同意并退出",
                DefaultButton = ContentDialogButton.Primary,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch
        {
            return false;
        }
        finally
        {
            _disclaimerOpen = false;
        }
    }

    public async Task ShowDisclaimerDocumentAsync()
    {
        if (_disclaimerOpen) return;
        _disclaimerOpen = true;
        try
        {
            await WaitForXamlRootAsync();
            if (RootGrid.XamlRoot is null) return;
            var dialog = new ContentDialog
            {
                XamlRoot = RootGrid.XamlRoot,
                Title = "免责声明",
                Content = new TextBlock
                {
                    Text = "本软件为非官方第三方工具，仅用于个人账号管理与游戏启动辅助。\n\n"
                         + "使用本软件可能违反游戏用户协议，存在账号封禁、数据丢失或其他风险；"
                         + "因使用本软件产生的全部后果由你自行承担。请勿用于商业用途或非法目的。\n\n"
                         + "账号数据仅保存在本机。",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 460,
                },
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }
        finally
        {
            _disclaimerOpen = false;
        }
    }

    private async Task<bool> ShowFrameDisclaimerAsync()
    {
        await WaitForXamlRootAsync();
        if (RootGrid.XamlRoot is null) return false;

        var input = new TextBox
        {
            PlaceholderText = FrameAgreementText,
            Header = "请完整填写以下文字",
            MinWidth = 420,
        };
        var error = new TextBlock
        {
            Text = "",
            Foreground = new SolidColorBrush(Colors.Red),
            FontSize = 11.5,
        };
        var content = new StackPanel { Spacing = 10, MaxWidth = 480 };
        content.Children.Add(new TextBlock
        {
            Text = "解帧功能会修改游戏运行状态，可能导致账号被封禁、游戏异常或其他不可预期的后果。\n\n"
                 + "使用解帧即表示你自愿承担全部后果。请完整填写下面这句话后才能继续：",
            TextWrapping = TextWrapping.Wrap,
        });
        var fillButton = new Button
        {
            Content = "一键填写同意",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        fillButton.Click += (_, _) =>
        {
            input.Text = FrameAgreementText;
            error.Text = "";
            input.Focus(FocusState.Programmatic);
        };
        content.Children.Add(fillButton);
        content.Children.Add(input);
        content.Children.Add(error);

        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            Title = "解帧免责声明",
            Content = content,
            PrimaryButtonText = "同意并进入",
            CloseButtonText = "不同意，返回主页",
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!string.Equals(input.Text.Trim(), FrameAgreementText, StringComparison.Ordinal))
            {
                args.Cancel = true;
                error.Text = "文字不一致，必须完整填写：" + FrameAgreementText;
            }
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task PromptFrameDisclaimerAsync()
    {
        if (_frameDisclaimerPrompting) return;
        _frameDisclaimerPrompting = true;
        try
        {
            bool accepted = await ShowFrameDisclaimerAsync();
            if (accepted)
            {
                Store.Instance.Settings.FrameDisclaimerAccepted = true;
                Store.Instance.SaveSettings();
                NavigateTo("injection");
            }
            else
            {
                NavigateTo("home");
            }
        }
        catch
        {
            NavigateTo("home");
        }
        finally
        {
            _frameDisclaimerPrompting = false;
        }
    }

    public void NavigateTo(string tag)
    {
        if (tag == "injection" && !Store.Instance.Settings.FrameDisclaimerAccepted)
        {
            _ = PromptFrameDisclaimerAsync();
            return;
        }

        Type pageType = tag switch
        {
            "accounts" => typeof(AccountsPage),
            "settings" => typeof(SettingsPage),
            "engine" => typeof(EnginePage),
            "injection" => typeof(InjectionPage),
            "cloud" => typeof(CloudSyncPage),
            "keys" => typeof(KeyBindingsPage),
            _ => typeof(HomePage),
        };

        int index = tag switch
        {
            "accounts" => 1,
            "settings" => 2,
            "engine" => 3,
            "injection" => 4,
            "cloud" => 5,
            "keys" => 2,
            _ => 0,
        };
        TitlePageText.Text = tag switch
        {
            "accounts" => "账号管理",
            "settings" => "设置",
            "engine" => "登录引擎",
            "injection" => "解帧",
            "cloud" => "云存档",
            "keys" => "键位设置",
            _ => "首页",
        };
        Store.Instance.Settings.LastPage = index;
        SaveSettingsSoon();

        _suppressNav = true;
        NavView.SelectedItem = FindMenuItem(tag == "keys" ? "settings" : tag);
        _suppressNav = false;

        if (ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
        }
    }

    private void InitializeNavigation()
    {
        int index = Math.Clamp(Store.Instance.Settings.LastPage, 0, 5);
        if (index == 4 && !Store.Instance.Settings.FrameDisclaimerAccepted)
            index = 0;
        string tag = index switch
        {
            1 => "accounts",
            2 => "settings",
            3 => "engine",
            4 => "injection",
            5 => "cloud",
            _ => "home",
        };
        Type pageType = index switch
        {
            1 => typeof(AccountsPage),
            2 => typeof(SettingsPage),
            3 => typeof(EnginePage),
            4 => typeof(InjectionPage),
            5 => typeof(CloudSyncPage),
            _ => typeof(HomePage),
        };

        _suppressNav = true;
        NavView.SelectedItem = FindMenuItem(tag);
        _suppressNav = false;

        TitlePageText.Text = tag switch
        {
            "accounts" => "账号管理",
            "settings" => "设置",
            "engine" => "登录引擎",
            "injection" => "解帧",
            "cloud" => "云存档",
            _ => "首页",
        };
        ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
    }

    private NavigationViewItem? FindMenuItem(string tag)
    {
        if (string.Equals(tag, "settings", StringComparison.Ordinal))
            return NavView.SettingsItem as NavigationViewItem;

        return NavView.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, tag, StringComparison.Ordinal));
    }

    private void ConfigureSettingsNavigation()
    {
        if (NavView.SettingsItem is NavigationViewItem settingsItem)
        {
            settingsItem.Content = "设置";
            settingsItem.Icon = new FontIcon { Glyph = "\uE713" };
        }
    }

    private void OnPaneOpened(NavigationView sender, object args)
    {
        Store.Instance.Settings.SidebarCollapsed = false;
        SaveSettingsSoon();
    }

    private void OnPaneClosed(NavigationView sender, object args)
    {
        Store.Instance.Settings.SidebarCollapsed = true;
        SaveSettingsSoon();
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNav) return;
        if (args.IsSettingsSelected)
        {
            NavigateTo("settings");
            return;
        }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            NavigateTo(tag);
    }

    private void RestoreBounds()
    {
        IntPtr hwnd = WindowNative.GetWindowHandle(this);
        double scale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        bool remember = Store.Instance.Settings.RememberWindowBounds;
        int baseWidth = remember ? Store.Instance.Settings.WindowW : 1268;
        int baseHeight = remember ? Store.Instance.Settings.WindowH : 690;
        int width = Math.Min((int)(baseWidth * scale), area.Width);
        int height = Math.Min((int)(baseHeight * scale), area.Height);
        int x = area.X + (area.Width - width) / 2;
        int y = area.Y + (area.Height - height) / 2;

        if (remember && Store.Instance.Settings.WindowX >= 0 && Store.Instance.Settings.WindowY >= 0)
        {
            x = (int)(Store.Instance.Settings.WindowX * scale);
            y = (int)(Store.Instance.Settings.WindowY * scale);
        }

        x = Math.Clamp(x, area.X, Math.Max(area.X, area.X + area.Width - width));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void SaveBounds()
    {
        try
        {
            if (!Store.Instance.Settings.RememberWindowBounds) return;
            if (AppWindow.Presenter is OverlappedPresenter presenter &&
                presenter.State == OverlappedPresenterState.Maximized)
                return;

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            double scale = Math.Max(1.0, GetDpiForWindow(hwnd) / 96.0);
            var position = AppWindow.Position;
            var size = AppWindow.Size;
            Store.Instance.Settings.WindowX = (int)(position.X / scale);
            Store.Instance.Settings.WindowY = (int)(position.Y / scale);
            Store.Instance.Settings.WindowW = (int)(size.Width / scale);
            Store.Instance.Settings.WindowH = (int)(size.Height / scale);
        }
        catch
        {
            // Window bounds are nice-to-have; never block closing.
        }
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name) return element;
            var found = FindDescendant<T>(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
