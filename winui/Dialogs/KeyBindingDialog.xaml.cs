using IdvLauncher.Models;
using IdvLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdvLauncher.Dialogs;

public sealed partial class KeyBindingDialog : ContentDialog
{
    private readonly KeyBindingService _service = new();
    private List<KeyBindingRow> _rows = new();
    private bool _loaded;
    private bool _loadingSelection;

    public KeyBindingDialog()
    {
        InitializeComponent();
    }

    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        if (_loaded) return;
        _loaded = true;
        ApplyButton.IsEnabled = false;
        KeyCombo.IsEnabled = false;

        if (KeyBindingService.FindPath().Length == 0)
        {
            StatusText.Text = "未找到键位配置；请先在设置中选择游戏路径并进入一次游戏。";
            return;
        }
        if (!await _service.LoadCatalogAsync())
        {
            StatusText.Text = "键位名称数据加载失败。";
            return;
        }

        try
        {
            _rows = await _service.LoadBindingsAsync();
            KeyCombo.ItemsSource = _service.Codes;
            CategoryCombo.ItemsSource = _service.Categories;
            CategoryCombo.SelectedIndex = 0;
            StatusText.Text = $"共 {_rows.Count} 个动作；修改后直接写入游戏键位配置。";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSelection) return;
        if (CategoryCombo.SelectedItem is not KeyActionCategory category) return;
        var filtered = _rows
            .Where(r => string.Equals(r.Category, category.Name, StringComparison.Ordinal))
            .ToList();
        ActionsList.ItemsSource = filtered;
        ActionsList.SelectedItem = null;
        ApplyButton.IsEnabled = false;
        KeyCombo.IsEnabled = false;
        SelectedActionText.Text = "请选择一个动作";
    }

    private void OnActionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSelection) return;
        if (ActionsList.SelectedItem is not KeyBindingRow row)
        {
            ApplyButton.IsEnabled = false;
            KeyCombo.IsEnabled = false;
            return;
        }

        _loadingSelection = true;
        try
        {
            SelectedActionText.Text = $"当前动作：{row.Name}（{row.KeyDisplay}）";
            KeyCombo.SelectedItem = row.KeyCode is int code
                ? _service.Codes.FirstOrDefault(c => c.Code == code)
                : null;
            KeyCombo.IsEnabled = true;
            ApplyButton.IsEnabled = true;
        }
        finally
        {
            _loadingSelection = false;
        }
    }

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is not KeyBindingRow row ||
            KeyCombo.SelectedItem is not KeyCodeOption key)
            return;

        if (GameLauncher.IsRunning())
        {
            StatusText.Text = "请先关闭游戏，再修改键位。";
            return;
        }

        ApplyButton.IsEnabled = false;
        KeyCombo.IsEnabled = false;
        try
        {
            await _service.SetBindingAsync(row.Op, new[] { key.Code });
            row.KeyCode = key.Code;
            row.KeyDisplay = key.Name;
            SelectedActionText.Text = $"当前动作：{row.Name}（{key.Name}）";
            StatusText.Text = $"已保存：{row.Name} → {key.Name}。";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"保存失败：{ex.Message}";
        }
        finally
        {
            ApplyButton.IsEnabled = true;
            KeyCombo.IsEnabled = true;
        }
    }
}
