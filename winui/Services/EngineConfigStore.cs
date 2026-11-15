using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdvLauncher.Models;

namespace IdvLauncher.Services;

public sealed class EngineConfigSnapshot
{
    public bool Loaded { get; init; }
    public bool HasConfig { get; init; }
    public string GameId { get; init; } = "h55";
    public string GamePath { get; init; } = "";
    public string ProxyMode { get; init; } = "compat";
    public int LoginDelay { get; init; } = 6;
    public bool AutoClose { get; init; } = true;
    public string SelectedUuid { get; init; } = "";
    public IReadOnlyList<EngineAccount> Accounts { get; init; } = Array.Empty<EngineAccount>();
    public string Error { get; init; } = "";
}

public sealed record EngineConfigDraft(
    string GameId,
    string GamePath,
    string ProxyMode,
    int LoginDelay,
    bool AutoClose,
    string SelectedUuid);

public sealed class EngineConfigException : Exception
{
    public EngineConfigException(string message, Exception? inner = null) : base(message, inner) { }
}

public static class EngineConfigStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static async Task<EngineConfigSnapshot> LoadAsync(string gameId)
    {
        gameId = NormalizeGameId(gameId);
        if (!File.Exists(EnginePaths.ConfigPath))
        {
            return new EngineConfigSnapshot
            {
                Loaded = true,
                HasConfig = false,
                GameId = gameId,
                GamePath = Store.Instance.Settings.GamePath,
                ProxyMode = "compat",
                LoginDelay = 6,
                AutoClose = true,
                Accounts = await ReadAccountsAsync().ConfigureAwait(false),
            };
        }

        try
        {
            string text = await ReadAllTextSharedAsync(EnginePaths.ConfigPath).ConfigureAwait(false);
            JsonObject root = JsonNode.Parse(text) as JsonObject
                              ?? throw new EngineConfigException("config.json 顶层不是 JSON 对象");

            string proxyMode = root["proxy_mode"]?.GetValue<string>() ?? "compat";
            JsonObject settings = root["game_settings"] as JsonObject ?? new JsonObject();
            string gameKey = FindGameKey(settings, gameId);
            JsonObject? game = gameKey.Length > 0 ? settings[gameKey] as JsonObject : null;

            int delay = ReadInt(game?["login_delay"], 6);
            bool autoClose = ReadBool(game?["auto_close_after_login"], true);
            string path = game?["path"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(path)) path = Store.Instance.Settings.GamePath;

            string selected = root[$"auto-{gameId}"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(selected) && gameKey.Length > 0)
                selected = root[$"auto-{gameKey}"]?.GetValue<string>() ?? "";

            return new EngineConfigSnapshot
            {
                Loaded = true,
                HasConfig = true,
                GameId = gameId,
                GamePath = path,
                ProxyMode = NormalizeProxyMode(proxyMode),
                LoginDelay = Math.Clamp(delay, 0, 30),
                AutoClose = autoClose,
                SelectedUuid = selected,
                Accounts = await ReadAccountsAsync().ConfigureAwait(false),
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or JsonException or EngineConfigException)
        {
            return new EngineConfigSnapshot
            {
                Loaded = false,
                GameId = gameId,
                GamePath = Store.Instance.Settings.GamePath,
                ProxyMode = "compat",
                LoginDelay = 6,
                AutoClose = true,
                Accounts = Array.Empty<EngineAccount>(),
                Error = ex is UnauthorizedAccessException
                    ? "无法读取原版配置：需要以管理员身份运行启动器。"
                    : $"无法读取原版配置：{ex.Message}",
            };
        }
    }

    public static async Task SaveAsync(EngineConfigDraft draft)
    {
        if (!EnginePaths.IsElevated)
            throw new EngineConfigException("保存登录配置需要管理员权限，请以管理员身份重新启动启动器。");

        string gameId = NormalizeGameId(draft.GameId);
        if (!File.Exists(EnginePaths.ConfigPath) && !Directory.Exists(EnginePaths.DataDir))
            throw new EngineConfigException("尚未找到原版 IDV-Login 数据目录，请先运行一次原版工具。");

        JsonObject root;
        if (File.Exists(EnginePaths.ConfigPath))
        {
            string text = await ReadAllTextSharedAsync(EnginePaths.ConfigPath).ConfigureAwait(false);
            root = JsonNode.Parse(text) as JsonObject
                   ?? throw new EngineConfigException("config.json 顶层不是 JSON 对象");
        }
        else
        {
            root = new JsonObject();
        }

        root["proxy_mode"] = NormalizeProxyMode(draft.ProxyMode);

        if (root["game_settings"] is not JsonObject settings)
        {
            settings = new JsonObject();
            root["game_settings"] = settings;
        }

        string gameKey = FindGameKey(settings, gameId);
        if (gameKey.Length == 0) gameKey = gameId;

        if (settings[gameKey] is not JsonObject game)
        {
            game = new JsonObject();
            settings[gameKey] = game;
        }

        game["game_id"] = game["game_id"]?.GetValue<string>() is { Length: > 0 } existingId ? existingId : gameId;
        if (game["name"] is null) game["name"] = "第五人格";
        if (game["account_switching"] is null) game["account_switching"] = new JsonObject();
        // The integrated launcher starts dwrg.exe itself; do not let the engine
        // auto-start a second copy when it boots.
        game["should_auto_start"] = false;
        if (game["last_used_time"] is null) game["last_used_time"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (game["version"] is null) game["version"] = "";
        if (game["default_distribution"] is null) game["default_distribution"] = -1;
        if (!string.IsNullOrWhiteSpace(draft.GamePath)) game["path"] = draft.GamePath.Trim();
        game["login_delay"] = Math.Clamp(draft.LoginDelay, 0, 30);
        game["auto_close_after_login"] = draft.AutoClose;

        // The engine keys the selected account by the short game id (auto-h55),
        // while game_settings may be keyed by a long installation id.
        string autoKey = $"auto-{gameId}";
        if (string.IsNullOrWhiteSpace(draft.SelectedUuid))
        {
            root.Remove(autoKey);
            if (!string.Equals(gameKey, gameId, StringComparison.Ordinal))
                root.Remove($"auto-{gameKey}");
        }
        else
        {
            root[autoKey] = draft.SelectedUuid.Trim();
        }

        string json = root.ToJsonString(WriteOptions);
        string temp = EnginePaths.ConfigPath + ".idvlauncher.tmp";
        try
        {
            if (File.Exists(EnginePaths.ConfigPath))
            {
                string backup = Path.Combine(Store.Instance.DataDir, "engine-config-backup.json");
                try { File.Copy(EnginePaths.ConfigPath, backup, true); } catch { }
            }

            await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temp, EnginePaths.ConfigPath, true);
            RestrictConfigAcl(EnginePaths.ConfigPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new EngineConfigException("没有权限写入 config.json，请以管理员身份运行。", ex);
        }
        catch (IOException ex)
        {
            throw new EngineConfigException($"写入 config.json 失败：{ex.Message}", ex);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public static async Task ApplyAutoSelectionAsync(string gameId, bool auto, string selectedUuid)
    {
        var config = await LoadAsync(gameId).ConfigureAwait(false);
        if (!config.Loaded)
            throw new EngineConfigException(config.Error.Length > 0
                ? config.Error
                : "无法读取原版登录配置。");

        string uuid = auto
            ? (string.IsNullOrWhiteSpace(selectedUuid) ? config.SelectedUuid : selectedUuid)
            : "";
        string path = !string.IsNullOrWhiteSpace(config.GamePath)
            ? config.GamePath
            : Store.Instance.Settings.GamePath;

        await SaveAsync(new EngineConfigDraft(
            config.GameId,
            path,
            config.ProxyMode,
            config.LoginDelay,
            config.AutoClose,
            uuid)).ConfigureAwait(false);
    }

    public static async Task RenameChannelRecordAsync(string uuid, string newName)
    {
        if (!EnginePaths.IsElevated)
            throw new EngineConfigException("改名需要管理员权限，请以管理员身份运行启动器。");
        if (!File.Exists(EnginePaths.ChannelsPath))
            throw new EngineConfigException("没有找到原版 channels-v2.json。");

        string text = await ReadAllTextSharedAsync(EnginePaths.ChannelsPath).ConfigureAwait(false);
        JsonArray array = JsonNode.Parse(text) as JsonArray
                          ?? throw new EngineConfigException("channels-v2.json 顶层不是 JSON 数组。");
        JsonObject? record = array.OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(item["uuid"]?.GetValue<string>(), uuid, StringComparison.Ordinal));
        if (record is null)
            throw new EngineConfigException("没有找到要改名的账号记录。");

        record["name"] = newName.Trim();
        await WriteProtectedJsonAsync(EnginePaths.ChannelsPath, array, "channels-v2-backup.json").ConfigureAwait(false);
    }

    public static async Task DeleteChannelRecordAsync(string uuid)
    {
        if (!EnginePaths.IsElevated)
            throw new EngineConfigException("删除账号需要管理员权限，请以管理员身份运行启动器。");
        if (!File.Exists(EnginePaths.ChannelsPath))
            throw new EngineConfigException("没有找到原版 channels-v2.json。");

        string text = await ReadAllTextSharedAsync(EnginePaths.ChannelsPath).ConfigureAwait(false);
        JsonArray array = JsonNode.Parse(text) as JsonArray
                          ?? throw new EngineConfigException("channels-v2.json 顶层不是 JSON 数组。");
        var matches = array.OfType<JsonObject>()
            .Where(item => string.Equals(item["uuid"]?.GetValue<string>(), uuid, StringComparison.Ordinal))
            .ToList();
        if (matches.Count == 0)
            throw new EngineConfigException("没有找到要删除的账号记录。");

        foreach (var record in matches)
            array.Remove(record);

        await WriteProtectedJsonAsync(EnginePaths.ChannelsPath, array, "channels-v2-backup.json").ConfigureAwait(false);

        if (File.Exists(EnginePaths.ConfigPath))
        {
            string configText = await ReadAllTextSharedAsync(EnginePaths.ConfigPath).ConfigureAwait(false);
            if (JsonNode.Parse(configText) is JsonObject config)
            {
                var autoKeys = config.Select(pair => pair.Key)
                    .Where(key => key.StartsWith("auto-", StringComparison.Ordinal)
                                  && string.Equals(config[key]?.GetValue<string>(), uuid, StringComparison.Ordinal))
                    .ToList();
                if (autoKeys.Count > 0)
                {
                    foreach (string key in autoKeys) config.Remove(key);
                    await WriteProtectedJsonAsync(EnginePaths.ConfigPath, config, "engine-config-backup.json").ConfigureAwait(false);
                }
            }
        }
    }

    public static async Task<IReadOnlyList<EngineAccount>> ReadAccountsAsync()
    {
        if (!File.Exists(EnginePaths.ChannelsPath)) return Array.Empty<EngineAccount>();

        try
        {
            string text = await ReadAllTextSharedAsync(EnginePaths.ChannelsPath).ConfigureAwait(false);
            JsonArray array = JsonNode.Parse(text) as JsonArray ?? new JsonArray();
            var accounts = new List<EngineAccount>();
            foreach (JsonNode? item in array)
            {
                if (item is not JsonObject record) continue;
                string uuid = record["uuid"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(uuid)) continue;

                string name = record["name"]?.GetValue<string>()
                              ?? record["nickname"]?.GetValue<string>()
                              ?? uuid;
                string channel = record["channel_name"]?.GetValue<string>()
                                 ?? record["channel"]?.GetValue<string>()
                                 ?? (record["user_info"] as JsonObject)?["login_channel"]?.GetValue<string>()
                                 ?? (record["login_info"] as JsonObject)?["login_channel"]?.GetValue<string>()
                                 ?? "";

                accounts.Add(new EngineAccount
                {
                    Uuid = uuid,
                    Name = name,
                    Channel = channel,
                    RecordSource = record["record_source"]?.GetValue<string>() ?? "",
                    LastLogin = ReadLong(record["last_login_time"], 0),
                });
            }

            return accounts.OrderByDescending(a => a.LastLogin).ToList();
        }
        catch
        {
            return Array.Empty<EngineAccount>();
        }
    }

    public static string ReadLogTail(int maxChars = 6000)
    {
        try
        {
            if (!File.Exists(EnginePaths.LogPath)) return "";
            using var stream = new FileStream(EnginePaths.LogPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, stream.Length - maxChars);
            stream.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            return $"无法读取日志：{ex.Message}";
        }
    }

    private static async Task WriteProtectedJsonAsync(string path, JsonNode node, string backupName)
    {
        string json = node.ToJsonString(WriteOptions);
        string temp = path + ".idvlauncher.tmp";
        try
        {
            if (File.Exists(path))
            {
                string backup = Path.Combine(Store.Instance.DataDir, backupName);
                try { File.Copy(path, backup, true); } catch { }
            }

            await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false)).ConfigureAwait(false);
            File.Move(temp, path, true);
            RestrictConfigAcl(path);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new EngineConfigException("没有权限写入原版账号文件，请以管理员身份运行。", ex);
        }
        catch (IOException ex)
        {
            throw new EngineConfigException($"写入原版账号文件失败：{ex.Message}", ex);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<string> ReadAllTextSharedAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static string FindGameKey(JsonObject settings, string gameId)
    {
        if (settings[gameId] is JsonObject) return gameId;
        foreach (var pair in settings)
        {
            if (pair.Value is not JsonObject game) continue;
            string id = game["game_id"]?.GetValue<string>() ?? pair.Key;
            if (string.Equals(id, gameId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key, gameId, StringComparison.OrdinalIgnoreCase))
                return pair.Key;
            if (id.EndsWith("-" + gameId, StringComparison.OrdinalIgnoreCase)
                || id.EndsWith("_" + gameId, StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith("-" + gameId, StringComparison.OrdinalIgnoreCase)
                || pair.Key.EndsWith("_" + gameId, StringComparison.OrdinalIgnoreCase))
                return pair.Key;
            if (id.StartsWith(gameId + "_", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(gameId + "-", StringComparison.OrdinalIgnoreCase))
                return pair.Key;
        }
        return "";
    }

    private static void RestrictConfigAcl(string path)
    {
        if (!EnginePaths.IsProcessElevated) return;
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "icacls",
                $"\"{path}\" /inheritance:r /grant:r *S-1-5-32-544:(F) /grant:r *S-1-5-18:(F)")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process?.WaitForExit(3000);
        }
        catch
        {
            // The file is still protected by the ProgramData directory ACL.
        }
    }

    private static string NormalizeGameId(string gameId)
    {
        gameId = string.IsNullOrWhiteSpace(gameId) ? "h55" : gameId.Trim();
        foreach (char c in gameId)
        {
            if (!char.IsLetterOrDigit(c) && c is not ('_' or '-'))
                throw new EngineConfigException("游戏 ID 只能包含字母、数字、下划线和连字符。");
        }
        return gameId;
    }

    public static string NormalizeProxyMode(string? mode) => mode?.ToLowerInvariant() switch
    {
        "process" => "process",
        "global" => "global",
        _ => "compat",
    };

    private static int ReadInt(JsonNode? node, int fallback)
    {
        try
        {
            if (node is null) return fallback;
            if (node.GetValueKind() == System.Text.Json.JsonValueKind.Number)
                return node.GetValue<int>();
            return int.TryParse(node.GetValue<string>(), out int value) ? value : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static long ReadLong(JsonNode? node, long fallback)
    {
        try
        {
            if (node is null) return fallback;
            if (node.GetValueKind() == System.Text.Json.JsonValueKind.Number)
            {
                if (node.AsValue().TryGetValue<long>(out long l)) return l;
                if (node.AsValue().TryGetValue<double>(out double d)) return (long)d;
            }
            return long.TryParse(node.GetValue<string>(), out long parsed) ? parsed : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool ReadBool(JsonNode? node, bool fallback)
    {
        try
        {
            if (node is null) return fallback;
            return node.GetValueKind() switch
            {
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => bool.TryParse(node.GetValue<string>(), out bool value) ? value : fallback,
            };
        }
        catch
        {
            return fallback;
        }
    }
}
