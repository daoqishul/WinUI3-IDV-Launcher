using IdvLauncher.Dialogs;
using IdvLauncher.Models;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace IdvLauncher.Pages;

public sealed partial class AccountsPage : Page
{
    private readonly List<EngineAccountRow> _allRows = new();
    private EngineConfigSnapshot? _config;
    private string _defaultUuid = "";
    private bool _busy;
    private bool _updatingFilters;
    private bool _unloaded;

    public AccountsPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _unloaded = false;
            await LoadAsync();
        };
        Unloaded += (_, _) => _unloaded = true;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!IsLoaded) return;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_busy || _unloaded) return;

        LoadingRing.IsActive = true;
        try
        {
            var config = await EngineConfigStore.LoadAsync(Store.Instance.Settings.EngineGameId);
            if (_unloaded) return;
            _config = config;
            _allRows.Clear();

            if (config.Loaded)
            {
                _defaultUuid = config.SelectedUuid;
                foreach (var account in config.Accounts)
                    _allRows.Add(new EngineAccountRow(account, account.Uuid == _defaultUuid));
            }
            else
            {
                // Debug / non-admin fallback: keep the page useful, but channel
                // login needs the real engine records.
                var localDefault = Store.Instance.DefaultAccount();
                _defaultUuid = localDefault?.Uuid ?? "";
                foreach (var account in Store.Instance.Accounts)
                {
                    _allRows.Add(new EngineAccountRow(new EngineAccount
                    {
                        Uuid = account.Uuid,
                        Name = account.Name,
                        Channel = account.Channel,
                        RecordSource = account.Source,
                        LastLogin = account.LastLogin,
                    }, account.IsDefault, isLocal: true));
                }
            }

            BuildChannelOptions();
            ApplyFilter();

            if (!EnginePaths.IsElevated)
            {
                ShowInfo("需要以管理员身份运行才能读取原版 channels-v2.json 并切换渠道登录。当前显示本地记录。",
                    InfoBarSeverity.Warning);
            }
            else if (!EngineService.Instance.IsInstalled)
            {
                ShowInfo("没有找到原版 IDV-Login 引擎，请先在「登录引擎」页指定引擎目录。",
                    InfoBarSeverity.Error);
            }
            else if (!config.Loaded)
            {
                ShowInfo(config.Error.Length > 0 ? config.Error : "无法读取原版渠道账号。",
                    InfoBarSeverity.Warning);
            }
            else if (config.Accounts.Count == 0)
            {
                ShowInfo("还没有渠道账号。点「添加渠道账号」选择渠道服完成登录。",
                    InfoBarSeverity.Informational);
            }
            else
            {
                AccountsInfo.IsOpen = false;
            }

            FooterText.Text = config.Loaded
                ? "账号来自原版 channels-v2.json · 添加/改名/删除均在本窗口完成"
                : "当前显示本地记录；需要管理员 + 原版引擎才能切换渠道登录";
        }
        catch (Exception ex)
        {
            ShowInfo($"读取渠道账号失败：{ex.Message}", InfoBarSeverity.Error);
            FooterText.Text = "读取渠道账号失败";
        }
        finally
        {
            if (!_unloaded) LoadingRing.IsActive = false;
        }
    }

    private void BuildChannelOptions()
    {
        string selectedCode = (ChannelCombo.SelectedItem as ChannelFilterOption)?.Code ?? "";
        var options = new List<ChannelFilterOption>
        {
            new("", $"全部渠道 ({_allRows.Count})", _allRows.Count),
        };

        foreach (var group in _allRows
                     .GroupBy(r => r.Account.Channel ?? "")
                     .OrderByDescending(g => g.Count())
                     .ThenBy(g => ChannelCatalog.Find(g.Key).Name))
        {
            string name = string.IsNullOrWhiteSpace(group.Key)
                ? "未知渠道"
                : ChannelCatalog.Find(group.Key).Name;
            options.Add(new ChannelFilterOption(group.Key, $"{name} ({group.Count()})", group.Count()));
        }

        _updatingFilters = true;
        ChannelCombo.ItemsSource = options;
        ChannelCombo.SelectedItem = options.FirstOrDefault(o => o.Code == selectedCode) ?? options[0];
        _updatingFilters = false;
    }

    private void ApplyFilter()
    {
        string query = SearchBox.Text?.Trim() ?? "";
        string channel = (ChannelCombo.SelectedItem as ChannelFilterOption)?.Code ?? "";

        var filtered = _allRows.Where(row =>
        {
            if (!string.IsNullOrEmpty(channel)
                && !string.Equals(row.Account.Channel, channel, StringComparison.OrdinalIgnoreCase))
                return false;
            if (query.Length == 0) return true;
            return row.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || row.Uuid.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || row.ChannelName.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || row.SourceLabel.Contains(query, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        AccountsList.ItemsSource = filtered;
        EmptyState.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (_allRows.Count == 0)
        {
            EmptyTitle.Text = "还没有渠道账号";
            EmptyDescription.Text = "点「添加渠道账号」选择渠道服，扫码或完成渠道登录。";
        }
        else
        {
            EmptyTitle.Text = "没有匹配的账号";
            EmptyDescription.Text = "换一个关键词，或把渠道筛选切回「全部渠道」。";
        }

        string defaultName = _allRows.FirstOrDefault(r => r.Uuid == _defaultUuid)?.DisplayName ?? "未设置";
        CountText.Text = filtered.Count == _allRows.Count
            ? $"共 {_allRows.Count} 个 · 默认 {defaultName}"
            : $"显示 {filtered.Count} / {_allRows.Count} 个";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void OnChannelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingFilters) return;
        ApplyFilter();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnAddAccount(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var flyout = new MenuFlyout();
        foreach (var channel in ChannelCatalog.All)
        {
            var item = new MenuFlyoutItem
            {
                Text = channel.Name,
                Tag = channel.Code,
            };
            item.Click += OnAddChannelSelected;
            flyout.Items.Add(item);
        }
        flyout.ShowAt(ManageButton);
    }

    private async void OnAddChannelSelected(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not MenuFlyoutItem { Tag: string channel }) return;
        _busy = true;
        ManageButton.IsEnabled = false;
        try
        {
            var api = EngineApiClient.Instance;
            if (!await api.HealthAsync())
            {
                ShowInfo("正在启动登录引擎…", InfoBarSeverity.Informational);
                await EngineService.Instance.StartAsync();
            }
            if (!await api.HealthAsync())
                throw new EngineException("登录引擎未就绪，无法打开渠道登录网页。");

            var start = await api.StartImportAsync(channel, Store.Instance.Settings.EngineGameId);
            if (!string.IsNullOrWhiteSpace(start.Error))
                throw new EngineException(start.Error);

            if (start.Pending && !string.IsNullOrWhiteSpace(start.TaskId))
            {
                _ = PollImportAsync(start.TaskId);
                ShowInfo("已打开渠道登录网页。", InfoBarSeverity.Informational);
            }
            else if (start.Success)
            {
                await LoadAsync();
                ShowInfo("渠道账号已添加。", InfoBarSeverity.Success);
            }
            else
            {
                throw new EngineException("渠道登录未能启动。");
            }
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
            ManageButton.IsEnabled = true;
        }
    }

    private async Task PollImportAsync(string taskId)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(2000);
            if (_unloaded) return;
            try
            {
                var status = await EngineApiClient.Instance.GetImportStatusAsync(taskId);
                if (status.Status == "done")
                {
                    await LoadAsync();
                    if (status.Success)
                        ShowInfo("渠道账号已添加。", InfoBarSeverity.Success);
                    else if (!status.Cancelled)
                        ShowInfo(string.IsNullOrWhiteSpace(status.Error) ? "渠道登录未完成。" : status.Error,
                            InfoBarSeverity.Warning);
                    return;
                }
            }
            catch
            {
                // The task may disappear when the engine is restarted; stop polling.
                return;
            }
        }
    }

    private async void OnRenameAccount(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not MenuFlyoutItem { Tag: string uuid }) return;
        var row = _allRows.FirstOrDefault(r => r.Uuid == uuid);
        if (row is null) return;
        if (row.IsLocal)
        {
            ShowInfo("本地记录不能通过引擎改名。", InfoBarSeverity.Warning);
            return;
        }

        var dialog = new RenameDialog(row.DisplayName);
        App.MainWindow?.ShowModal(dialog);
        string? newName = await dialog.WaitForResultAsync();
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            _busy = true;
            if (EngineService.Instance.IsRunning)
            {
                var result = await EngineApiClient.Instance.RenameAsync(uuid, newName);
                if (!result.Success)
                    throw new EngineException(string.IsNullOrWhiteSpace(result.Error) ? "改名失败。" : result.Error);
            }
            else
            {
                await EngineConfigStore.RenameChannelRecordAsync(uuid, newName);
            }

            await LoadAsync();
            ShowInfo("账号已改名。", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnDeleteAccount(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not MenuFlyoutItem { Tag: string uuid }) return;
        var row = _allRows.FirstOrDefault(r => r.Uuid == uuid);
        if (row is null) return;
        if (row.IsLocal)
        {
            ShowInfo("本地记录不能通过引擎删除。", InfoBarSeverity.Warning);
            return;
        }

        var dialog = new ConfirmDialog("删除账号",
            $"确定要删除「{row.DisplayName}」吗？此操作会同时删除原版渠道记录，无法撤销。",
            "删除", danger: true);
        App.MainWindow?.ShowModal(dialog);
        if (!await dialog.WaitForResultAsync()) return;

        try
        {
            _busy = true;
            if (EngineService.Instance.IsRunning)
            {
                var result = await EngineApiClient.Instance.DeleteAsync(uuid);
                if (!result.Success)
                    throw new EngineException(string.IsNullOrWhiteSpace(result.Error) ? "删除失败。" : result.Error);
            }
            else
            {
                await EngineConfigStore.DeleteChannelRecordAsync(uuid);
            }

            if (_defaultUuid == uuid)
            {
                try { await SaveSelectedAsync(""); } catch { }
            }

            await LoadAsync();
            ShowInfo("账号已删除。", InfoBarSeverity.Informational);
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OnSetDefault(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string uuid }) return;
        var row = _allRows.FirstOrDefault(r => r.Uuid == uuid);
        if (row is null) return;
        if (row.IsLocal)
        {
            ShowInfo("本地记录没有原版渠道身份，无法设为引擎默认账号。", InfoBarSeverity.Warning);
            return;
        }

        try
        {
            bool clearing = string.Equals(_defaultUuid, uuid, StringComparison.OrdinalIgnoreCase);
            await SaveSelectedAsync(clearing ? "" : uuid);

            _defaultUuid = clearing ? "" : uuid;
            foreach (var item in _allRows) item.IsDefault = item.Uuid == _defaultUuid;
            ApplyFilter();

            if (clearing)
            {
                ShowInfo("已取消收藏；默认账号为空，启动游戏不会自动登录该账号。",
                    InfoBarSeverity.Informational);
            }
            else
            {
                ShowInfo("已设为收藏；启动游戏会直接登录该账号，无需内置登录窗口。",
                    InfoBarSeverity.Success);
            }
        }
        catch (Exception ex)
        {
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async void OnLoginAccount(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string uuid }) return;
        var row = _allRows.FirstOrDefault(r => r.Uuid == uuid);
        if (row is null) return;
        if (row.IsLocal)
        {
            ShowInfo("本地记录不能登录原版渠道服，请先点「添加渠道账号」完成真实登录。",
                InfoBarSeverity.Warning);
            return;
        }

        bool gameRunning = GameLauncher.IsRunning();
        _busy = true;
        row.IsBusy = true;
        ManageButton.IsEnabled = false;
        RefreshButton.IsEnabled = false;
        AccountsList.IsEnabled = false;
        ShowInfo(gameRunning
                ? $"正在让游戏登录 {row.DisplayName}（{row.ChannelName}）…"
                : $"正在切换到 {row.DisplayName}（{row.ChannelName}）并启动登录…",
            InfoBarSeverity.Informational);

        try
        {
            await EngineService.Instance.LoginAccountAsync(uuid);
            _defaultUuid = uuid;
            foreach (var item in _allRows) item.IsDefault = item.Uuid == uuid;
            ShowInfo(gameRunning
                    ? $"已在当前游戏窗口登录 {row.DisplayName}。"
                    : $"已切换至 {row.DisplayName}，游戏启动中…",
                InfoBarSeverity.Success);
            ApplyFilter();

            if (Store.Instance.Settings.AutoMinimize)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
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
            ShowInfo(ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            row.IsBusy = false;
            _busy = false;
            AccountsList.IsEnabled = true;
            ManageButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
        }
    }

    private async Task SaveSelectedAsync(string uuid)
    {
        var config = await EngineConfigStore.LoadAsync(Store.Instance.Settings.EngineGameId);
        if (!config.Loaded)
            throw new EngineConfigException(config.Error.Length > 0
                ? config.Error
                : "无法读取原版登录配置，无法设置默认账号。");

        string gamePath = !string.IsNullOrWhiteSpace(config.GamePath)
            ? config.GamePath
            : Store.Instance.Settings.GamePath;

        await EngineConfigStore.SaveAsync(new EngineConfigDraft(
            config.GameId,
            gamePath,
            config.ProxyMode,
            config.LoginDelay,
            config.AutoClose,
            uuid));
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        if (_unloaded) return;
        AccountsInfo.Severity = severity;
        AccountsInfo.Message = message;
        AccountsInfo.IsOpen = true;
    }
}
