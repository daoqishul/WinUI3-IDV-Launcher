using System.Security.Principal;

namespace IdvLauncher.Services;

public static class EnginePaths
{
    public static string DataDir
    {
        get
        {
#if DEBUG
            string? testDir = Environment.GetEnvironmentVariable("IDVLAUNCHER_ENGINE_DATA");
            if (!string.IsNullOrWhiteSpace(testDir)) return testDir;
#endif
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "idv-login");
        }
    }

    public static string DisplayDataDir
    {
        get
        {
#if DEBUG
            string? display = Environment.GetEnvironmentVariable("IDVLAUNCHER_ENGINE_DISPLAY");
            if (!string.IsNullOrWhiteSpace(display)) return display;
#endif
            return DataDir;
        }
    }

    public static string ConfigPath => Path.Combine(DataDir, "config.json");
    public static string ChannelsPath => Path.Combine(DataDir, "channels-v2.json");
    public static string LogPath => Path.Combine(DataDir, "log.txt");

    public static string BundledRoot =>
        Path.Combine(AppContext.BaseDirectory, "engine", "IDV-Login");

    public static IReadOnlyList<string> Candidates { get; } = new[]
    {
        BundledRoot,
        Path.Combine(AppContext.BaseDirectory, "engine"),
        @"D:\ProgramData\IDV-Login",
        @"C:\ProgramData\IDV-Login",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "IDV-Login"),
    };

    public static bool IsProcessElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool IsElevated
    {
        get
        {
#if DEBUG
            if (Environment.GetEnvironmentVariable("IDVLAUNCHER_FAKE_ADMIN") == "1") return true;
#endif
            return IsProcessElevated;
        }
    }

    public static bool IsEngineRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            return File.Exists(Path.Combine(path, "python-embed", "python.exe"))
                   && (File.Exists(Path.Combine(path, "src", "main.pyc"))
                       || File.Exists(Path.Combine(path, "src", "main.py")));
        }
        catch
        {
            return false;
        }
    }

    public static string DetectInstallRoot()
    {
        foreach (string candidate in Candidates)
        {
            if (IsEngineRoot(candidate)) return candidate;
        }
        return "";
    }

    public static string PythonExe(string root) => Path.Combine(root, "python-embed", "python.exe");

    public static bool ConfigReadable
    {
        get
        {
            try
            {
                using var stream = File.Open(ConfigPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
