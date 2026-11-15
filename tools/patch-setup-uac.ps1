param(
    [Parameter(Mandatory = $true)][string]$Setup
)

Add-Type @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class SetupManifestPatcher
{
    private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
    private const int RT_MANIFEST = 24;
    private const int IDR_MANIFEST = 1;

    private delegate bool EnumResLangProc(IntPtr hModule, IntPtr lpType, IntPtr lpName, ushort wLanguage, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LockResource(IntPtr hResData);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumResourceLanguages(IntPtr hModule, IntPtr lpType, IntPtr lpName, EnumResLangProc lpEnumFunc, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BeginUpdateResource(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateResource(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResource(IntPtr hUpdate, bool fDiscard);

    [DllImport("kernel32.dll")]
    private static extern bool FreeLibrary(IntPtr hModule);

    public static string Patch(string path)
    {
        IntPtr module = LoadLibraryEx(path, IntPtr.Zero, LOAD_LIBRARY_AS_DATAFILE);
        if (module == IntPtr.Zero) throw new IOException("LoadLibraryEx failed: " + Marshal.GetLastWin32Error());

        var languages = new List<ushort>();
        string xml;
        try
        {
            IntPtr res = FindResource(module, (IntPtr)IDR_MANIFEST, (IntPtr)RT_MANIFEST);
            if (res == IntPtr.Zero) throw new IOException("Manifest resource not found");

            uint size = SizeofResource(module, res);
            IntPtr data = LockResource(LoadResource(module, res));
            var bytes = new byte[size];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            xml = Encoding.UTF8.GetString(bytes);
            if (!xml.Contains("requestedExecutionLevel"))
                throw new IOException("Manifest does not contain requestedExecutionLevel");

            EnumResourceLanguages(module, (IntPtr)RT_MANIFEST, (IntPtr)IDR_MANIFEST,
                (h, t, n, lang, l) => { languages.Add(lang); return true; }, IntPtr.Zero);
            if (languages.Count == 0) languages.Add(0);
        }
        finally
        {
            FreeLibrary(module);
        }

        xml = xml.Replace("level=\"asInvoker\"", "level=\"requireAdministrator\"");
        xml = xml.Replace("level=\"highestAvailable\"", "level=\"requireAdministrator\"");
        if (!xml.Contains("level=\"requireAdministrator\""))
            throw new IOException("Could not set requireAdministrator");

        IntPtr update = BeginUpdateResource(path, false);
        if (update == IntPtr.Zero) throw new IOException("BeginUpdateResource failed: " + Marshal.GetLastWin32Error());
        try
        {
            byte[] newBytes = new UTF8Encoding(false).GetBytes(xml);
            foreach (ushort language in languages)
            {
                if (!UpdateResource(update, (IntPtr)RT_MANIFEST, (IntPtr)IDR_MANIFEST, language, newBytes, (uint)newBytes.Length))
                    throw new IOException("UpdateResource failed: " + Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (!EndUpdateResource(update, false))
                throw new IOException("EndUpdateResource failed: " + Marshal.GetLastWin32Error());
        }

        return "patched languages=" + string.Join(",", languages);
    }
}
'@

$result = [SetupManifestPatcher]::Patch($Setup)
Write-Output $result
