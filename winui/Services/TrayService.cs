using System.Runtime.InteropServices;

namespace IdvLauncher.Services;

public sealed class TrayService : IDisposable
{
    private const int WM_APP = 0x8000;
    private const int WM_NULL = 0x0000;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_CONTEXTMENU = 0x007B;
    private const int CallbackMessage = WM_APP + 0x51;

    private const uint NIM_ADD = 0;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;
    private const uint MF_STRING = 0x0000;
    private const uint TPM_RETURNCMD = 0x0100;
    private const uint TPM_RIGHTBUTTON = 0x0002;

    private readonly IntPtr _hwnd;
    private readonly IntPtr _icon;
    private readonly IntPtr _menu;
    private readonly SubclassProc _subclassProc;
    private bool _added;
    private bool _disposed;

    public event EventHandler? ShowRequested;
    public event EventHandler? ExitRequested;

    public bool IsVisible => _added && !_disposed;

    public void ShowBalloon()
    {
        if (_disposed) return;
        try
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1,
                uFlags = 0x10, // NIF_INFO
                szTip = "",
                szInfo = "启动器已隐藏到小托盘，双击图标可重新打开。",
                szInfoTitle = "IDV 启动器",
                dwInfoFlags = 1, // NIIF_INFO
            };
            Shell_NotifyIcon(1, ref data); // NIM_MODIFY
        }
        catch { }
    }

    public TrayService(IntPtr hwnd)
    {
        _hwnd = hwnd;
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "777.ico");
        _icon = LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);
        if (_icon == IntPtr.Zero)
            _icon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION

        _menu = CreatePopupMenu();
        AppendMenuW(_menu, MF_STRING, 1, "显示 IDV 启动器");
        AppendMenuW(_menu, MF_STRING, 2, "退出");

        _subclassProc = WndProc;
        SetWindowSubclass(_hwnd, _subclassProc, (UIntPtr)1, UIntPtr.Zero);

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
            szTip = "IDV 启动器",
            szInfo = "",
            szInfoTitle = "",
        };
        Shell_NotifyIcon(NIM_ADD, ref data);
        _added = true;
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, UIntPtr dwRefData)
    {
        if (msg == CallbackMessage)
        {
            int evt = (int)(lParam.ToInt64() & 0xFFFF);
            if (evt is WM_LBUTTONDBLCLK or WM_CONTEXTMENU or WM_RBUTTONUP)
            {
                if (evt == WM_LBUTTONDBLCLK)
                {
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ShowMenu();
                }
                return IntPtr.Zero;
            }
        }
        return DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        GetCursorPos(out POINT point);
        SetForegroundWindow(_hwnd);
        int command = TrackPopupMenu(_menu, TPM_RETURNCMD | TPM_RIGHTBUTTON,
            point.X, point.Y, 0, _hwnd, IntPtr.Zero);
        PostMessage(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        if (command == 1) ShowRequested?.Invoke(this, EventArgs.Empty);
        else if (command == 2) ExitRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_added)
            {
                var data = new NOTIFYICONDATA
                {
                    cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = 1,
                    szTip = "",
                    szInfo = "",
                    szInfoTitle = "",
                };
                Shell_NotifyIcon(NIM_DELETE, ref data);
            }
            RemoveWindowSubclass(_hwnd, _subclassProc, (UIntPtr)1);
            DestroyMenu(_menu);
            DestroyIcon(_icon);
        }
        catch { }
    }

    private delegate IntPtr SubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, UIntPtr dwRefData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("comctl32.dll")]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SubclassProc callback,
        UIntPtr id, UIntPtr data);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hWnd, SubclassProc callback, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type,
        int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, uint id, string text);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr menu, uint flags, int x, int y,
        int reserved, IntPtr hwnd, IntPtr rect);
}
