using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace IdvLauncher.Services;

public static class FrameUnlockService
{
    private const uint CreateNewConsole = 0x00000010;
    private const uint StartfUseShowWindow = 0x00000001;
    private const short SwHide = 0;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const ushort KeyEvent = 0x0001;

    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "Tools", "解帧.exe");

    public static bool IsAvailable => File.Exists(ToolPath);

    public static async Task<string> ApplyAsync(int fps, CancellationToken cancellationToken = default)
    {
        if (!EnginePaths.IsElevated)
            throw new EngineException("解帧需要管理员权限，请以管理员身份运行启动器。");
        if (!IsAvailable)
            throw new EngineException("没有找到 Tools\\解帧.exe。");

        int pid = GameLauncher.FindProcessId(Store.Instance.Settings.GamePath);
        if (pid == 0)
            throw new EngineException("未检测到第五人格进程。请先进入游戏，再点「应用解帧」。");

        fps = Math.Clamp(fps, 30, 500);
        return await Task.Run(() => RunTool(pid, fps, cancellationToken), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string RunTool(int gamePid, int fps, CancellationToken cancellationToken)
    {
        string workDir = Path.GetDirectoryName(ToolPath) ?? AppContext.BaseDirectory;
        Process? tool = null;
        try
        {
            // ShellExecute gives the console app a real console with valid
            // stdin/stdout handles, unlike CreateProcess(CREATE_NEW_CONSOLE)
            // from a GUI process.
            tool = Process.Start(new ProcessStartInfo
            {
                FileName = ToolPath,
                WorkingDirectory = workDir,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
        }
        catch (Exception ex)
        {
            throw new EngineException("无法启动解帧工具：" + ex.Message);
        }

        if (tool is null)
            throw new EngineException("无法启动解帧工具。");

        try
        {
            LogTool($"tool pid={tool.Id} name={tool.ProcessName}");
            FreeConsole();
            if (!AttachToConsole((uint)tool.Id))
            {
                if (tool.HasExited)
                    throw new EngineException($"解帧工具提前退出（代码 {tool.ExitCode}）。");
                throw new EngineException("解帧工具未能创建可交互控制台。");
            }

            IntPtr conIn = CreateFileW("CONIN$", GenericRead | GenericWrite,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            IntPtr conOut = CreateFileW("CONOUT$", GenericRead | GenericWrite,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            LogTool($"handles in={conIn} out={conOut} err={Marshal.GetLastWin32Error()}");
            if (conIn == IntPtr.Zero || conOut == IntPtr.Zero)
                throw new EngineException("无法连接解帧工具控制台。");

            try
            {
                bool gotIdPrompt = WaitForText(conOut, "请输入指定ID", 12, cancellationToken);
                LogTool("idPrompt=" + gotIdPrompt);
                if (!gotIdPrompt)
                {
                    string current = ReadScreen(conOut);
                    if (current.Contains("游戏似乎未启动", StringComparison.Ordinal))
                        throw new EngineException("解帧工具未识别到游戏进程，请在游戏内重试。");
                    Thread.Sleep(1500);
                }

                // The tool prints "PID-ID: 1" and asks for the ID after "-ID:",
                // not the PID itself. Use 1 as a fallback for a single instance.
                string toolId = FindToolId(conOut, gamePid);
                SendLine(conIn, toolId);
                bool gotFpsPrompt = WaitForText(conOut, "请输入帧数", 5, cancellationToken);
                LogTool("fpsPrompt=" + gotFpsPrompt);
                SendLine(conIn, fps.ToString());
                Thread.Sleep(1000);
                LogTool("after fps");

                string output = "";
                var deadline = DateTime.UtcNow.AddSeconds(20);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output = ReadScreen(conOut);
                    if (output.Contains("修改成功", StringComparison.Ordinal))
                    {
                        try { tool.WaitForExit(1500); } catch { }
                        return "解帧成功。";
                    }

                    if (tool.HasExited)
                    {
                        if (tool.ExitCode == 0)
                            return "解帧成功。";
                        throw new EngineException(
                            $"解帧工具返回错误代码 {tool.ExitCode}。");
                    }
                    Thread.Sleep(250);
                }

                output = ReadScreen(conOut);
                if (output.Contains("修改成功", StringComparison.Ordinal))
                    return "解帧成功。";
                throw new EngineException("解帧工具超时；请确认游戏仍在运行，且未被反作弊拦截。");
            }
            finally
            {
                if (conIn != IntPtr.Zero) CloseHandle(conIn);
                if (conOut != IntPtr.Zero) CloseHandle(conOut);
                FreeConsole();
            }
        }
        finally
        {
            try
            {
                if (!tool.HasExited) tool.Kill(entireProcessTree: true);
            }
            catch { }
            tool.Dispose();
        }
    }

    private static void LogTool(string message)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IDVLauncher");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "frame-tool.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }

    private static string FindToolId(IntPtr conOut, int gamePid)
    {
        string screen = ReadScreen(conOut);
        foreach (Match match in Regex.Matches(screen, @"(\d+)-ID:\s*(\d+)"))
        {
            if (int.TryParse(match.Groups[1].Value, out int pid) && pid == gamePid)
                return match.Groups[2].Value;
        }
        return "1";
    }

    private static bool AttachToConsole(uint pid)
    {
        for (int i = 0; i < 30; i++)
        {
            if (AttachConsole(pid)) return true;
            Thread.Sleep(100);
        }
        return false;
    }

    private static bool WaitForText(IntPtr conOut, string text, int seconds,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReadScreen(conOut).Contains(text, StringComparison.Ordinal))
                return true;
            Thread.Sleep(200);
        }
        return false;
    }

    private static string ReadScreen(IntPtr conOut)
    {
        if (!GetConsoleScreenBufferInfo(conOut, out CONSOLE_SCREEN_BUFFER_INFO info))
        {
            LogTool("screenInfoErr=" + Marshal.GetLastWin32Error());
            return "";
        }
        int length = Math.Max(1, info.dwSize.X * info.dwSize.Y);
        var buffer = new char[length];
        if (ReadConsoleOutputCharacterW(conOut, buffer, (uint)length,
                new COORD { X = 0, Y = 0 }, out uint read))
            return new string(buffer, 0, (int)Math.Min(read, (uint)length));
        LogTool("readScreenErr=" + Marshal.GetLastWin32Error() +
                " size=" + info.dwSize.X + "x" + info.dwSize.Y);
        return "";
    }

    private static void SendLine(IntPtr conIn, string text)
    {
        var records = new List<INPUT_RECORD>();
        foreach (char ch in text)
            AddKey(records, ch, true);
        AddKey(records, '\r', true);
        uint written = 0;
        bool ok = records.Count > 0 &&
                  WriteConsoleInputW(conIn, records.ToArray(), (uint)records.Count, out written);
        LogTool($"input text={text} ok={ok} written={written} err={Marshal.GetLastWin32Error()}");
    }

    private static void AddKey(List<INPUT_RECORD> records, char ch, bool down)
    {
        ushort vk = ch switch
        {
            '\r' => 0x0D,
            >= '0' and <= '9' => (ushort)ch,
            _ => (ushort)(VkKeyScanW(ch) & 0xFF),
        };
        records.Add(new INPUT_RECORD
        {
            EventType = KeyEvent,
            KeyEvent = new KEY_EVENT_RECORD
            {
                bKeyDown = down,
                wRepeatCount = 1,
                wVirtualKeyCode = vk,
                wVirtualScanCode = 0,
                UnicodeChar = down ? ch : '\0',
                dwControlKeyState = 0,
            },
        });
        if (down)
            AddKey(records, ch, false);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public uint dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SMALL_RECT
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CONSOLE_SCREEN_BUFFER_INFO
    {
        public COORD dwSize;
        public COORD dwCursorPosition;
        public ushort wAttributes;
        public SMALL_RECT srWindow;
        public COORD dwMaximumWindowSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct KEY_EVENT_RECORD
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bKeyDown;
        public ushort wRepeatCount;
        public ushort wVirtualKeyCode;
        public ushort wVirtualScanCode;
        public char UnicodeChar;
        public uint dwControlKeyState;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT_RECORD
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KEY_EVENT_RECORD KeyEvent;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(string applicationName, string commandLine,
        IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles,
        uint creationFlags, IntPtr environment, string currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteConsoleInputW(IntPtr consoleInput,
        INPUT_RECORD[] buffer, uint length, out uint written);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ReadConsoleOutputCharacterW(IntPtr consoleOutput,
        [Out] char[] buffer, uint length, COORD readCoord, out uint read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(IntPtr consoleOutput,
        out CONSOLE_SCREEN_BUFFER_INFO info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScanW(char ch);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
