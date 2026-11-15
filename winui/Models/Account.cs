using System.Text.Json.Serialization;

namespace IdvLauncher.Models;

public sealed class Account
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "";

    [JsonPropertyName("login")]
    public string Login { get; set; } = "";

    [JsonPropertyName("note")]
    public string Note { get; set; } = "";

    [JsonPropertyName("source")]
    public string Source { get; set; } = "local";

    [JsonPropertyName("lastLogin")]
    public long LastLogin { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }
}
