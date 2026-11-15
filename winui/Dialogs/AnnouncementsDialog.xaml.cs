using IdvLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IdvLauncher.Dialogs;

public sealed partial class AnnouncementsDialog : UserControl
{
    public AnnouncementsDialog(IReadOnlyList<Announcement> announcements)
    {
        InitializeComponent();
        foreach (var item in announcements.Take(8))
        {
            var block = new StackPanel { Spacing = 4 };
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            meta.Children.Add(new TextBlock
            {
                Text = item.Date,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 150, 143, 132)),
            });
            if (!string.IsNullOrWhiteSpace(item.Tag))
            {
                meta.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, 216, 179, 108)),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(8, 1, 8, 2),
                    Child = new TextBlock
                    {
                        Text = item.Tag,
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromArgb(255, 216, 179, 108)),
                    },
                });
            }
            block.Children.Add(meta);
            block.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
            });
            block.Children.Add(new TextBlock
            {
                Text = item.Summary,
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap,
            });
            ListPanel.Children.Add(block);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        App.MainWindow?.HideModal();
    }
}
