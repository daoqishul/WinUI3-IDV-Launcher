using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Win32;
using IdvLauncher.Models;

namespace IdvLauncher.Services;

public sealed record ImportResult(int Added, string Message);

public sealed class Store
{
    public static Store Instance { get; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string DataDir { get; }
    public string SettingsPath => Path.Combine(DataDir, "settings.json");
    public string AccountsPath => Path.Combine(DataDir, "accounts.json");
    public AppSettings Settings { get; private set; } = new();
    public List<Account> Accounts { get; private set; } = new();
    public List<Announcement> Announcements { get; private set; } = new();
    public string AnnouncementSource { get; private set; } = "";

    private Store()
    {
        DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IDVLauncher");
        Directory.CreateDirectory(DataDir);
    }

    public void Load()
    {
        Settings = ReadJson<AppSettings>(SettingsPath)
                   ?? ReadJson<AppSettings>(SettingsPath + ".bak")
                   ?? new AppSettings();

        // Version 2: preserve the previous window state and turn off the two
        // old "automatic" defaults that surprised existing users.
        if (Settings.SettingsSchema < 2)
        {
            Settings.AutoMinimize = false;
            Settings.AutoScanLogin = false;
            Settings.RememberWindowBounds = true;
            Settings.SettingsSchema = 2;
            SaveSettings();
        }

        // Version 3: restore the auto-login/scan toggle that the previous
        // build removed. Existing installs get it enabled once.
        if (Settings.SettingsSchema < 3)
        {
            Settings.AutoScanLogin = true;
            Settings.SettingsSchema = 3;
            SaveSettings();
        }

        // Version 4: manual QR mode and engine prewarm are opt-in/out
        // independently of the existing auto-login setting.
        if (Settings.SettingsSchema < 4)
        {
            Settings.AutoScanLogin = true;
            Settings.AutoStartEngine = true;
            Settings.ManualQrMode = false;
            Settings.SettingsSchema = 4;
            SaveSettings();
        }

        if (Settings.Theme is < 0 or > 2) Settings.Theme = 0;
        if (Settings.LaunchDelay is < 0 or > 30) Settings.LaunchDelay = Math.Clamp(Settings.LaunchDelay, 0, 30);
        if (Settings.WindowW < 900) Settings.WindowW = 1268;
        if (Settings.WindowH < 640) Settings.WindowH = 690;
        if (Settings.Theme is < 0 or > 2) Settings.Theme = 0;
        if (Settings.BackdropMode is < 0 or > 2) Settings.BackdropMode = 1;
        if (Settings.LastPage is < 0 or > 5) Settings.LastPage = 0;
        if (Settings.EnginePort is < 1024 or > 65535) Settings.EnginePort = 10717;
        if (string.IsNullOrWhiteSpace(Settings.EngineGameId)) Settings.EngineGameId = "h55";
        if (string.IsNullOrWhiteSpace(Settings.EnginePath))
            Settings.EnginePath = EnginePaths.DetectInstallRoot();

        Accounts = ReadJson<List<Account>>(AccountsPath) ?? new List<Account>();
        Accounts = Accounts
            .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Uuid))
            .Select(a =>
            {
                if (string.IsNullOrWhiteSpace(a.Name)) a.Name = a.Uuid;
                if (string.IsNullOrWhiteSpace(a.Source)) a.Source = "local";
                return a;
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(Settings.GamePath))
            Settings.GamePath = DetectGamePath();

        EnsureDefaults();
        ReloadAnnouncements();
    }

    public void EnsureDefaults()
    {
        if (!Accounts.Any(a => a.IsDefault) && Accounts.Count > 0)
        {
            var selected = Accounts.FirstOrDefault(a => a.Uuid == Settings.SelectedAccount) ?? Accounts[0];
            selected.IsDefault = true;
            if (string.IsNullOrEmpty(Settings.SelectedAccount)) Settings.SelectedAccount = selected.Uuid;
        }
        if (string.IsNullOrEmpty(Settings.SelectedAccount) && Accounts.Count > 0)
            Settings.SelectedAccount = Accounts[0].Uuid;
    }

    public Account? FindAccount(string? uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return null;
        return Accounts.FirstOrDefault(a => a.Uuid == uuid);
    }

    public Account? DefaultAccount() => Accounts.FirstOrDefault(a => a.IsDefault);

    public Account? CurrentAccount()
    {
        return DefaultAccount()
               ?? FindAccount(Settings.SelectedAccount)
               ?? Accounts.FirstOrDefault();
    }

    public void SetDefault(string uuid)
    {
        foreach (var account in Accounts) account.IsDefault = account.Uuid == uuid;
        Settings.SelectedAccount = uuid;
        SaveAccounts();
        SaveSettings();
    }

    public void Select(string uuid)
    {
        Settings.SelectedAccount = uuid;
        SaveSettings();
    }

    public string AddOrUpdate(Account draft)
    {
        var existing = Accounts.FirstOrDefault(a => a.Uuid == draft.Uuid);
        if (existing is null)
        {
            Accounts.Add(draft);
            existing = draft;
        }
        else
        {
            existing.Name = draft.Name;
            existing.Channel = draft.Channel;
            existing.Login = draft.Login;
            existing.Source = draft.Source;
            existing.IsDefault = draft.IsDefault;
        }

        if (draft.IsDefault)
            foreach (var account in Accounts) account.IsDefault = account.Uuid == draft.Uuid;
        if (!Accounts.Any(a => a.IsDefault) && Accounts.Count > 0)
            Accounts[0].IsDefault = true;

        Settings.SelectedAccount = draft.Uuid;
        SaveAccounts();
        SaveSettings();
        return draft.Uuid;
    }

