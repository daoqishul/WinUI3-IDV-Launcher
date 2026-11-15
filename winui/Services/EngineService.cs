using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace IdvLauncher.Services;

public enum EngineState
{
    Stopped,
    Starting,
    Ready,
    Stopping,
    Error,
}

public sealed class EngineException : Exception
{
    public EngineException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed class EngineService
{
    public static EngineService Instance { get; } = new();

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(120);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private long _logOffset;

    public EngineState State { get; private set; } = EngineState.Stopped;
    public string StatusText { get; private set; } = "登录引擎未启动";
    public string LastError { get; private set; } = "";
    public string LogTail { get; private set; } = "";
    public int ProcessId { get; private set; }

    public event EventHandler? StateChanged;

    public bool IsRunning
    {
        get
        {
            try { return _process is { HasExited: false }; }
            catch { return false; }
        }
    }

    public bool IsReady => State == EngineState.Ready && IsRunning;

    public string EngineRoot => Store.Instance.Settings.EnginePath;

    public bool IsInstalled =>
        EnginePaths.IsEngineRoot(EngineRoot) ||
        EnginePaths.IsEngineRoot(EnginePaths.BundledRoot) ||
        EnginePaths.DetectInstallRoot().Length > 0;

    public async Task StartAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!EnginePaths.IsElevated)
                throw new EngineException("启动登录引擎需要管理员权限，请以管理员身份运行启动器。");

            string root = ResolveRoot();
            if (!EnginePaths.IsEngineRoot(root))
                throw new EngineException("没有找到原版 IDV-Login 引擎，请先在设置中指定引擎目录。");

            if (IsReady)
                return;

            if (IsRunning)
            {
                SetState(EngineState.Ready, "登录引擎运行中");
                return;
            }

            int port = Math.Clamp(Store.Instance.Settings.EnginePort, 1024, 65535);
            string python = EnginePaths.PythonExe(root);
            string script = Path.Combine(root, "src", "main.pyc");
            if (!File.Exists(script)) script = Path.Combine(root, "src", "main.py");

            string arguments = $"\"{script}\" --proxy-port {port}";

            LastError = "";
            LogTail = "";
            _logOffset = GetLogLength();
            SetState(EngineState.Starting, "正在启动登录引擎…");

            _process = CreateHiddenProcess(python, arguments, root);
            ProcessId = _process.Id;

            var deadline = DateTimeOffset.UtcNow + StartupTimeout;
            var appended = new StringBuilder();
            int ticks = 0;
            while (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(250).ConfigureAwait(false);
                ticks++;

                if (_process.HasExited)
                {
                    LastError = $"登录引擎意外退出（代码 {_process.ExitCode}）。";
                    LogTail = EngineConfigStore.ReadLogTail();
                    SetState(EngineState.Error, LastError);
                    throw new EngineException(LastError);
                }

                appended.Append(ReadLogSince(ref _logOffset));
                LogTail = TrimLog(appended.ToString());
                if (appended.ToString().Contains("已就绪", StringComparison.Ordinal))
                {
                    SetState(EngineState.Ready, "登录引擎已就绪");
                    return;
                }

                // The HTTP listener can be ready slightly before the log line
                // is flushed; checking it makes startup feel faster.
                if (ticks % 4 == 0 &&
                    await EngineApiClient.Instance.HealthAsync().ConfigureAwait(false))
                {
                    SetState(EngineState.Ready, "登录引擎已就绪");
                    return;
                }
            }

            LastError = "登录引擎启动超时（120 秒）。原版引擎可能弹出了更新或错误窗口。";
            LogTail = EngineConfigStore.ReadLogTail();
            SetState(EngineState.Error, LastError);
            await StopProcessAsync(forceAfterSeconds: 3).ConfigureAwait(false);
            throw new EngineException(LastError);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsureStartedAsync() => await StartAsync().ConfigureAwait(false);

