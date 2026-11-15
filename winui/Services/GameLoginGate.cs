using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace IdvLauncher.Services;

/// <summary>
/// Keeps the game's built-in login window out of sight while the engine
/// performs an automatic account login. On success/failure the window is
/// restored; if the engine never reports a result it is restored on timeout.
/// </summary>
public static class GameLoginGate
{
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static long CaptureLogOffset()
    {
        try
        {
            return File.Exists(EnginePaths.LogPath) ? new FileInfo(EnginePaths.LogPath).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static async Task MinimizeUntilLoggedInAsync(
        string gamePath, long logOffset, TimeSpan timeout)
    {
        IntPtr minimizedWindow = IntPtr.Zero;
        try
        {
            var deadline = DateTimeOffset.UtcNow + timeout;
            while (DateTimeOffset.UtcNow < deadline)
            {
                int pid = GameLauncher.FindProcessId(gamePath);
                if (pid != 0)
                {
                    try
                    {
                        using var process = Process.GetProcessById(pid);
                        process.Refresh();
                        IntPtr window = process.MainWindowHandle;
                        if (window != IntPtr.Zero && window != minimizedWindow)
                        {
                            if (ShowWindow(window, SW_MINIMIZE))
                                minimizedWindow = window;
                        }
                    }
                    catch { }
                }

                string appended = ReadLogSince(logOffset);
                if (appended.Contains("自动登录成功", StringComparison.Ordinal) ||
                    appended.Contains("自动登录失败", StringComparison.Ordinal))
                    break;

                await Task.Delay(150).ConfigureAwait(false);
            }
        }
        finally
        {
            if (minimizedWindow != IntPtr.Zero)
            {
                try
                {
                    ShowWindow(minimizedWindow, SW_RESTORE);
                    SetForegroundWindow(minimizedWindow);
                }
                catch { }
            }
        }
    }

    private static string ReadLogSince(long offset)
    {
        try
        {
            if (!File.Exists(EnginePaths.LogPath)) return "";
            using var stream = new FileStream(EnginePaths.LogPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: false);
            if (offset > stream.Length) offset = 0;
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch
        {
            return "";
        }
    }
}
