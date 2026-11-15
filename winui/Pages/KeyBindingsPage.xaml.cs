using IdvLauncher.Models;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace IdvLauncher.Pages;

public sealed partial class KeyBindingsPage : Page
{
    private sealed record SearchSuggestion(string Display, string Group, string? Role, int? Op);

    private readonly KeyBindingService _service = new();
    private readonly List<int> _pendingCodes = new();
    private List<KeyBindingRow> _rows = new();
    private KeyBindingRow? _captureRow;
    private string _captureOriginal = "";
    private string _group = "通用";
    private bool _loaded;
    private bool _loadingRoles;

    public KeyBindingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        StatusText.Text = "读取键位中…";
        string path = KeyBindingService.FindPath();
        if (path.Length == 0)
        {
            StatusText.Text = "正在自动查询按键配置文件…";
            path = await KeyBindingService.DiscoverAsync();
        }

        if (path.Length == 0)
        {
            KeyConfigPathText.Text = "按键配置文件：未找到";
            StatusText.Text = "未找到 pc_key_setting_data.json，可手动选择或填写路径。";
            return;
        }

        KeyConfigPathText.Text = "按键配置文件：" + path;
        ToolTipService.SetToolTip(KeyConfigPathText, path);
        ManualPathBox.Text = path;

        if (!await _service.LoadCatalogAsync())
        {
            StatusText.Text = "键位名称数据加载失败。";
            return;
        }

