using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace IdvLauncher.Dialogs;

public sealed partial class RenameDialog : UserControl
{
    private readonly TaskCompletionSource<string?> _completion = new();

    public RenameDialog(string currentName)
    {
        InitializeComponent();
        NameBox.Text = currentName;
        Loaded += (_, _) =>
        {
            NameBox.Focus(FocusState.Programmatic);
            NameBox.SelectAll();
        };
    }

    public Task<string?> WaitForResultAsync() => _completion.Task;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _completion.TrySetResult(null);
        App.MainWindow?.HideModal();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ErrorText.Text = "名称不能为空";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        _completion.TrySetResult(name);
        App.MainWindow?.HideModal();
    }
}
