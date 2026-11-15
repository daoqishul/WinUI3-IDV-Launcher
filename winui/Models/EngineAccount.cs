using IdvLauncher.Models;
using Windows.UI;

namespace IdvLauncher.Models;

public sealed class EngineAccount
{
    public string Uuid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Channel { get; init; } = "";
    public string RecordSource { get; init; } = "";
    public long LastLogin { get; init; }

    public string Display => string.IsNullOrWhiteSpace(Name) ? Uuid : Name;

    public string Initial
    {
        get
        {
            string name = Display.Trim();
            return name.Length == 0 ? "?" : name[..1].ToUpperInvariant();
        }
    }

    public string ChannelLabel => ChannelCatalog.Find(Channel).Name;

    public string SourceLabel => RecordSource switch
    {
        "manual" => "手动 / 导入",
        "scan" => "扫码记录",
        _ => "引擎记录",
    };

    public string Meta => $"{ChannelLabel} · {SourceLabel}";

    public Color AccentColor => ChannelCatalog.Find(Channel).Color;

    public string LastLoginText
    {
        get
        {
            if (LastLogin <= 0) return "从未登录";
            var time = DateTimeOffset.FromUnixTimeSeconds(LastLogin).ToLocalTime();
            var span = DateTimeOffset.Now - time;
            if (span.TotalMinutes < 1) return "刚刚";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} 分钟前";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours} 小时前";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} 天前";
            return time.ToString("yyyy/MM/dd");
        }
    }
}
