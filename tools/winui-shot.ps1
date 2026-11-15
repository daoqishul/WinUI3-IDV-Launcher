param(
    [string]$Name = 'shot',
    [int]$ClickX = -1,
    [int]$ClickY = -1,
    [int]$Delay = 900,
    [string]$Out = 'shots\winui'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $root $Out
New-Item -ItemType Directory -Force $outDir | Out-Null

Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class WinUiShot {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, UIntPtr e);

    public static IntPtr FindMain(uint pid)
    {
        IntPtr best = IntPtr.Zero;
        long bestArea = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l)
        {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if (p != pid || !IsWindowVisible(h)) return true;
            var cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            if (cls.ToString() != "WinUIDesktopWin32WindowClass") return true;
            RECT r;
            GetWindowRect(h, out r);
            long area = (long)(r.R - r.L) * (r.B - r.T);
            if (area > bestArea) { best = h; bestArea = area; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
}
'@

$p = Get-Process IdvLauncher -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { throw 'IdvLauncher is not running' }
$h = [WinUiShot]::FindMain([uint32]$p.Id)
if ($h -eq [IntPtr]::Zero) { throw 'main window not found' }

[WinUiShot]::ShowWindow($h, 9) | Out-Null
Start-Sleep -Milliseconds 500
[WinUiShot]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 3) | Out-Null
[WinUiShot]::SetForegroundWindow($h) | Out-Null
Start-Sleep -Milliseconds 550

$cr = New-Object WinUiShot+RECT
[WinUiShot]::GetClientRect($h, [ref]$cr) | Out-Null
$origin = New-Object WinUiShot+POINT
[WinUiShot]::ClientToScreen($h, [ref]$origin) | Out-Null

if ($ClickX -ge 0 -and $ClickY -ge 0) {
    [WinUiShot]::SetCursorPos($origin.X + $ClickX, $origin.Y + $ClickY) | Out-Null
    Start-Sleep -Milliseconds 180
    [WinUiShot]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [WinUiShot]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds $Delay
}

[WinUiShot]::SetCursorPos(10, 10) | Out-Null
Start-Sleep -Milliseconds 250

$w = $cr.R - $cr.L
$hh = $cr.B - $cr.T
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($origin.X, $origin.Y, 0, 0, (New-Object System.Drawing.Size($w, $hh)))
$g.Dispose()
$file = Join-Path $outDir ("{0}.png" -f $Name)
$bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

"captured $file ({0}x{1}) clientOrigin={2},{3}" -f $w, $hh, $origin.X, $origin.Y
