using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

/*
+=====================================
 File   : CS_MayaPreviewFileDialog
 Summary: The Windows "open file" dialog for texture slots (folder button / thumbnail).
          It runs on its own STA thread, so the preview and the link to Maya keep running;
          the answer is handed over on the main thread by Tick().
 History: 2026/11/22 New
=====================================+
*/
public static class CS_MayaPreviewFileDialog
{
    private static volatile bool s_done;
    private static string s_result;
    private static Action<string> s_chosen;
    private static string s_lastFolder;

    public static bool IsOpen { get; private set; }

    // current: the file in the slot now (its folder is shown first). chosen is not called on cancel.
    public static void OpenImage(string title, string current, Action<string> chosen)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (IsOpen) return;
        string folder = FolderOf(current) ?? s_lastFolder ?? "";
        IntPtr owner = GetActiveWindow();
        s_chosen = chosen;
        s_result = null;
        s_done = false;
        IsOpen = true;
        var thread = new Thread(() => Show(owner, title, folder)) { IsBackground = true, Name = "MayaPreview file dialog" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
#endif
    }

    // Every frame.
    public static void Tick()
    {
        if (!IsOpen || !s_done) return;
        IsOpen = false;
        string result = s_result;
        Action<string> chosen = s_chosen;
        s_chosen = null;
        if (string.IsNullOrEmpty(result)) return;
        s_lastFolder = FolderOf(result);
        chosen?.Invoke(result.Replace('\\', '/'));
        CS_MayaPreviewFrameCache.MarkDirty();
    }

    private static string FolderOf(string file)
    {
        if (string.IsNullOrEmpty(file)) return null;
        try
        {
            string folder = Path.GetDirectoryName(Path.GetFullPath(file));
            return Directory.Exists(folder) ? folder : null;
        }
        catch (Exception) { return null; }   // not a usable path
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private const int MaxPathChars = 4096;
    private const int OFN_HIDEREADONLY = 0x4;
    private const int OFN_NOCHANGEDIR = 0x8;        // the player's working folder must not move
    private const int OFN_PATHMUSTEXIST = 0x800;
    private const int OFN_FILEMUSTEXIST = 0x1000;
    private const int OFN_EXPLORER = 0x80000;
    private const uint COINIT_APARTMENTTHREADED = 0x2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int structSize;
        public IntPtr owner;
        public IntPtr instance;
        public IntPtr filter;               // has embedded '\0': cannot be marshalled as a string
        public IntPtr customFilter;
        public int maxCustomFilter;
        public int filterIndex;
        public IntPtr file;
        public int maxFile;
        public IntPtr fileTitle;
        public int maxFileTitle;
        [MarshalAs(UnmanagedType.LPWStr)] public string initialDir;
        [MarshalAs(UnmanagedType.LPWStr)] public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public IntPtr defaultExtension;
        public IntPtr customData;
        public IntPtr hook;
        public IntPtr templateName;
        public IntPtr reservedPointer;
        public int reserved;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetOpenFileNameW")]
    private static extern bool GetOpenFileName(ref OpenFileName data);
    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint model);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();

    private static void Show(IntPtr owner, string title, string folder)
    {
        IntPtr filter = IntPtr.Zero, buffer = IntPtr.Zero;
        bool initialized = false;
        try
        {
            initialized = CoInitializeEx(IntPtr.Zero, COINIT_APARTMENTTHREADED) >= 0;
            filter = Marshal.StringToHGlobalUni("画像 (png / jpg)\0*.png;*.jpg;*.jpeg\0\0");
            buffer = Marshal.AllocHGlobal(MaxPathChars * 2);
            Marshal.WriteInt16(buffer, (short)0);
            var data = new OpenFileName
            {
                owner = owner,
                filter = filter,
                filterIndex = 1,
                file = buffer,
                maxFile = MaxPathChars,
                initialDir = folder.Length > 0 ? folder.Replace('/', '\\') : null,
                title = title,
                flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY | OFN_NOCHANGEDIR
            };
            data.structSize = Marshal.SizeOf(typeof(OpenFileName));
            if (GetOpenFileName(ref data)) s_result = Marshal.PtrToStringUni(buffer);
        }
        catch (Exception exception) { Debug.LogException(exception); }
        finally
        {
            if (filter != IntPtr.Zero) Marshal.FreeHGlobal(filter);
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            if (initialized) CoUninitialize();
            s_done = true;
        }
    }
#endif
}
