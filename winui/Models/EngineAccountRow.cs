using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IdvLauncher.Models;

public sealed record ChannelFilterOption(string Code, string Label, int Count);

public sealed class EngineAccountRow : INotifyPropertyChanged
{
    private bool _isDefault;
    private bool _isBusy;

    public EngineAccountRow(EngineAccount account, bool isDefault, bool isLocal = false)
    {
        Account = account;
        _isDefault = isDefault;
        IsLocal = isLocal;

        var channel = ChannelCatalog.Find(account.Channel);
        ChannelColor = channel.Color;
        AvatarBackground = new SolidColorBrush(Color.FromArgb(34, channel.Color.R, channel.Color.G, channel.Color.B));
        AvatarBorder = new SolidColorBrush(Color.FromArgb(80, channel.Color.R, channel.Color.G, channel.Color.B));
        BadgeBackground = new SolidColorBrush(Color.FromArgb(26, channel.Color.R, channel.Color.G, channel.Color.B));
        BadgeForeground = new SolidColorBrush(channel.Color);
    }

    public EngineAccount Account { get; }
    public string Uuid => Account.Uuid;
    public string DisplayName => Account.Display;
    public string Initial => string.IsNullOrWhiteSpace(Account.Initial) ? "账" : Account.Initial;
    public string ChannelName => ChannelCatalog.Find(Account.Channel).Name;
    public string SourceLabel => IsLocal ? "本地记录" : Account.SourceLabel;
    public string Meta => SourceLabel;
    public string LastLoginText => Account.LastLoginText;
    public bool IsLocal { get; }

    public Color ChannelColor { get; }
    public SolidColorBrush AvatarBackground { get; }
    public SolidColorBrush AvatarBorder { get; }
    public SolidColorBrush BadgeBackground { get; }
    public SolidColorBrush BadgeForeground { get; }

    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault == value) return;
            _isDefault = value;
            OnPropertyChanged(nameof(IsDefault));
            OnPropertyChanged(nameof(DefaultGlyph));
            OnPropertyChanged(nameof(DefaultTooltip));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(LoginText));
            OnPropertyChanged(nameof(CanLogin));
        }
    }

    public string DefaultGlyph => IsDefault ? "\uE735" : "\uE734";
    public string DefaultTooltip => IsDefault ? "取消默认（不自动登录）" : "设为默认（自动登录）";
    public string LoginText => IsBusy ? "登录中…" : "登录账号";
    public bool CanLogin => !IsBusy && !IsLocal;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
