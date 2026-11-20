using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IdvLauncher.Services;

public sealed class EngineApiException : Exception
{
    public EngineApiException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed record ManualChannelOption(string Code, string Name);

public sealed record ImportStartResult(bool Pending, string TaskId, bool Success, string Error);

public sealed record ImportStatusResult(string Status, bool Success, bool Cancelled, string Error);

public sealed record ApiActionResult(bool Success, string Error);

public sealed record CloudSyncSettingsResult(
    bool Success, string Error, bool ConsentAck, bool AutoSync, string SavedMasterKey);

public sealed record CloudSyncActionResult(bool Success, string Error);

public sealed class EngineApiClient
{
    public static EngineApiClient Instance { get; } = new();

    private readonly HttpClient _http;

    private EngineApiClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };

        string baseUrl = "https://localhost";
#if DEBUG
        string? test = Environment.GetEnvironmentVariable("IDVLAUNCHER_API_BASE");
        if (!string.IsNullOrWhiteSpace(test)) baseUrl = test.TrimEnd('/');
#endif
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            using var response = await _http.GetAsync("_idv-login/health", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<ManualChannelOption>> GetManualChannelsAsync(string gameId)
    {
        using var response = await _http.GetAsync($"_idv-login/manualChannels?game_id={Uri.EscapeDataString(gameId)}");
        string text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new EngineApiException($"渠道列表请求失败（HTTP {(int)response.StatusCode}）。");

        var options = new List<ManualChannelOption>();
        try
        {
            JsonNode? root = JsonNode.Parse(text);
            if (root is JsonArray array)
            {
                foreach (JsonNode? item in array)
                {
                    if (item is not JsonObject obj) continue;
                    string code = obj["channel"]?.GetValue<string>()
                                  ?? obj["app_channel"]?.GetValue<string>()
                                  ?? "";
                    string name = obj["name"]?.GetValue<string>()
                                  ?? obj["channel_name"]?.GetValue<string>()
                                  ?? code;
                    if (string.IsNullOrWhiteSpace(code)) continue;
                    if (options.All(o => !string.Equals(o.Code, code, StringComparison.OrdinalIgnoreCase)))
                        options.Add(new ManualChannelOption(code, name));
                }
            }
        }
        catch (JsonException ex)
        {
            throw new EngineApiException("渠道列表返回格式异常。", ex);
        }
        return options;
    }

    public async Task<ImportStartResult> StartImportAsync(string channel, string gameId)
    {
        // Prefer the channel's web login page where the engine supports it.
        string loginMethod = channel is "huawei" or "bilibili_sdk" ? "web" : "";
        string url = $"_idv-login/import?channel={Uri.EscapeDataString(channel)}&game_id={Uri.EscapeDataString(gameId)}";
        if (loginMethod.Length > 0) url += $"&login_method={loginMethod}";

        using var response = await _http.GetAsync(url);
        string text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new EngineApiException($"启动渠道登录失败（HTTP {(int)response.StatusCode}）。");

        try
        {
            JsonNode? root = JsonNode.Parse(text);
            if (root is not JsonObject obj)
                throw new EngineApiException("渠道登录接口返回格式异常。");

            string status = obj["status"]?.GetValue<string>() ?? "";
            string taskId = obj["task_id"]?.GetValue<string>() ?? "";
            bool success = obj["success"]?.GetValue<bool>() ?? false;
            string error = obj["error"]?.GetValue<string>() ?? "";
            return new ImportStartResult(status == "pending", taskId, success, error);
        }
        catch (JsonException ex)
        {
            throw new EngineApiException("渠道登录接口返回格式异常。", ex);
        }
    }

    public async Task<ImportStatusResult> GetImportStatusAsync(string taskId)
    {
        using var response = await _http.GetAsync($"_idv-login/import-status?task_id={Uri.EscapeDataString(taskId)}");
        string text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new EngineApiException($"读取登录状态失败（HTTP {(int)response.StatusCode}）。");

        try
        {
            JsonNode? root = JsonNode.Parse(text);
            if (root is not JsonObject obj)
                return new ImportStatusResult("pending", false, false, "");
            return new ImportStatusResult(
                obj["status"]?.GetValue<string>() ?? "pending",
                obj["success"]?.GetValue<bool>() ?? false,
                obj["cancelled"]?.GetValue<bool>() ?? false,
                obj["error"]?.GetValue<string>() ?? "");
        }
        catch (JsonException ex)
        {
            throw new EngineApiException("登录状态返回格式异常。", ex);
        }
    }

    public async Task<ApiActionResult> RenameAsync(string uuid, string newName)
    {
        using var response = await _http.GetAsync(
            $"_idv-login/rename?uuid={Uri.EscapeDataString(uuid)}&new_name={Uri.EscapeDataString(newName)}");
        string text = await response.Content.ReadAsStringAsync();
        return ParseAction(text, response.IsSuccessStatusCode);
    }

    public async Task<ApiActionResult> DeleteAsync(string uuid)
    {
        using var response = await _http.GetAsync($"_idv-login/del?uuid={Uri.EscapeDataString(uuid)}");
        string text = await response.Content.ReadAsStringAsync();
        return ParseAction(text, response.IsSuccessStatusCode);
    }

    public async Task<ApiActionResult> SwitchAccountAsync(string uuid, string gameId)
    {
        using var response = await _http.GetAsync(
            $"_idv-login/switch?uuid={Uri.EscapeDataString(uuid)}&game_id={Uri.EscapeDataString(gameId)}");
        string text = await response.Content.ReadAsStringAsync();
        return ParseAction(text, response.IsSuccessStatusCode);
    }

    public async Task<ApiActionResult> StartGameAsync(string gameId)
    {
        using var response = await _http.GetAsync(
            $"_idv-login/start-game?game_id={Uri.EscapeDataString(gameId)}");
        string text = await response.Content.ReadAsStringAsync();
        return ParseAction(text, response.IsSuccessStatusCode);
    }

    public async Task<CloudSyncSettingsResult> GetCloudSyncSettingsAsync()
    {
        using var response = await _http.GetAsync("_idv-login/cloud-sync/settings");
        string text = await response.Content.ReadAsStringAsync();
        try
        {
            JsonNode? root = JsonNode.Parse(text);
            var settings = root?["settings"] as JsonObject;
            return new CloudSyncSettingsResult(
                root?["success"]?.GetValue<bool>() ?? response.IsSuccessStatusCode,
                root?["error"]?.GetValue<string>() ?? "",
                settings?["consent_ack"]?.GetValue<bool>() ?? false,
                settings?["auto_sync"]?.GetValue<bool>() ?? false,
                settings?["saved_master_key"]?.GetValue<string>() ?? "");
        }
        catch
        {
            return new CloudSyncSettingsResult(false, "云存档设置返回格式异常。", false, false, "");
        }
    }

    public async Task<CloudSyncActionResult> SaveCloudSyncSettingsAsync(
        bool consent, bool autoSync, string masterKey)
    {
        var body = new JsonObject
        {
            ["consent_ack"] = consent,
            ["remember_level"] = "none",
            ["saved_master_key"] = "",
            ["auto_sync"] = autoSync,
            ["sync_direction"] = "bidirectional",
            ["scope_type"] = "all",
            ["scope_game_id"] = "",
            ["scope_uuids"] = new JsonArray(),
            ["expire_time"] = 259200,
            ["master_key"] = masterKey,
        };
        return await PostCloudActionAsync("_idv-login/cloud-sync/settings", body);
    }

    public async Task<CloudSyncActionResult> RunCloudSyncAsync(
        string action, string masterKey, bool consent, string direction = "bidirectional")
    {
        var body = new JsonObject
        {
            ["action"] = action,
            ["master_key"] = masterKey,
            ["consent_ack"] = consent,
            ["sync_direction"] = direction,
            ["scope_type"] = "all",
            ["expire_time"] = 259200,
        };
        return await PostCloudActionAsync("_idv-login/cloud-sync/run", body);
    }

    public async Task<CloudSyncActionResult> ProbeCloudSyncAsync(string masterKey)
    {
        var body = new JsonObject { ["master_key"] = masterKey };
        return await PostCloudActionAsync("_idv-login/cloud-sync/probe", body);
    }

    public async Task<CloudSyncActionResult> DeleteCloudSyncAsync(string masterKey)
    {
        var body = new JsonObject { ["master_key"] = masterKey };
        return await PostCloudActionAsync("_idv-login/cloud-sync/delete", body);
    }

    private async Task<CloudSyncActionResult> PostCloudActionAsync(string path, JsonObject body)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(path, content);
        string text = await response.Content.ReadAsStringAsync();
        try
        {
            JsonNode? root = JsonNode.Parse(text);
            bool success = root?["success"]?.GetValue<bool>() ?? response.IsSuccessStatusCode;
            string error = root?["error"]?.GetValue<string>() ?? "";
            return new CloudSyncActionResult(success, error);
        }
        catch
        {
            return new CloudSyncActionResult(false, "云存档返回格式异常。");
        }
    }

    private static ApiActionResult ParseAction(string text, bool httpOk)
    {
        try
        {
            JsonNode? root = JsonNode.Parse(text);
            if (root is not JsonObject obj)
                return new ApiActionResult(httpOk, httpOk ? "" : "请求失败。");
            bool success = obj["success"]?.GetValue<bool>() ?? httpOk;
            string error = obj["error"]?.GetValue<string>() ?? "";
            return new ApiActionResult(success, error);
        }
        catch
        {
            return new ApiActionResult(httpOk, httpOk ? "" : "请求失败。");
        }
    }
}
