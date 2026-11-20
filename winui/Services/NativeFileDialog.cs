using System.Runtime.InteropServices;
using System.Text;

namespace IdvLauncher.Services;

public static class NativeFileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OPENFILENAME
    {
        public int lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public string lpstrFilter;
        public string lpstrCustomFilter;
        public int nMaxCustFilter;
        public int nFilterIndex;
        public IntPtr lpstrFile;
        public int nMaxFile;
        public IntPtr lpstrFileTitle;
        public int nMaxFileTitle;
        public string lpstrInitialDir;
        public string lpstrTitle;
        public int Flags;
        public short nFileOffset;
        public short nFileExtension;
        public string lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public string lpTemplateName;
        public IntPtr pvReserved;
        public int dwReserved;
        public int FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

    private const int OFN_FILEMUSTEXIST = 0x00001000;
    private const int OFN_PATHMUSTEXIST = 0x00000800;
    private const int OFN_NOCHANGEDIR = 0x00000008;
    private const int BufferChars = 2048;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BROWSEINFO
    {
        public IntPtr hwndOwner;
        public IntPtr pidlRoot;
        public IntPtr pszDisplayName;
        public string lpszTitle;
        public uint ulFlags;
        public IntPtr lpfn;
        public IntPtr lParam;
        public int iImage;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHBrowseForFolderW(ref BROWSEINFO bi);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SHGetPathFromIDListW(IntPtr pidl, StringBuilder path);

    [DllImport("ole32.dll")]
    private static extern void CoTaskMemFree(IntPtr pv);

    private const uint BIF_RETURNONLYFSDIRS = 0x0001;
    private const uint BIF_NEWDIALOGSTYLE = 0x0040;

    public static string? PickFolder(IntPtr owner)
    {
        IntPtr displayName = Marshal.AllocHGlobal(BufferChars * sizeof(char));
        try
        {
            for (int i = 0; i < BufferChars; i++)
                Marshal.WriteInt16(displayName, i * sizeof(char), 0);

            var info = new BROWSEINFO
            {
                hwndOwner = owner,
                pszDisplayName = displayName,
                lpszTitle = "选择游戏目录或 Documents 目录",
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
            };
            IntPtr pidl = SHBrowseForFolderW(ref info);
            if (pidl == IntPtr.Zero) return null;
            try
            {
                var path = new StringBuilder(BufferChars);
                return SHGetPathFromIDListW(pidl, path) ? path.ToString() : null;
            }
            finally
            {
                CoTaskMemFree(pidl);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(displayName);
        }
    }

    public static string? PickJson(IntPtr owner) =>
        PickFile(owner, "选择 pc_key_setting_data.json",
            "按键配置 (*.json)\0*.json\0所有文件 (*.*)\0*.*\0\0", "json");

    public static string? PickExecutable(IntPtr owner) =>
        PickFile(owner, "选择第五人格 dwrg.exe",
            "游戏程序 (*.exe)\0*.exe\0所有文件 (*.*)\0*.*\0\0", "exe");

    private static string? PickFile(IntPtr owner, string title, string filter, string extension)
    {
        IntPtr buffer = Marshal.AllocHGlobal(BufferChars * sizeof(char));
        try
        {
            for (int i = 0; i < BufferChars; i++)
                Marshal.WriteInt16(buffer, i * sizeof(char), 0);

            var dialog = new OPENFILENAME
            {
                lStructSize = Marshal.SizeOf<OPENFILENAME>(),
                hwndOwner = owner,
                lpstrFilter = filter,
                nFilterIndex = 1,
                lpstrFile = buffer,
                nMaxFile = BufferChars,
                lpstrTitle = title,
                Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                lpstrDefExt = extension,
            };

            return GetOpenFileNameW(ref dialog)
                ? Marshal.PtrToStringUni(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