    public void Delete(string uuid)
    {
        Accounts.RemoveAll(a => a.Uuid == uuid);
        if (Settings.SelectedAccount == uuid)
            Settings.SelectedAccount = Accounts.FirstOrDefault()?.Uuid ?? "";
        if (!Accounts.Any(a => a.IsDefault) && Accounts.Count > 0)
            Accounts[0].IsDefault = true;
        SaveAccounts();
        SaveSettings();
    }

    public void SaveSettings() => WriteJson(SettingsPath, Settings);
    public void SaveAccounts() => WriteJson(AccountsPath, Accounts);

    public void ReloadAnnouncements()
    {
        string path = Settings.AnnouncementPath;
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(AppContext.BaseDirectory, "data", "announcements.json");

        if (!File.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? DataDir);
                File.WriteAllText(path, DefaultAnnouncementJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        AnnouncementSource = path;
        var list = ReadJson<List<Announcement>>(path);
        if (list is null || list.Count == 0)
        {
            list = JsonSerializer.Deserialize<List<Announcement>>(DefaultAnnouncementJson) ?? new();
        }
        Announcements = list.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Title)).ToList();
    }

    public ImportResult ImportFromIdvLogin()
    {
        var candidates = new List<string>
        {
            @"D:\ProgramData\IDV-Login\channels.json",
            Path.Combine(AppContext.BaseDirectory, "channels.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "channels.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "IDV-Login", "channels.json"),
        };
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(appData, "IDV-Login", "channels.json"));
        candidates.Add(Path.Combine(appData, "IdentityV-login-helper", "channels.json"));
        candidates.Add(Path.Combine(localAppData, "IDV-Login", "channels.json"));
        candidates.Add(Path.Combine(localAppData, "IdentityV-login-helper", "channels.json"));

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is null)
            return new ImportResult(0, "未找到 IDV-Login 的 channels.json");

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(found));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new ImportResult(0, "账号文件格式不正确");

            int added = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                string uuid = GetString(item, "uuid");
                if (string.IsNullOrWhiteSpace(uuid)) continue;
                if (Accounts.Any(a => a.Uuid == uuid)) continue;

                var account = new Account
                {
                    Uuid = uuid,
                    Name = GetString(item, "name"),
                    Channel = GetString(item, "channel_name"),
                    Source = "idv-login",
                };
                if (string.IsNullOrWhiteSpace(account.Name)) account.Name = uuid;

                if (item.TryGetProperty("user_info", out var userInfo) && userInfo.ValueKind == JsonValueKind.Object)
                {
                    if (string.IsNullOrWhiteSpace(account.Channel))
                        account.Channel = GetString(userInfo, "login_channel");
                    account.Login = GetString(userInfo, "uid");
                }
                if (item.TryGetProperty("last_login_time", out var lastLogin) && lastLogin.TryGetInt64(out long ts))
                    account.LastLogin = ts;

                Accounts.Add(account);
                added++;
            }

            if (added > 0)
            {
                EnsureDefaults();
                SaveAccounts();
                SaveSettings();
                return new ImportResult(added, $"已导入 {added} 个账号");
            }
            return new ImportResult(0, "没有新的账号可导入");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new ImportResult(0, "账号文件读取失败");
        }
    }

    public static string DetectGamePath()
    {
        var candidates = new List<string>
        {
            @"D:\dwrg\dwrg.exe",
        };
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IdentityV", "dwrg.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "IdentityV", "dwrg.exe"));
        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;

        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var sub in new[]
                     {
                         @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                         @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                     })
            {
                try
                {
                    using var key = root.OpenSubKey(sub);
                    if (key is null) continue;
                    foreach (var name in key.GetSubKeyNames())
                    {
                        using var item = key.OpenSubKey(name);
                        string display = item?.GetValue("DisplayName") as string ?? "";
                        if (!display.Contains("第五人格") &&
                            !display.Contains("Identity V", StringComparison.OrdinalIgnoreCase)) continue;
                        string location = item?.GetValue("InstallLocation") as string ?? "";
                        var exe = Path.Combine(location, "dwrg.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
        }
        return "";
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static T? ReadJson<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return default;
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".bak", true); } catch { }
            }
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(tmp, path, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private const string DefaultAnnouncementJson = """
    [
      {
        "date": "2026-11-14",
        "tag": "启动器",
        "title": "欢迎使用 IDV 启动器",
        "summary": "在「账号管理」中添加或导入你的第五人格账号，然后从这里一键启动游戏。"
      },
      {
        "date": "2026-11-12",
        "tag": "账号",
        "title": "账号数据与 IDV-Login 互通",
        "summary": "启动器可读取 IDV-Login 的 channels.json，将已有的渠道服账号记录导入本地列表。"
      },
      {
        "date": "2026-11-08",
        "tag": "提示",
        "title": "启动前请确认游戏路径",
        "summary": "首次使用请到「设置 - 游戏与启动」选择 dwrg.exe，启动按钮会自动记录本次登录时间。"
      }
    ]
    """;
}
