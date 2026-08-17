using System;
using System.IO;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.mac
{
    /// <summary>
    /// Run-at-startup via a LaunchAgent plist. Writing the file is enough for the next
    /// login; no launchctl interaction is needed (or attempted).
    /// </summary>
    [SupportedOSPlatform("macos")]
    public class MacStartupManager : IStartupManager
    {
        const string Label = "com.fiddyschmitt.simplekvm";

        static string PlistPath
        {
            get
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "LaunchAgents", $"{Label}.plist");
            }
        }

        static string ExecutablePath => Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine the executable path");

        public bool IsEnabled()
        {
            try
            {
                return File.Exists(PlistPath) && File.ReadAllText(PlistPath).Contains($"<string>{ExecutablePath}</string>");
            }
            catch
            {
                return false;
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (!enabled)
            {
                if (File.Exists(PlistPath)) File.Delete(PlistPath);
                return;
            }

            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                	<key>Label</key>
                	<string>{Label}</string>
                	<key>ProgramArguments</key>
                	<array>
                		<string>{ExecutablePath}</string>
                		<string>{Program.StartMinimizedArg}</string>
                	</array>
                	<key>RunAtLoad</key>
                	<true/>
                </dict>
                </plist>

                """;

            Directory.CreateDirectory(Path.GetDirectoryName(PlistPath)!);
            File.WriteAllText(PlistPath, plist);
        }
    }
}
