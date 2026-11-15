using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdvLauncher.Dialogs;

public sealed partial class ConfirmDialog : UserControl
{
    private readonly TaskCompletionSource<bool> _completion = new();

    public ConfirmDialog(string title, string message, string confirmText, bool danger = false)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        if (danger && Application.Current.Resources.TryGetValue("DangerButtonStyle", out object? style) && style is Style buttonStyle)
            ConfirmButton.Style = buttonStyle;
    }

    public Task<bool> WaitForResultAsync() => _completion.Task;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(false);
        App.MainWindow?.HideModal();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(true);
        App.MainWindow?.HideModal();
    }
}
