using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace IdvLauncher.Models;

public sealed class ChannelInfo
{
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public string ShortName { get; init; } = "";
    public string Letter { get; init; } = "";
    public Color Color { get; init; }
}

public static class ChannelCatalog
{
    public static readonly IReadOnlyList<ChannelInfo> All = new List<ChannelInfo>
    {
        new() { Code = "huawei", Name = "华为账号", ShortName = "华为", Letter = "华", Color = Color.FromArgb(255, 207, 42, 42) },
        new() { Code = "xiaomi_app", Name = "小米账号", ShortName = "小米", Letter = "米", Color = Color.FromArgb(255, 255, 106, 0) },
        new() { Code = "oppo", Name = "OPPO 账号", ShortName = "OPPO", Letter = "O", Color = Color.FromArgb(255, 31, 158, 85) },
        new() { Code = "nearme_vivo", Name = "vivo 账号", ShortName = "vivo", Letter = "V", Color = Color.FromArgb(255, 74, 99, 231) },
        new() { Code = "bilibili_sdk", Name = "哔哩哔哩", ShortName = "哔哩哔哩", Letter = "B", Color = Color.FromArgb(255, 251, 114, 153) },
        new() { Code = "honor_sdk", Name = "荣耀账号", ShortName = "荣耀", Letter = "荣", Color = Color.FromArgb(255, 45, 127, 249) },
        new() { Code = "uc_platform", Name = "九游账号", ShortName = "九游", Letter = "九", Color = Color.FromArgb(255, 255, 122, 0) },
        new() { Code = "4399com", Name = "4399 账号", ShortName = "4399", Letter = "4", Color = Color.FromArgb(255, 255, 90, 31) },
        new() { Code = "360_assistant", Name = "360 账号", ShortName = "360", Letter = "3", Color = Color.FromArgb(255, 47, 174, 58) },
        new() { Code = "baidu", Name = "百度账号", ShortName = "百度", Letter = "百", Color = Color.FromArgb(255, 58, 69, 232) },
        new() { Code = "lenovo_open", Name = "联想账号", ShortName = "联想", Letter = "联", Color = Color.FromArgb(255, 226, 35, 26) },
        new() { Code = "guopan", Name = "果盘账号", ShortName = "果盘", Letter = "果", Color = Color.FromArgb(255, 255, 184, 0) },
        new() { Code = "myapp", Name = "应用宝（微信）", ShortName = "应用宝（微信）", Letter = "微", Color = Color.FromArgb(255, 7, 193, 96) },
        new() { Code = "myapp_qq", Name = "应用宝（QQ）", ShortName = "应用宝（QQ）", Letter = "Q", Color = Color.FromArgb(255, 18, 183, 245) },
        new() { Code = "netease", Name = "网易官方", ShortName = "网易官方", Letter = "网", Color = Color.FromArgb(255, 214, 179, 108) },
    };

    public static ChannelInfo Find(string? code)
    {
        return All.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
               ?? new ChannelInfo { Code = code ?? "", Name = string.IsNullOrEmpty(code) ? "本地账号" : code, ShortName = string.IsNullOrEmpty(code) ? "本地" : code, Letter = "账", Color = Color.FromArgb(255, 214, 179, 108) };
    }

    public static string ShortName(string? code) => Find(code).ShortName;

    public static SolidColorBrush Brush(string? code, byte alpha = 255)
    {
        var c = Find(code).Color;
        return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
    }
}