        try
        {
            _rows = await _service.LoadBindingsAsync();
            _loaded = true;
            ShowGroup("通用");
            StatusText.Text = $"共 {_rows.Count} 个动作；点击右侧按键修改。";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void ShowGroup(string group)
    {
        _group = group;
        RefreshRoles(group);
        ApplyFilter();
        TabGeneral.IsChecked = group == "通用";
        TabSurvivor.IsChecked = group == "求生者";
        TabHunter.IsChecked = group == "监管者";
        TabNotebook.IsChecked = group == "加页手记";
        TabOther.IsChecked = group == "其他模式";
    }

    private void RefreshRoles(string group)
    {
        _loadingRoles = true;
        try
        {
            bool hasRoles = group is "求生者" or "监管者";
            RolePanel.Visibility = hasRoles ? Visibility.Visible : Visibility.Collapsed;
            if (!hasRoles)
            {
                RoleCombo.ItemsSource = null;
                return;
            }

            var roles = _service.Categories
                .Select(c => c.Name)
                .Where(name => name.StartsWith(group + "·", StringComparison.Ordinal))
                .Where(name => !name.EndsWith("·通用", StringComparison.Ordinal))
                .Select(name => name[(group.Length + 1)..])
                .ToList();
            roles.Insert(0, "通用");
            RoleCombo.ItemsSource = roles;
            RoleCombo.SelectedIndex = 0;
        }
        finally
        {
            _loadingRoles = false;
        }
    }

    private void ApplyFilter()
    {
        if (_group is "求生者" or "监管者")
        {
            string role = RoleCombo.SelectedItem as string ?? "通用";
            string generalCategory = _group + "·通用";
            string roleCategory = _group + "·" + role;

            KeyRepeater.ItemsSource = _rows
                .Where(r => string.Equals(r.Category, generalCategory, StringComparison.Ordinal))
                .ToList();
            RoleKeyRepeater.ItemsSource = role == "通用"
                ? new List<KeyBindingRow>()
                : _rows.Where(r => string.Equals(r.Category, roleCategory, StringComparison.Ordinal)).ToList();
        }
        else
        {
            KeyRepeater.ItemsSource = _rows
                .Where(r => string.Equals(r.Group, _group, StringComparison.Ordinal))
                .ToList();
            RoleKeyRepeater.ItemsSource = null;
        }
    }

    private void OnKeyScrollWheel(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(KeyScroll);
        int delta = point.Properties.MouseWheelDelta;
        if (delta == 0) return;
        KeyScroll.ChangeView(null, KeyScroll.VerticalOffset - delta, null, true);
        e.Handled = true;
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string group }) return;
        CancelCapture();
        ShowGroup(group);
    }

    private void OnRoleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingRoles || !_loaded) return;
        CancelCapture();
        ApplyFilter();
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            return;
        string query = sender.Text.Trim();
        if (query.Length == 0)
        {
            sender.ItemsSource = null;
            return;
        }

        var results = new List<SearchSuggestion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in _service.Categories)
        {
            string? role = null;
            if (category.Name.StartsWith("求生者·", StringComparison.Ordinal))
                role = category.Name["求生者·".Length..];
            else if (category.Name.StartsWith("监管者·", StringComparison.Ordinal))
                role = category.Name["监管者·".Length..];

            if (role is not null && role != "通用" && role.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                string group = category.Name.StartsWith("求生者", StringComparison.Ordinal) ? "求生者" : "监管者";
                string key = "role:" + group + ":" + role;
                if (seen.Add(key))
                    results.Add(new SearchSuggestion($"角色：{role}", group, role, null));
            }
        }

        foreach (var row in _rows)
        {
            if (!row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !row.KeyDisplay.Contains(query, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add("op:" + row.Op)) continue;
            string? role = null;
            if (row.Category.StartsWith("求生者·", StringComparison.Ordinal))
                role = row.Category["求生者·".Length..];
            else if (row.Category.StartsWith("监管者·", StringComparison.Ordinal))
                role = row.Category["监管者·".Length..];
            if (role == "通用") role = null;
            results.Add(new SearchSuggestion($"动作：{row.Name}（{row.KeyDisplay}）", row.Group, role, row.Op));
        }

        sender.ItemsSource = results
            .OrderBy(r => r.Op.HasValue ? 1 : 0)
            .ThenBy(r => r.Display, StringComparer.CurrentCulture)
            .Take(25)
            .ToList();
    }

    private void OnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchSuggestion suggestion)
            JumpTo(suggestion);
    }

    private void OnSearchSubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SearchSuggestion chosen)
        {
            JumpTo(chosen);
            return;
        }
        if (sender.ItemsSource is IEnumerable<SearchSuggestion> list)
        {
            var first = list.FirstOrDefault();
            if (first is not null) JumpTo(first);
        }
    }

    private void JumpTo(SearchSuggestion suggestion)
    {
        CancelCapture();
        ShowGroup(suggestion.Group);
        if (suggestion.Role is not null)
        {
            RoleCombo.SelectedItem = suggestion.Role;
            ApplyFilter();
        }

        SearchBox.Text = "";
        SearchBox.ItemsSource = null;

        if (suggestion.Op is not int op)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RolePanel.StartBringIntoView();
                _ = FlashBorderAsync(RolePanel);
            });
            return;
        }

        KeyBindingRow? target = _rows.FirstOrDefault(r => r.Op == op);
        if (target is null) return;

        bool roleSpecific = _group is "求生者" or "监管者" &&
                            !string.Equals(target.Category, _group + "·通用", StringComparison.Ordinal);
        if (roleSpecific)
        {
            string role = target.Category[(_group.Length + 1)..];
            RoleCombo.SelectedItem = role;
            ApplyFilter();
            var roleRows = RoleKeyRepeater.ItemsSource as List<KeyBindingRow>;
            int roleIndex = roleRows?.FindIndex(r => r.Op == op) ?? -1;
            DispatcherQueue.TryEnqueue(() =>
            {
                RolePanel.StartBringIntoView();
                if (roleIndex >= 0 && RoleKeyRepeater.GetOrCreateElement(roleIndex) is UIElement element)
                    element.StartBringIntoView();
                _ = FlashBorderAsync(RolePanel);
            });
            return;
        }

        var rows = KeyRepeater.ItemsSource as List<KeyBindingRow>;
        int index = rows?.FindIndex(r => r.Op == op) ?? -1;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (index >= 0 && KeyRepeater.GetOrCreateElement(index) is UIElement element)
                element.StartBringIntoView();
            _ = FlashBorderAsync(KeyListBorder);
        });
    }

    private async Task FlashBorderAsync(Border border)
    {
        Brush accent = Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? res) &&
                       res is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(255, 60, 150, 230));
        Brush clear = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        for (int i = 0; i < 6; i++)
        {
            border.BorderBrush = i % 2 == 0 ? accent : clear;
            await Task.Delay(140);
        }
        border.BorderBrush = clear;
    }

    private async Task FlashComboAsync(ComboBox combo)
    {
        Brush original = combo.BorderBrush;
        Brush accent = Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out object? res) &&
                       res is Brush brush
            ? brush
            : new SolidColorBrush(Color.FromArgb(255, 60, 150, 230));
        for (int i = 0; i < 6; i++)
        {
            combo.BorderBrush = i % 2 == 0 ? accent : new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            await Task.Delay(140);
        }
        combo.BorderBrush = original;
    }

    private void OnKeyButtonClick(object sender, RoutedEventArgs e)
    {
        if (!_loaded || sender is not Button { Tag: int op }) return;
        var row = _rows.FirstOrDefault(r => r.Op == op);
        if (row is null) return;

        CancelCapture();
        _captureRow = row;
        _captureOriginal = row.KeyDisplay;
        _pendingCodes.Clear();
        ConfirmKeyButton.Visibility = Visibility.Collapsed;
        CaptureBox.Text = "";
        CaptureBox.Visibility = Visibility.Visible;
        row.KeyDisplay = "按下新按键…";
        StatusText.Text = $"正在修改「{row.Name}」：按下按键，可组合多个键，Enter 确认，Esc 取消。";
        CaptureBox.Focus(FocusState.Programmatic);
    }

    private async void OnCaptureBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_captureRow is null) return;
        e.Handled = true;

        if (e.Key == VirtualKey.Escape)
        {
            CancelCapture();
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            await CommitPendingAsync();
            return;
        }

        if (e.Key == VirtualKey.Back)
        {
            if (_pendingCodes.Count > 0) _pendingCodes.RemoveAt(_pendingCodes.Count - 1);
            UpdatePendingDisplay();
            return;
        }

        int keyCode = (int)e.Key;
        if (keyCode <= 0) return;
        _pendingCodes.Add(keyCode);
        UpdatePendingDisplay();
    }

    private void UpdatePendingDisplay()
    {
        if (_captureRow is null) return;
        if (_pendingCodes.Count == 0)
        {
            CaptureBox.Text = "";
            _captureRow.KeyDisplay = "按下新按键…";
            StatusText.Text = "按下按键，可组合多个键，Enter 保存，Esc 取消。";
            ConfirmKeyButton.Visibility = Visibility.Collapsed;
            return;
        }

        string combo = string.Join(" + ", _pendingCodes.Select(c => _service.KeyName(c)));
        CaptureBox.Text = combo;
        _captureRow.KeyDisplay = $"{combo}（Enter 确认）";
        StatusText.Text = $"已选择「{combo}」，Enter 保存，Backspace 删除最后一个，Esc 取消。";
        ConfirmKeyButton.Visibility = Visibility.Visible;
    }

    private async void OnConfirmKey(object sender, RoutedEventArgs e)
    {
        await CommitPendingAsync();
    }

    private async Task CommitPendingAsync()
    {
        if (_captureRow is null || _pendingCodes.Count == 0) return;
        var row = _captureRow;
        var codes = _pendingCodes.ToList();
        _captureRow = null;
        _pendingCodes.Clear();
        ConfirmKeyButton.Visibility = Visibility.Collapsed;
        CaptureBox.Visibility = Visibility.Collapsed;
        CaptureBox.Text = "";
        try
        {
            await _service.SetBindingAsync(row.Op, codes);
            row.KeyCode = codes[0];
            row.KeyDisplay = string.Join(" + ", codes.Select(c => _service.KeyName(c)));
            StatusText.Text = $"已保存：{row.Name} → {row.KeyDisplay}。";
        }
        catch (Exception ex)
        {
            row.KeyDisplay = _captureOriginal;
            StatusText.Text = $"保存失败：{ex.Message}";
        }
    }

    private void CancelCapture()
    {
        if (_captureRow is not null)
            _captureRow.KeyDisplay = _captureOriginal;
        _captureRow = null;
        _pendingCodes.Clear();
        ConfirmKeyButton.Visibility = Visibility.Collapsed;
        CaptureBox.Visibility = Visibility.Collapsed;
        CaptureBox.Text = "";
    }

    private async void OnBrowsePath(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null) return;
        try
        {
            IntPtr owner = WindowNative.GetWindowHandle(App.MainWindow);
            string? folder = NativeFileDialog.PickFolder(owner);
            if (string.IsNullOrWhiteSpace(folder)) return;

            ManualPathBox.Text = folder;
            StatusText.Text = $"正在从 {folder} 查找按键文件…";
            string path = await KeyBindingService.DiscoverInFolderAsync(folder);
            if (path.Length == 0)
            {
                StatusText.Text = "所选目录及子目录中没有找到 pc_key_setting_data.json。";
                return;
            }
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"选择目录失败：{ex.Message}";
        }
    }

    private async void OnUseManualPath(object sender, RoutedEventArgs e)
    {
        string path = ManualPathBox.Text.Trim().Trim('"');
        if (path.Length == 0)
        {
            StatusText.Text = "请填写按键配置文件路径。";
            return;
        }
        if (Directory.Exists(path))
        {
            StatusText.Text = "正在从目录中查找按键文件…";
            string found = await KeyBindingService.DiscoverInFolderAsync(path);
            if (found.Length == 0)
            {
                StatusText.Text = "该目录及子目录中没有找到 pc_key_setting_data.json。";
                return;
            }
            await LoadAsync();
            return;
        }
        if (!File.Exists(path))
        {
            StatusText.Text = "该路径不存在，请检查后重试。";
            return;
        }
        KeyBindingService.SetManualPath(path);
        await LoadAsync();
    }

    private async void OnDiscoverPath(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "正在自动查询按键配置文件…";
        string path = await KeyBindingService.DiscoverAsync();
        if (path.Length == 0)
        {
            KeyConfigPathText.Text = "按键配置文件：未找到";
            StatusText.Text = "没有找到；请手动选择或填写路径。";
            return;
        }
        await LoadAsync();
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        CancelCapture();
        App.MainWindow?.NavigateTo("settings");
    }
}
