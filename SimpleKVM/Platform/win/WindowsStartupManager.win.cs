using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// Run-at-startup via a shortcut in the user's Startup folder, written through the
    /// IShellLink COM interface. The interop is source-generated ([GeneratedComInterface] +
    /// ComWrappers) rather than built-in [ComImport]: the built-in path relies on runtime
    /// marshalling metadata that trimming strips, which made Save silently write nothing in the
    /// published exe. The generated stubs are compiled in, so they survive trimming and AOT.
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public partial class WindowsStartupManager : IStartupManager
    {
        static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        static readonly string ShortcutPath = Path.Combine(StartupFolder, "SimpleKVM.lnk");

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        const int MaxPath = 260;

        public bool IsEnabled()
        {
            if (!File.Exists(ShortcutPath)) return false;

            try
            {
                var link = CreateShellLink();
                ((IPersistFile)link).Load(ShortcutPath, 0);

                if (!string.Equals(GetPath(link), ExecutablePath, StringComparison.OrdinalIgnoreCase)) return false;

                //Shortcuts made before the start-minimized argument existed relied on a
                //window-style hint the app no longer honours; upgrade them in place
                if (!GetArguments(link).Contains(Program.StartMinimizedArg))
                {
                    link.SetArguments(Program.StartMinimizedArg);
                    ((IPersistFile)link).Save(ShortcutPath, true);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                CreateStartupShortcut();
            }
            else if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }
        }

        static void CreateStartupShortcut()
        {
            var link = CreateShellLink();
            link.SetPath(ExecutablePath);
            link.SetArguments(Program.StartMinimizedArg);
            link.SetWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ((IPersistFile)link).Save(ShortcutPath, true);
        }

        static readonly Guid CLSID_ShellLink = new("00021401-0000-0000-C000-000000000046");
        static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");
        static readonly StrategyBasedComWrappers comWrappers = new();

        static IShellLinkW CreateShellLink()
        {
            int hr = CoCreateInstance(in CLSID_ShellLink, IntPtr.Zero, CLSCTX_INPROC_SERVER, in IID_IUnknown, out IntPtr pUnknown);
            Marshal.ThrowExceptionForHR(hr);

            try
            {
                return (IShellLinkW)comWrappers.GetOrCreateObjectForComInstance(pUnknown, CreateObjectFlags.None);
            }
            finally
            {
                Marshal.Release(pUnknown);  //the wrapper holds its own reference
            }
        }

        static unsafe string GetPath(IShellLinkW link)
        {
            var buffer = stackalloc char[MaxPath];
            link.GetPath(buffer, MaxPath, IntPtr.Zero, 0);
            return new string(buffer);
        }

        static unsafe string GetArguments(IShellLinkW link)
        {
            var buffer = stackalloc char[MaxPath * 4];
            link.GetArguments(buffer, MaxPath * 4);
            return new string(buffer);
        }

        const uint CLSCTX_INPROC_SERVER = 1;

        [DllImport("ole32.dll")]
        static extern int CoCreateInstance(in Guid rclsid, IntPtr pUnkOuter, uint dwClsContext, in Guid riid, out IntPtr ppv);

        [GeneratedComInterface]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        internal partial interface IShellLinkW
        {
            unsafe void GetPath(char* pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            unsafe void GetDescription(char* pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            unsafe void GetWorkingDirectory(char* pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            unsafe void GetArguments(char* pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            unsafe void GetIconLocation(char* pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        //IPersistFile derives from IPersist, so GetClassID comes first in the vtable
        [GeneratedComInterface]
        [Guid("0000010b-0000-0000-C000-000000000046")]
        internal partial interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile(out IntPtr ppszFileName);
        }
    }
}
