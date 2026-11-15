using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using IdvLauncher.Models;

namespace IdvLauncher.Services;

public sealed record KeyCodeOption(int Code, string Name);

public sealed class KeyBindingService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private List<KeyActionCategory>? _categories;
    private List<KeyCodeOption>? _codes;

    public IReadOnlyList<KeyActionCategory> Categories => _categories ?? new();
    public IReadOnlyList<KeyCodeOption> Codes => _codes ?? new();

    private static string _discoveredPath = "";

    public static string FindPath()
    {
        if (_discoveredPath.Length > 0 && File.Exists(_discoveredPath))
            return _discoveredPath;

        string savedPath = Store.Instance.Settings.KeyConfigPath;
        if (!string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
            return savedPath;

        string gamePath = Store.Instance.Settings.GamePath;
        if (!string.IsNullOrWhiteSpace(gamePath))
        {
            string? dir = Path.GetDirectoryName(gamePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                string path = Path.Combine(dir, "Documents", "pc_key_setting_data.json");
                if (File.Exists(path)) return path;
            }
        }

        foreach (string candidate in CommonGameRoots())
        {
            string path = Path.Combine(candidate, "Documents", "pc_key_setting_data.json");
            if (File.Exists(path)) return path;
        }
        return "";
    }

    public static void SetManualPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        _discoveredPath = path;
        Store.Instance.Settings.KeyConfigPath = path;
        Store.Instance.SaveSettings();
    }

    public static async Task<string> DiscoverInFolderAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "";
        string found = await Task.Run(() => FindInFolder(folder)).ConfigureAwait(false);
        if (found.Length > 0)
        {
            _discoveredPath = found;
            Store.Instance.Settings.KeyConfigPath = found;
            Store.Instance.SaveSettings();
        }
        return found;
    }

    private static string FindInFolder(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return "";
            string direct = Path.Combine(folder, "pc_key_setting_data.json");
            if (File.Exists(direct)) return direct;

            string documents = Path.Combine(folder, "Documents", "pc_key_setting_data.json");
            if (File.Exists(documents)) return documents;

            string gameExe = Path.Combine(folder, "dwrg.exe");
            if (File.Exists(gameExe))
            {
                string gameDocs = Path.Combine(folder, "Documents", "pc_key_setting_data.json");
                if (File.Exists(gameDocs)) return gameDocs;
            }

            // Search a bounded number of subdirectories. This covers selecting
            // the drive, the game folder, or an unrelated parent folder.
            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((folder, 0));
            int visited = 0;
            while (queue.Count > 0 && visited < 4000)
            {
                var (current, depth) = queue.Dequeue();
                visited++;
                try
                {
                    string candidate = Path.Combine(current, "pc_key_setting_data.json");
                    if (File.Exists(candidate)) return candidate;

                    if (depth >= 4) continue;
                    foreach (string child in Directory.EnumerateDirectories(current))
                    {
                        try
                        {
                            var info = new DirectoryInfo(child);
                            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                            queue.Enqueue((child, depth + 1));
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return "";
    }

    public static async Task<string> DiscoverAsync()
    {
        string found = await Task.Run(() =>
        {
            string detectedGame = Store.DetectGamePath();
            if (!string.IsNullOrWhiteSpace(detectedGame))
            {
                string? dir = Path.GetDirectoryName(detectedGame);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    string path = Path.Combine(dir, "Documents", "pc_key_setting_data.json");
                    if (File.Exists(path)) return path;
                }
            }

            foreach (string root in CommonGameRoots())
            {
                string path = Path.Combine(root, "Documents", "pc_key_setting_data.json");
                if (File.Exists(path)) return path;
            }

            foreach (string parent in new[]
                     {
                         @"D:\", @"C:\", @"C:\Program Files", @"C:\Program Files (x86)",
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     })
            {
                try
                {
                    if (!Directory.Exists(parent)) continue;
                    foreach (string dir in Directory.EnumerateDirectories(parent))
                    {
                        string path = Path.Combine(dir, "Documents", "pc_key_setting_data.json");
                        if (File.Exists(path)) return path;
                    }
                }
                catch { }
            }
            return "";
        }).ConfigureAwait(false);

        _discoveredPath = found;
        if (found.Length > 0)
        {
            Store.Instance.Settings.KeyConfigPath = found;
            Store.Instance.SaveSettings();
        }
        return found;
    }

    private static IEnumerable<string> CommonGameRoots()
    {
        yield return @"D:\dwrg";
        yield return @"C:\dwrg";
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IdentityV");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "IdentityV");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IdentityV");
    }

    public async Task<bool> LoadCatalogAsync()
    {
        try
        {
            string actionsPath = Path.Combine(AppContext.BaseDirectory, "Assets", "key-actions.json");
            string codesPath = Path.Combine(AppContext.BaseDirectory, "Assets", "key-codes.json");
            if (!File.Exists(actionsPath) || !File.Exists(codesPath)) return false;

            string actionsJson = await File.ReadAllTextAsync(actionsPath, Encoding.UTF8).ConfigureAwait(false);
            string codesJson = await File.ReadAllTextAsync(codesPath, Encoding.UTF8).ConfigureAwait(false);
            var actions = JsonSerializer.Deserialize<KeyActionsFile>(actionsJson, ReadOptions);
            var codes = JsonSerializer.Deserialize<KeyCodesFile>(codesJson, ReadOptions);
            _categories = actions?.Categories?
                .Where(c => c.Actions is { Count: > 0 })
                .ToList() ?? new();
            _codes = codes?.Keys?
                .Select(k => new KeyCodeOption(k.Code, k.Name))
                .DistinctBy(k => k.Code)
                .OrderBy(k => k.Code)
                .ToList() ?? new();
            return _categories.Count > 0 && _codes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<KeyBindingRow>> LoadBindingsAsync()
    {
        string path = FindPath();
        if (path.Length == 0)
            throw new InvalidOperationException("未找到 pc_key_setting_data.json，请先设置游戏路径并进入一次游戏。");

        string text = await File.ReadAllTextAsync(path, Encoding.UTF8).ConfigureAwait(false);
        JsonObject root = JsonNode.Parse(text) as JsonObject
                          ?? throw new InvalidDataException("pc_key_setting_data.json 顶层不是 JSON 对象");
        JsonObject? control = root["control"] as JsonObject;
        JsonObject? op2key = control?["op2key"] as JsonObject;
        if (op2key is null)
            throw new InvalidDataException("键位配置缺少 control.op2key。");

        var rows = new List<KeyBindingRow>();
        var seen = new HashSet<int>();
        foreach (var category in Categories)
        {
            foreach (var action in category.Actions)
            {
                if (!seen.Add(action.Op)) continue;
                JsonNode? binding = op2key[action.Op.ToString()];
                rows.Add(new KeyBindingRow
                {
                    Op = action.Op,
                    Name = action.Name,
                    Category = category.Name,
                    Group = ToGroup(category.Name),
                    KeyCode = ReadKeyCode(binding),
                    KeyDisplay = FormatBinding(binding),
                });
            }
        }
        return rows;
    }

    public async Task SetBindingAsync(int op, IReadOnlyList<int> keyCodes)
    {
        int[] codes = keyCodes.Where(c => c > 0).Distinct().ToArray();
        if (codes.Length == 0) return;
        string path = FindPath();
        if (path.Length == 0)
            throw new InvalidOperationException("未找到 pc_key_setting_data.json。");

        string text = await File.ReadAllTextAsync(path, Encoding.UTF8).ConfigureAwait(false);
        JsonObject root = JsonNode.Parse(text) as JsonObject
                          ?? throw new InvalidDataException("pc_key_setting_data.json 顶层不是 JSON 对象");
        JsonObject? control = root["control"] as JsonObject;
        JsonObject? op2key = control?["op2key"] as JsonObject;
        if (control is null || op2key is null)
            throw new InvalidDataException("键位配置缺少 control.op2key。");

        if (control["key2op_lst"] is not JsonObject key2op)
        {
            key2op = new JsonObject();
            control["key2op_lst"] = key2op;
        }

        // Remove this operation from every previous key bucket.
        foreach (var pair in key2op)
        {
            if (pair.Value is not JsonArray array) continue;
            for (int i = array.Count - 1; i >= 0; i--)
            {
                if (array[i] is JsonValue value && value.TryGetValue(out int existing) && existing == op)
                    array.RemoveAt(i);
            }
        }

        string keyName = codes.Length == 1
            ? codes[0].ToString()
            : "(" + string.Join(", ", codes) + ")";
        if (key2op[keyName] is not JsonArray target)
        {
            target = new JsonArray();
            key2op[keyName] = target;
        }
        if (!target.Any(n => n is JsonValue v && v.TryGetValue(out int existing) && existing == op))
            target.Add(op);

        if (codes.Length == 1)
        {
            op2key[op.ToString()] = codes[0];
        }
        else
        {
            var array = new JsonArray();
            foreach (int code in codes) array.Add(code);
            op2key[op.ToString()] = array;
        }

        string backup = path + ".idvlauncher.bak";
        try { File.Copy(path, backup, true); } catch { }

        string temp = path + ".idvlauncher.tmp";
        try
        {
            await File.WriteAllTextAsync(temp, root.ToJsonString(), new UTF8Encoding(false))
                .ConfigureAwait(false);
            File.Move(temp, path, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public string KeyName(int? code)
    {
        if (code is null) return "未设置";
        return Codes.FirstOrDefault(c => c.Code == code.Value)?.Name ?? $"键码 {code.Value}";
    }

    private string FormatBinding(JsonNode? node)
    {
        if (node is null) return "未设置";
        try
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue(out int code)) return KeyName(code);
                if (value.TryGetValue<string>(out string? text) && int.TryParse(text, out int parsed))
                    return KeyName(parsed);
            }
            if (node is JsonArray array)
            {
                var names = new List<string>();
                foreach (var item in array)
                {
                    if (item is JsonValue v && v.TryGetValue(out int code))
                        names.Add(KeyName(code));
                    else if (item is JsonValue s && s.TryGetValue<string>(out string? text) &&
                             int.TryParse(text, out int parsed))
                        names.Add(KeyName(parsed));
                }
                return names.Count > 0 ? string.Join(" + ", names) : "未设置";
            }
        }
        catch { }
        return "未设置";
    }

    private static string ToGroup(string category)
    {
        if (category == "通用") return "通用";
        if (category.StartsWith("求生者", StringComparison.Ordinal)) return "求生者";
        if (category.StartsWith("监管者", StringComparison.Ordinal)) return "监管者";
        if (category.StartsWith("模式", StringComparison.Ordinal)) return "其他模式";
        return "加页手记";
    }

    private static int? ReadKeyCode(JsonNode? node)
    {
        if (node is not JsonValue value) return null;
        if (value.TryGetValue(out int code)) return code;
        if (value.TryGetValue<string>(out string? text) && int.TryParse(text, out int parsed)) return parsed;
        return null;
    }

    private sealed class KeyActionsFile
    {
        public List<KeyActionCategory>? Categories { get; set; }
    }

    private sealed class KeyCodesFile
    {
        public List<KeyCodeEntry>? Keys { get; set; }
    }

    private sealed class KeyCodeEntry
    {
        public string Name { get; set; } = "";
        public int Code { get; set; }
    }
}

public sealed class KeyActionCategory
{
    public string Name { get; set; } = "";
    public List<KeyActionEntry> Actions { get; set; } = new();
}

public sealed class KeyActionEntry
{
    public int Op { get; set; }
    public string Name { get; set; } = "";
}

