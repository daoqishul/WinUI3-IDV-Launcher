using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using IdvLauncher.Models;

namespace IdvLauncher.Services;

public sealed record OfficialNewsResult(IReadOnlyList<Announcement> Items, bool FromNetwork);

public static class OfficialNewsService
{
    public const string SourceUrl = "https://id5.163.com/news/update/";
    public const string NewsUrl = "https://id5.163.com/news/official/";

    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IDVLauncher", "official-news.json");

    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex ItemRegex = new(
        "<a\\s+href=\"(?<url>[^\"]*?/news/(?:official|update)/[^\"]+\\.html)\"[^>]*class=\"item\"[^>]*>\\s*<p><i>(?<tag>.*?)</i>\\s*(?<title>.*?)</p>\\s*<span>(?<date>[^<]+)</span>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    static OfficialNewsService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static async Task<OfficialNewsResult> FetchAsync()
    {
        try
        {
            var all = new List<Announcement>();
            foreach (string url in new[] { SourceUrl, NewsUrl })
            {
                var items = await FetchPageAsync(url).ConfigureAwait(false);
                all.AddRange(items);
            }

            var merged = all
                .Where(a => !string.IsNullOrWhiteSpace(a.Title))
                .GroupBy(a => a.Url)
                .Select(g => g.First())
                .OrderByDescending(a => a.Date)
                .Take(8)
                .ToList();

            if (merged.Count > 0)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
                    await File.WriteAllTextAsync(CachePath,
                        JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true }),
                        new UTF8Encoding(false)).ConfigureAwait(false);
                }
                catch { }
                return new OfficialNewsResult(merged, true);
            }
        }
        catch
        {
            // Fall through to cache/local data.
        }

        try
        {
            if (File.Exists(CachePath))
            {
                var cached = JsonSerializer.Deserialize<List<Announcement>>(
                    await File.ReadAllTextAsync(CachePath).ConfigureAwait(false));
                if (cached is { Count: > 0 })
                    return new OfficialNewsResult(cached, false);
            }
        }
        catch { }

        return new OfficialNewsResult(Store.Instance.Announcements, false);
    }

    private static async Task<List<Announcement>> FetchPageAsync(string url)
    {
        byte[] bytes = await Http.GetByteArrayAsync(url).ConfigureAwait(false);
        string html;
        try
        {
            html = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            html = Encoding.GetEncoding("gb18030").GetString(bytes);
        }

        var result = new List<Announcement>();
        foreach (Match match in ItemRegex.Matches(html))
        {
            string rawTitle = Regex.Replace(match.Groups["title"].Value, "<.*?>", " ");
            string title = WebUtility.HtmlDecode(rawTitle).Trim();
            string tag = WebUtility.HtmlDecode(Regex.Replace(match.Groups["tag"].Value, "<.*?>", " ")).Trim();
            string date = WebUtility.HtmlDecode(match.Groups["date"].Value).Trim();
            string link = match.Groups["url"].Value.Trim();

            if (link.StartsWith("//", StringComparison.Ordinal))
                link = "https:" + link;
            else if (link.StartsWith("/", StringComparison.Ordinal))
                link = "https://id5.163.com" + link;

            if (title.Length == 0) continue;
            result.Add(new Announcement
            {
                Date = date,
                Title = title,
                Summary = "第五人格官方公告",
                Tag = tag.Length == 0 ? "公告" : tag,
                Url = link,
            });
        }
        return result;
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { UseProxy = true, AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36");
        return client;
    }
}
