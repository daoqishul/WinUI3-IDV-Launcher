using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace IdvLauncher.Services;

public sealed record GameDownloadOption(int DistributionId, string Name, string Description)
{
    public string Display => DistributionId > 0 ? $"{Name} · {Description}" : Name;
}

public sealed record GameDownloadProgress(
    bool Installed,
    bool Running,
    double Percent,
    string Status,
    string Error,
    string GamePath);

public static class GameDownloadService
{
    private static readonly HttpClient Http = CreateClient();
    private static DateTime _startedAtUtc = DateTime.MinValue;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    public static async Task<IReadOnlyList<GameDownloadOption>> GetOptionsAsync()
    {
        var options = new List<GameDownloadOption>();
        try
        {
            string text = await Http.GetStringAsync(
                $"_idv-login/launcher-status?game_id={Uri.EscapeDataString(Store.Instance.Settings.EngineGameId)}");
            JsonNode? root = JsonNode.Parse(text);
            CollectOptions(root, options);
        }
        catch
        {
            // The engine may not be ready yet. Fall back to its default
            // distribution, which is still enough for a fresh download.
        }

        if (options.Count == 0)
            options.Add(new GameDownloadOption(-1, "官方 PC 版", "由登录引擎选择默认版本"));
        options.Add(new GameDownloadOption(-2, "共研服（独立客户端）",
            "需官方共研资格；原软件目录未提供独立分发"));
        return options
            .GroupBy(o => o.DistributionId)
            .Select(g => g.First())
            .Take(30)
            .ToList();
    }

    private static void CollectOptions(JsonNode? node, List<GameDownloadOption> options, int depth = 0)
    {
        if (node is null || depth > 6) return;
        if (node is JsonObject obj)
        {
            int id = ReadInt(obj, "distribution_id", "distributionId", "id");
            string name = ReadString(obj, "name", "display_name", "displayName", "channel_name");
            if (id > 0 && name.Length > 0 &&
                options.All(o => o.DistributionId != id))
            {
                string desc = ReadString(obj, "channel", "source", "description");
                options.Add(new GameDownloadOption(id, name, desc.Length > 0 ? desc : "官方渠道"));
            }

            foreach (var child in obj)
                CollectOptions(child.Value, options, depth + 1);
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
                CollectOptions(child, options, depth + 1);
        }
    }

