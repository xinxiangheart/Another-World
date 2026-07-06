#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

public static class WindowsFileDialog
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct OpenFileName
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
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
    static extern bool GetOpenFileName(ref OpenFileName ofn);

    public static string Open(string title, string extensions)
    {
        string filter = BuildFilter(extensions);
        IntPtr filePtr = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
        Marshal.WriteByte(filePtr, 0);

        OpenFileName ofn = new OpenFileName();
        ofn.lStructSize = Marshal.SizeOf(ofn);
        ofn.lpstrFilter = filter;
        ofn.lpstrFile = filePtr;
        ofn.nMaxFile = 256;
        ofn.lpstrTitle = title;

        if (GetOpenFileName(ref ofn))
        {
            string path = Marshal.PtrToStringAuto(ofn.lpstrFile);
            Marshal.FreeHGlobal(filePtr);
            return path;
        }
        Marshal.FreeHGlobal(filePtr);
        return "";
    }

    static string BuildFilter(string extensions)
    {
        // "png,jpg,jpeg" -> "Images\0*.png;*.jpg;*.jpeg\0"
        string[] parts = extensions.Split(',');
        string pattern = "";
        foreach (var p in parts) pattern += $"*.{p.Trim()};";
        pattern = pattern.TrimEnd(';');
        return $"Images\0{pattern}\0";
    }
}
#endif
