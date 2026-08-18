using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.win
{
    /// <summary>
    /// Run-at-startup via a shortcut in the user's Startup folder, written through the
    /// IShellLink COM interface (typed interop, so it survives trimming — the previous
    /// late-bound WScript.Shell approach depended on the dynamic runtime binder).
    /// </summary>
    [SupportedOSPlatform("windows6.1")]
    public class WindowsStartupManager : IStartupManager
    {
        static readonly string StartupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        static readonly string ShortcutPath = Path.Combine(StartupFolder, "SimpleKVM.lnk");

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        public bool IsEnabled()
        {
            if (!File.Exists(ShortcutPath)) return false;

            try
            {
                var link = (IShellLinkW)new ShellLink();
                ((IPersistFile)link).Load(ShortcutPath, 0);

                var target = new StringBuilder(260);
                link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
                if (!string.Equals(target.ToString(), ExecutablePath, StringComparison.OrdinalIgnoreCase)) return false;

                //Shortcuts made before the start-minimized argument existed relied on a
                //window-style hint the app no longer honours; upgrade them in place
                var arguments = new StringBuilder(1024);
                link.GetArguments(arguments, arguments.Capacity);
                if (!arguments.ToString().Contains(Program.StartMinimizedArg))
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
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(ExecutablePath);
            link.SetArguments(Program.StartMinimizedArg);
            link.SetWorkingDirectory(AppDomain.CurrentDomain.BaseDirectory);
            ((IPersistFile)link).Save(ShortcutPath, true);
        }

        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        class ShellLink
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out ushort pwHotkey);
            void SetHotkey(ushort wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }
    }
}