    public static async Task StartAsync(GameDownloadOption option, string targetDir)
    {
        if (option.DistributionId == -2)
            throw new EngineException("共研服为独立客户端，原版引擎目录中没有它的下载分发。请使用官方共研资格邮件中的安装包。");

        _startedAtUtc = DateTime.UtcNow;
        if (!EngineService.Instance.IsReady)
            await EngineService.Instance.StartAsync().ConfigureAwait(false);

        string gameId = Uri.EscapeDataString(Store.Instance.Settings.EngineGameId);
        string dir = Uri.EscapeDataString(targetDir);
        string url = $"_idv-login/launcher-install?game_id={gameId}&target_dir={dir}";
        if (option.DistributionId > 0)
            url += $"&distribution_id={option.DistributionId}";

        using var response = await Http.GetAsync(url).ConfigureAwait(false);
        string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string error = "";
            try { error = JsonNode.Parse(text)?["error"]?.GetValue<string>() ?? ""; } catch { }
            throw new EngineException(string.IsNullOrWhiteSpace(error)
                ? $"启动游戏下载失败（HTTP {(int)response.StatusCode}）。"
                : error);
        }
    }

    public static async Task<GameDownloadProgress> GetProgressAsync()
    {
        bool installed = false;
        bool running = false;
        double percent = 0;
        string status = "正在检查下载状态…";
        string error = "";
        string path = "";

        try
        {
            string text = await Http.GetStringAsync(
                $"_idv-login/launcher-status?game_id={Uri.EscapeDataString(Store.Instance.Settings.EngineGameId)}")
                .ConfigureAwait(false);
            JsonNode? root = JsonNode.Parse(text);
            if (root is JsonObject obj)
            {
                installed = obj["installed"]?.GetValue<bool>() ?? false;
                running = obj["download_pending"]?.GetValue<bool>() ?? false;
                path = FindGamePath(root) ?? "";
                if (installed)
                {
                    status = "游戏已安装";
                    percent = 100;
                }
            }
        }
        catch { }

        var latest = ReadLatestStatusFile();
        if (latest is not null)
        {
            double p = latest.Value.Percent;
            if (p > percent) percent = p;
            if (!string.IsNullOrWhiteSpace(latest.Value.Status)) status = latest.Value.Status;
            if (!string.IsNullOrWhiteSpace(latest.Value.Error)) error = latest.Value.Error;
            if (latest.Value.Installed) installed = true;
            if (latest.Value.Running) running = true;
            if (string.IsNullOrWhiteSpace(path)) path = latest.Value.GamePath;
        }

        if (string.IsNullOrWhiteSpace(path))
            path = Store.DetectGamePath();
        return new GameDownloadProgress(installed, running, Math.Clamp(percent, 0, 100),
            status, error, path);
    }

    private static (bool Installed, bool Running, double Percent, string Status, string Error, string GamePath)?
        ReadLatestStatusFile()
    {
        try
        {
            string dir = EnginePaths.DataDir;
            if (!Directory.Exists(dir)) return null;
            var file = new DirectoryInfo(dir)
                .GetFiles("download_status_*.json")
                .Where(f => f.LastWriteTimeUtc >= _startedAtUtc.AddSeconds(-5))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (file is null) return null;

            JsonNode? root = JsonNode.Parse(File.ReadAllText(file.FullName));
            if (root is not JsonObject obj) return null;

            double percent = ReadDouble(obj, "progress_percent");
            var stages = obj["stages"] as JsonObject;
            if (stages?["download"] is JsonObject download)
                percent = Math.Max(percent, ReadDouble(download, "percent"));

            string state = ReadString(obj, "status");
            string phase = ReadString(obj, "phase");
            string error = ReadString(obj, "error");
            bool success = obj["success"]?.GetValue<bool>() ?? false;
            bool installed = success && (state == "done" || phase == "finished") && percent >= 99;
            bool running = !installed && error.Length == 0;
            string status = installed
                ? "下载完成，正在完成安装…"
                : string.IsNullOrWhiteSpace(state) ? "正在下载…" : $"正在下载 · {state}";
            string path = FindGamePath(obj) ?? "";
            return (installed, running, percent, status, error, path);
        }
        catch
        {
            return null;
        }
    }

    private static string? FindGamePath(JsonNode? node, int depth = 0)
    {
        if (node is null || depth > 6) return null;
        if (node is JsonObject obj)
        {
            foreach (string key in new[] { "path", "game_path", "install_path", "startup_path" })
            {
                string value = ReadString(obj, key);
                if (value.Length == 0) continue;
                try
                {
                    string? dir = Directory.Exists(value) ? value : Path.GetDirectoryName(value);
                    if (dir is null) continue;
                    string exe = Path.Combine(dir, "dwrg.exe");
                    if (File.Exists(exe)) return exe;
                }
                catch { }
            }
            foreach (var child in obj)
            {
                string? found = FindGamePath(child.Value, depth + 1);
                if (found is not null) return found;
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                string? found = FindGamePath(child, depth + 1);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static string ReadString(JsonObject obj, params string[] keys)
    {
        foreach (string key in keys)
        {
            try
            {
                JsonNode? node = obj[key];
                if (node is JsonValue value)
                {
                    if (value.TryGetValue<string>(out string? text) && !string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch { }
        }
        return "";
    }

    private static int ReadInt(JsonObject obj, params string[] keys)
    {
        foreach (string key in keys)
        {
            try
            {
                JsonNode? node = obj[key];
                if (node is JsonValue value)
                {
                    if (value.TryGetValue<int>(out int number)) return number;
                    if (value.TryGetValue<string>(out string? text) && int.TryParse(text, out number))
                        return number;
                }
            }
            catch { }
        }
        return 0;
    }

    private static double ReadDouble(JsonObject obj, string key)
    {
        try
        {
            JsonNode? node = obj[key];
            if (node is JsonValue value)
            {
                if (value.TryGetValue<double>(out double number)) return number;
                if (value.TryGetValue<string>(out string? text) && double.TryParse(text, out number))
                    return number;
            }
        }
        catch { }
        return 0;
    }
}
