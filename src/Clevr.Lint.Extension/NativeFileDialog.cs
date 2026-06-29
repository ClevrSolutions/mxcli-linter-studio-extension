using System.Runtime.InteropServices;

namespace Clevr.Lint.Extension;

/// <summary>
/// Minimal Win32 file-open dialog via P/Invoke (comdlg32.dll → GetOpenFileNameW).
/// No WinForms / WPF dependency required.
/// Must be called on an STA thread (MessageReceived always runs on the UI thread — fine).
/// </summary>
internal static class NativeFileDialog
{
    // ── Win32 declarations ────────────────────────────────────────────────────

    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOpenFileNameW(ref OPENFILENAMEW lpofn);

    // All pointer fields use IntPtr so the struct packs correctly on both 32-bit and 64-bit.
    [StructLayout(LayoutKind.Sequential)]
    private struct OPENFILENAMEW
    {
        public int    lStructSize;
        public IntPtr hwndOwner;
        public IntPtr hInstance;
        public IntPtr lpstrFilter;
        public IntPtr lpstrCustomFilter;
        public int    nMaxCustFilter;
        public int    nFilterIndex;
        public IntPtr lpstrFile;       // output: path written here by the dialog
        public int    nMaxFile;
        public IntPtr lpstrFileTitle;
        public int    nMaxFileTitle;
        public IntPtr lpstrInitialDir;
        public IntPtr lpstrTitle;
        public int    Flags;
        public short  nFileOffset;
        public short  nFileExtension;
        public IntPtr lpstrDefExt;
        public IntPtr lCustData;
        public IntPtr lpfnHook;
        public IntPtr lpTemplateName;
        public IntPtr pvReserved;
        public int    dwReserved;
        public int    FlagsEx;
    }

    private const int OFN_HIDEREADONLY  = 0x0004;
    private const int OFN_NOCHANGEDIR   = 0x0008;
    private const int OFN_PATHMUSTEXIST = 0x0800;
    private const int OFN_FILEMUSTEXIST = 0x1000;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Shows a native "Open File" dialog filtered to .exe files.
    /// Returns the selected full path, or null if the user cancelled.
    /// </summary>
    public static string? ShowExePicker(string title, string? initialPath)
    {
        const int MaxPath = 1024;

        // Allocate all strings as unmanaged Unicode so we can pass pointers into the struct.
        var pFile   = Marshal.AllocHGlobal((MaxPath + 1) * 2);  // writable output buffer
        var pFilter = AllocFilter("Executables (*.exe)", "*.exe", "All files", "*.*");
        var pTitle  = Marshal.StringToHGlobalUni(title);

        string? initialDir = null;
        if (!string.IsNullOrWhiteSpace(initialPath))
            initialDir = File.Exists(initialPath) ? Path.GetDirectoryName(initialPath) : initialPath;
        var pInitDir = initialDir != null ? Marshal.StringToHGlobalUni(initialDir) : IntPtr.Zero;

        // Pre-fill with the current path so the dialog opens there if the user cancels.
        if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes(initialPath + '\0');
            Marshal.Copy(bytes, 0, pFile, Math.Min(bytes.Length, MaxPath * 2));
        }
        else
        {
            Marshal.WriteInt16(pFile, 0);
        }

        try
        {
            var ofn = new OPENFILENAMEW
            {
                lStructSize   = Marshal.SizeOf<OPENFILENAMEW>(),
                lpstrFilter   = pFilter,
                nFilterIndex  = 1,
                lpstrFile     = pFile,
                nMaxFile      = MaxPath,
                lpstrInitialDir = pInitDir,
                lpstrTitle    = pTitle,
                Flags         = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR,
            };

            return GetOpenFileNameW(ref ofn) ? Marshal.PtrToStringUni(pFile) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(pFile);
            Marshal.FreeHGlobal(pFilter);
            Marshal.FreeHGlobal(pTitle);
            if (pInitDir != IntPtr.Zero) Marshal.FreeHGlobal(pInitDir);
        }
    }

    // Builds a double-null-terminated filter string: "Display\0Pattern\0Display\0Pattern\0\0"
    private static IntPtr AllocFilter(params string[] pairs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i + 1 < pairs.Length; i += 2)
            sb.Append(pairs[i]).Append('\0').Append(pairs[i + 1]).Append('\0');
        sb.Append('\0');
        return Marshal.StringToHGlobalUni(sb.ToString());
    }
}