    public async Task LoginAccountAsync(string uuid)
    {
        if (!EnginePaths.IsElevated)
            throw new EngineException("切换渠道账号需要管理员权限，请以管理员身份运行启动器。");
        if (!IsInstalled)
            throw new EngineException("没有找到原版 IDV-Login 引擎，请先在登录引擎页指定目录。");
        if (GameLauncher.IsRunning())
            throw new EngineException("游戏正在运行。请先关闭第五人格，再点击「登录账号」切换渠道。");

        var config = await EngineConfigStore.LoadAsync(Store.Instance.Settings.EngineGameId);
        if (!config.Loaded)
            throw new EngineException(config.Error.Length > 0 ? config.Error : "无法读取原版登录配置。");

        string gamePath = !string.IsNullOrWhiteSpace(config.GamePath)
            ? config.GamePath
            : Store.Instance.Settings.GamePath;
        if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
            throw new EngineException("游戏路径无效，请先在登录引擎页选择 dwrg.exe。");

        // The engine keeps auto-* in memory. Restart it so the selected channel
        // account is applied before the game connects to the login service.
        if (IsRunning)
            await StopAsync().ConfigureAwait(false);

        await EngineConfigStore.SaveAsync(new EngineConfigDraft(
            config.GameId,
            gamePath,
            config.ProxyMode,
            config.LoginDelay,
            config.AutoClose,
            uuid)).ConfigureAwait(false);

        await StartAsync().ConfigureAwait(false);
        await LaunchGameAsync(gamePath, Store.Instance.Settings.GameArgs).ConfigureAwait(false);
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopProcessAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void StopSync()
    {
        try
        {
            if (_process is null || _process.HasExited)
            {
                SetState(EngineState.Stopped, "登录引擎未启动");
                return;
            }

            SetState(EngineState.Stopping, "正在停止登录引擎并清理网络配置…");
            SendCtrlC(_process.Id);
            if (!_process.WaitForExit(15000))
            {
                try { _process.Kill(entireProcessTree: true); } catch { }
                _process.WaitForExit(3000);
            }
            _process = null;
            SetState(EngineState.Stopped, "登录引擎已停止");
        }
        catch
        {
            // Closing must not be blocked by a failed engine cleanup.
        }
    }

    public async Task<bool> LaunchGameAsync(string path, string arguments)
    {
        string gameId = Store.Instance.Settings.EngineGameId;

        // Always prefer the engine's Game.start path when the engine is running.
        // That is where FeverBridge.activate() runs: it hosts the mpay/Fever IPC
        // and injects the platform login response so the game uses its built-in
        // login UI instead of demanding the real Fever platform.
        if (IsRunning)
        {
            try
            {
                if (await EngineApiClient.Instance.HealthAsync().ConfigureAwait(false))
                {
                    var apiResult = await EngineApiClient.Instance.StartGameAsync(gameId).ConfigureAwait(false);
                    if (apiResult.Success)
                        return true;
                    if (!string.IsNullOrWhiteSpace(apiResult.Error))
                        throw new EngineException(apiResult.Error);
                }
            }
            catch (EngineException)
            {
                throw;
            }
            catch
            {
                // Fall through to the URI listener, which also calls Game.start.
            }

            try
            {
                await SignalStartGameAsync(gameId).ConfigureAwait(false);
                return true;
            }
            catch
            {
                // Both engine launch paths failed; surface a useful error rather
                // than silently starting the game without FeverBridge.
                throw new EngineException("登录引擎未能启动游戏。请检查引擎日志和游戏路径。");
            }
        }

        EngineConfigSnapshot config;
        try
        {
            config = await EngineConfigStore.LoadAsync(gameId).ConfigureAwait(false);
        }
        catch
        {
            return LaunchGame(path, arguments);
        }

        if (config.ProxyMode == "process")
        {
            await SignalStartGameAsync(config.GameId).ConfigureAwait(false);
            return true;
        }

        return LaunchGame(path, arguments);
    }

    public bool LaunchGame(string path, string arguments)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new EngineException("游戏路径不存在，请先在设置中选择 dwrg.exe。");

        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            Arguments = arguments ?? "",
            WorkingDirectory = Path.GetDirectoryName(path) ?? "",
            UseShellExecute = true,
        };
        return Process.Start(startInfo) is not null;
    }

    public void RefreshLog()
    {
        LogTail = EngineConfigStore.ReadLogTail();
    }

    public void Poll()
    {
        try
        {
            if (_process is null) return;
            if (!_process.HasExited) return;

            _process = null;
            ProcessId = 0;
            LogTail = EngineConfigStore.ReadLogTail();
            SetState(EngineState.Stopped, "登录引擎已退出（登录后自动关闭或手动结束）");
        }
        catch
        {
            // Polling is best-effort; the UI keeps the last known state.
        }
    }

    private string ResolveRoot()
    {
        string configured = Store.Instance.Settings.EnginePath;
        if (EnginePaths.IsEngineRoot(EnginePaths.BundledRoot))
        {
            configured = EnginePaths.BundledRoot;
            Store.Instance.Settings.EnginePath = configured;
            Store.Instance.SaveSettings();
            return configured;
        }
        if (EnginePaths.IsEngineRoot(configured)) return configured;

        string detected = EnginePaths.DetectInstallRoot();
        if (detected.Length > 0)
        {
            Store.Instance.Settings.EnginePath = detected;
            Store.Instance.SaveSettings();
        }
        return detected;
    }

    private async Task SignalStartGameAsync(string gameId)
    {
        await SendUriSignalAsync("start", gameId).ConfigureAwait(false);
    }

    private async Task SendUriSignalAsync(string action, string gameId)
    {
        try
        {
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                game_id = gameId,
                action,
                installation_id = "",
                initial_view = "",
            });

            using var pipe = new NamedPipeClientStream(".", "idvlogin_uri_signal", PipeDirection.Out);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await pipe.ConnectAsync(cts.Token).ConfigureAwait(false);
            await pipe.WriteAsync(payload, cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new EngineException("登录引擎正在运行，但无法发送操作请求。", ex);
        }
    }

    private async Task StopProcessAsync(int forceAfterSeconds = 15)
    {
        if (_process is null || _process.HasExited)
        {
            _process = null;
            SetState(EngineState.Stopped, "登录引擎未启动");
            return;
        }

        SetState(EngineState.Stopping, "正在停止登录引擎并清理网络配置…");
        int pid = _process.Id;
        SendCtrlC(pid);

        bool exited = await Task.Run(() => _process.WaitForExit(forceAfterSeconds * 1000)).ConfigureAwait(false);
        if (!exited)
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
            await Task.Run(() => _process.WaitForExit(3000)).ConfigureAwait(false);
        }

        _process = null;
        ProcessId = 0;
        SetState(EngineState.Stopped, "登录引擎已停止");
    }

    private void SetState(EngineState state, string text)
    {
        State = state;
        StatusText = text;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string TrimLog(string text)
        => text.Length <= 20000 ? text : text[^20000..];

    private long GetLogLength()
    {
        try { return File.Exists(EnginePaths.LogPath) ? new FileInfo(EnginePaths.LogPath).Length : 0; }
        catch { return 0; }
    }

    private string ReadLogSince(ref long offset)
    {
        try
        {
            if (!File.Exists(EnginePaths.LogPath)) return "";
            long length = new FileInfo(EnginePaths.LogPath).Length;
            if (length < offset) offset = 0;

            using var stream = new FileStream(EnginePaths.LogPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(offset, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string text = reader.ReadToEnd();
            offset = stream.Position;
            return text;
        }
        catch
        {
            return "";
        }
    }

    private static Process CreateHiddenProcess(string fileName, string arguments, string workingDirectory)
    {
        var startup = new STARTUPINFO
        {
            cb = Marshal.SizeOf<STARTUPINFO>(),
            dwFlags = STARTF_USESHOWWINDOW,
            wShowWindow = SW_HIDE,
        };

        var command = new StringBuilder($"\"{fileName}\" {arguments}");
        bool ok = CreateProcessW(
            fileName,
            command,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            CREATE_NEW_CONSOLE,
            IntPtr.Zero,
            workingDirectory,
            ref startup,
            out PROCESS_INFORMATION info);

        if (!ok)
        {
            int error = Marshal.GetLastWin32Error();
            throw new EngineException($"无法启动登录引擎（Win32 错误 {error}）。");
        }

        try
        {
            return Process.GetProcessById(info.dwProcessId);
        }
        finally
        {
            CloseHandle(info.hThread);
            CloseHandle(info.hProcess);
        }
    }

    private static void SendCtrlC(int pid)
    {
        FreeConsole();
        if (!AttachConsole((uint)pid))
            return;

        try
        {
            SetConsoleCtrlHandler(IntPtr.Zero, true);
            GenerateConsoleCtrlEvent(CTRL_C_EVENT, 0);
            Thread.Sleep(350);
        }
        finally
        {
            FreeConsole();
            SetConsoleCtrlHandler(IntPtr.Zero, false);
        }
    }

    private const uint CREATE_NEW_CONSOLE = 0x00000010;
    private const uint STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_HIDE = 0;
    private const uint CTRL_C_EVENT = 0;

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
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessW(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
