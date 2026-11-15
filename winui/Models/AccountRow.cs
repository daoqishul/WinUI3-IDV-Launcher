using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IdvLauncher.Models;

public sealed class AccountRow
{
    public AccountRow(Account account)
    {
        Account = account;
        var channel = ChannelCatalog.Find(account.Channel);
        AvatarText = string.IsNullOrEmpty(channel.Letter) ? "账" : channel.Letter;
        AvatarForeground = new SolidColorBrush(channel.Color);
        AvatarBackground = new SolidColorBrush(Color.FromArgb(42, channel.Color.R, channel.Color.G, channel.Color.B));
        AvatarBorder = new SolidColorBrush(Color.FromArgb(95, channel.Color.R, channel.Color.G, channel.Color.B));

        Meta = channel.ShortName;
        if (account.IsDefault) Meta += " · 默认";
        if (account.Source == "idv-login") Meta += " · 已同步";
        SourceLabel = account.Source == "idv-login" ? "IDV-Login" : "本地账号";
        DefaultGlyph = account.IsDefault ? "\uE735" : "\uE734";
        DefaultBrush = new SolidColorBrush(account.IsDefault
            ? Color.FromArgb(255, 216, 179, 108)
            : Color.FromArgb(255, 145, 139, 128));
        LastLoginRelative = Relative(account.LastLogin);
        LastLoginDate = account.LastLogin <= 0 ? "从未登录" : DateTimeOffset.FromUnixTimeSeconds(account.LastLogin).LocalDateTime.ToString("yyyy/MM/dd");
    }

    public Account Account { get; }
    public string Uuid => Account.Uuid;
    public string Name => string.IsNullOrWhiteSpace(Account.Name) ? Account.Uuid : Account.Name;
    public string Meta { get; }
    public string SourceLabel { get; }
    public string AvatarText { get; }
    public SolidColorBrush AvatarForeground { get; }
    public SolidColorBrush AvatarBackground { get; }
    public SolidColorBrush AvatarBorder { get; }
    public string DefaultGlyph { get; }
    public SolidColorBrush DefaultBrush { get; }
    public string LastLoginRelative { get; }
    public string LastLoginDate { get; }

    private static string Relative(long unix)
    {
        if (unix <= 0) return "从未登录";
        var span = DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(unix);
        if (span.TotalSeconds < 60) return "刚刚";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} 分钟前";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} 小时前";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} 天前";
        return DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime.ToString("yyyy/MM/dd");
    }
}
