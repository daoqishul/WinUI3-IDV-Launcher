using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IdvLauncher.Services;

public enum GameQualityPreset
{
    Smooth,
    Balanced,
    High,
}

public sealed record GameSettingsOptions(
    GameQualityPreset Quality,
    bool Fullscreen,
    int ResolutionScale,
    bool HighFpsBattle,
    bool HighFpsOutBattle,
    bool AntiAlias,
    bool FrameCheck,
    bool WideFov);

public sealed record GameSettingsSnapshot(
    bool Loaded,
    string Error,
    string Quality,
    bool Fullscreen,
    int ResolutionScale,
    bool HighFpsBattle,
    bool HighFpsOutBattle,
    bool AntiAlias,
    bool FrameCheck,
    bool WideFov);

public static class GameSettingsService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string FindSettingsPath()
    {
        string gamePath = Store.Instance.Settings.GamePath;
        if (string.IsNullOrWhiteSpace(gamePath)) return "";
        try
        {
            string? dir = Path.GetDirectoryName(gamePath);
            if (string.IsNullOrWhiteSpace(dir)) return "";
            string path = Path.Combine(dir, "Documents", "setting_data.json");
            return File.Exists(path) ? path : "";
        }
        catch
        {
            return "";
        }
    }

    public static async Task<GameSettingsSnapshot> LoadAsync()
    {
        string path = FindSettingsPath();
        if (path.Length == 0)
            return new GameSettingsSnapshot(false, "未找到游戏的 setting_data.json，请先进入一次游戏。",
                "", true, 5, true, true, false, false, true);

        try
        {
            string text = await ReadSharedAsync(path).ConfigureAwait(false);
            JsonObject root = JsonNode.Parse(text) as JsonObject
                              ?? throw new InvalidDataException("setting_data.json 顶层不是 JSON 对象");
            JsonObject? graph = root["graph"] as JsonObject;
            if (graph is null)
                return new GameSettingsSnapshot(false, "游戏设置中缺少 graph 配置。",
                    "", true, 5, true, true, false, false, true);

            return new GameSettingsSnapshot(
                true,
                "",
                graph["graph"]?.GetValue<string>() ?? "high",
                IsOn(graph["full_screen"]),
                Math.Clamp(ReadInt(graph["reso_accu_select"], 5), 1, 5),
                IsOn(graph["high_fps"]),
                IsOn(graph["high_fps_out_battle"]),
                IsOn(graph["open_smaa"]),
                IsOn(graph["frame_check_hint"]),
                IsOn(graph["battle_fov_adapt"]));
        }
        catch (Exception ex)
        {
            return new GameSettingsSnapshot(false, $"读取游戏设置失败：{ex.Message}",
                "", true, 5, true, true, false, false, true);
        }
    }

    public static async Task ApplyAsync(GameSettingsOptions options)
    {
        string path = FindSettingsPath();
        if (path.Length == 0)
            throw new InvalidOperationException("未找到游戏设置文件，请先设置游戏路径并进入一次游戏。");

        string text = await ReadSharedAsync(path).ConfigureAwait(false);
        JsonObject root = JsonNode.Parse(text) as JsonObject
                          ?? throw new InvalidDataException("setting_data.json 顶层不是 JSON 对象");
        if (root["graph"] is not JsonObject graph)
            throw new InvalidDataException("游戏设置中缺少 graph 配置。");

        string level = options.Quality switch
        {
            GameQualityPreset.Smooth => "low",
            GameQualityPreset.Balanced => "mid",
            _ => "high",
        };
        bool low = options.Quality == GameQualityPreset.Smooth;
        bool high = options.Quality == GameQualityPreset.High;

        graph["graph"] = level;
        graph["particle"] = level;
        graph["shadow"] = level;
        graph["home_hall_graph"] = level;
        graph["home_hall_material"] = low ? "off" : "on";
        graph["home_hall_shadowmap"] = low ? "off" : "on";
        graph["home_hall_pcf"] = low ? "off" : "on";
        graph["home_hall_ssao"] = high ? "on" : "off";
        graph["home_hall_gtao"] = high ? "on" : "off";
        graph["home_hall_fxaa"] = "on";
        graph["full_screen"] = options.Fullscreen ? "on" : "off";
        graph["reso_accu_select"] = Math.Clamp(options.ResolutionScale, 1, 5);
        graph["high_fps"] = options.HighFpsBattle ? "on" : "off";
        graph["high_fps_out_battle"] = options.HighFpsOutBattle ? "on" : "off";
        graph["open_smaa"] = options.AntiAlias ? "on" : "off";
        graph["frame_check_hint"] = options.FrameCheck ? "on" : "off";
        graph["battle_fov_adapt"] = options.WideFov ? "on" : "off";

        string backup = path + ".idvlauncher.bak";
        try { File.Copy(path, backup, true); } catch { }

        string temp = path + ".idvlauncher.tmp";
        try
        {
            await File.WriteAllTextAsync(temp, root.ToJsonString(WriteOptions), new UTF8Encoding(false))
                .ConfigureAwait(false);
            File.Move(temp, path, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<string> ReadSharedAsync(string path)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }

    private static int ReadInt(JsonNode? node, int fallback)
    {
        try
        {
            return node?.GetValue<int>() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static bool IsOn(JsonNode? node)
    {
        if (node is null) return false;
        try
        {
            return node.GetValue<string>().Equals("on", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            try { return node.GetValue<bool>(); } catch { return false; }
        }
    }
}
