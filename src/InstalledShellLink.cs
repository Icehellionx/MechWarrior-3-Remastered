using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// Typed vtable calls avoid the dynamic WScript.Shell binder failure reported on Wine.
// Shell Link persistence is the same Windows contract for launcher and manual links.
internal static class InstalledShellLink
{
    internal static void Create(string path, string target, string workingDirectory, string description, string icon)
    {
        object instance = new ShellLink();
        try
        {
            IShellLinkW link = (IShellLinkW)instance;
            link.SetPath(target);
            link.SetArguments(String.Empty);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription(description);
            if (icon != null) link.SetIconLocation(icon, 0);
            ((IPersistFile)instance).Save(Path.GetFullPath(path), true);
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    internal static string[] Read(string path)
    {
        object instance = new ShellLink();
        try
        {
            ((IPersistFile)instance).Load(Path.GetFullPath(path), 0);
            IShellLinkW link = (IShellLinkW)instance;
            StringBuilder target = new StringBuilder(32768);
            StringBuilder arguments = new StringBuilder(32768);
            StringBuilder working = new StringBuilder(32768);
            StringBuilder icon = new StringBuilder(32768);
            int index;
            link.GetPath(target, target.Capacity, IntPtr.Zero, 4); // SLGP_RAWPATH; never resolve or execute.
            link.GetArguments(arguments, arguments.Capacity);
            link.GetWorkingDirectory(working, working.Capacity);
            link.GetIconLocation(icon, icon.Capacity, out index);
            return new string[] { target.ToString(), arguments.ToString(), working.ToString(), icon + "," + index };
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    // Order follows IShellLinkW's native vtable, including unused methods.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int maximum, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int maximum);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maximum);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maximum);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int maximum, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
