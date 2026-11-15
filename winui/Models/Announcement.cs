using System.Text.Json.Serialization;

namespace IdvLauncher.Models;

public sealed class Announcement
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
}
