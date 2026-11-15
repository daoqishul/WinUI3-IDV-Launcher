using IdvLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdvLauncher.Dialogs;

public sealed partial class AccountDialog : UserControl
{
    private readonly Account? _editing;
    private readonly TaskCompletionSource<Account?> _completion = new();

    public AccountDialog(Account? account)
    {
        InitializeComponent();
        _editing = account;
        ChannelBox.ItemsSource = ChannelCatalog.All;

        if (account is not null)
        {
            TitleText.Text = "编辑账号";
            NameBox.Text = account.Name;
            LoginBox.Text = account.Login;
            DefaultSwitch.IsOn = account.IsDefault;
            int index = ChannelCatalog.All.ToList().FindIndex(c => c.Code == account.Channel);
            ChannelBox.SelectedIndex = index >= 0 ? index : 0;
        }
        else
        {
            TitleText.Text = "添加账号";
            ChannelBox.SelectedIndex = 0;
        }

        Loaded += (_, _) => NameBox.Focus(FocusState.Programmatic);
    }

    public Task<Account?> WaitForResultAsync() => _completion.Task;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        App.MainWindow?.HideModal();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorText.Text = "请输入账号名称";
            ErrorText.Visibility = Visibility.Visible;
            NameBox.Focus(FocusState.Programmatic);
            return;
        }

        string channel = ChannelBox.SelectedValue as string
                         ?? (ChannelBox.SelectedItem as ChannelInfo)?.Code
                         ?? ChannelCatalog.All[0].Code;

        var result = new Account
        {
            Uuid = _editing?.Uuid ?? $"local-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{Random.Shared.Next(1000, 9999)}",
            Name = name,
            Channel = channel,
            Login = LoginBox.Text.Trim(),
            Note = _editing?.Note ?? "",
            Source = _editing?.Source ?? "local",
            LastLogin = _editing?.LastLogin ?? 0,
            IsDefault = DefaultSwitch.IsOn,
        };

        _completion.TrySetResult(result);
        App.MainWindow?.HideModal();
    }
}
