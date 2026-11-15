using System.Diagnostics;

namespace IdvLauncher.Services;

public static class GameLauncher
{
    public static bool IsRunning()
    {
        foreach (string name in new[] { "dwrg", "identityv", "idv" })
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    foreach (var process in processes) process.Dispose();
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    public static async Task<bool> CloseGameAsync()
    {
        var processes = new List<Process>();
        try
        {
            foreach (string name in new[] { "dwrg", "identityv", "idv" })
                processes.AddRange(Process.GetProcessesByName(name));
        }
        catch { return false; }

        if (processes.Count == 0) return false;

        foreach (var process in processes)
        {
            try { process.CloseMainWindow(); } catch { }
        }

        await Task.Delay(1500);
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
            finally { process.Dispose(); }
        }
        return true;
    }

    public static int FindProcessId(string? gamePath = null)
    {
        string configured = "";
        try
        {
            if (!string.IsNullOrWhiteSpace(gamePath))
                configured = Path.GetFullPath(gamePath);
        }
        catch { }

        // Never enumerate every process here: reading MainModule for all
        // processes was the source of the visible stutter when opening 解帧.
        var names = new List<string> { "dwrg", "identityv", "idv", "第五人格" };
        if (configured.Length > 0)
        {
            string stem = Path.GetFileNameWithoutExtension(configured);
            if (!string.IsNullOrWhiteSpace(stem) &&
                !names.Contains(stem, StringComparer.OrdinalIgnoreCase))
                names.Add(stem);
        }

        int nameFallback = 0;
        int windowFallback = 0;
        var seen = new HashSet<int>();

        foreach (string name in names)
        {
            Process[] processes;
            try { processes = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (Process process in processes)
            {
                try
                {
                    if (!seen.Add(process.Id)) continue;

                    string path = "";
                    try { path = process.MainModule?.FileName ?? ""; } catch { }
                    bool pathMatch = configured.Length > 0 &&
                                     path.Equals(configured, StringComparison.OrdinalIgnoreCase);

                    if (pathMatch)
                        return process.Id;
                    if (nameFallback == 0)
                        nameFallback = process.Id;
                    if (windowFallback == 0 && process.MainWindowHandle != IntPtr.Zero)
                        windowFallback = process.Id;
                }
                catch { }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return windowFallback != 0 ? windowFallback : nameFallback;
    }

    public static bool Launch(string path, string arguments)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            var info = new ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments ?? "",
                WorkingDirectory = Path.GetDirectoryName(path) ?? "",
                UseShellExecute = true,
            };
            Process.Start(info);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
