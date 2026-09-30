using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// Runs a desktop tool (gdbus, kscreen-doctor, xrandr) and captures what it prints. Both
    /// output streams are drained concurrently, so a chatty tool can't fill a pipe and hang,
    /// and a tool that doesn't return by the timeout is killed. Arguments are always
    /// constants chosen by the caller; nothing user-supplied is ever passed through.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static class ExternalTool
    {
        public sealed record Result(int ExitCode, string StandardOutput, string StandardError)
        {
            public bool Succeeded => ExitCode == 0;
        }

        /// <summary>Null when the program isn't installed or couldn't be started.</summary>
        public static Result? Run(string fileName, string arguments, int timeoutMs = 5000, IReadOnlyDictionary<string, string>? environment = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                if (environment != null)
                {
                    foreach (var (key, value) in environment) startInfo.Environment[key] = value;
                }

                using var process = Process.Start(startInfo);
                if (process == null) return null;

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return null;
                }

                return new Result(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
            }
            catch
            {
                return null;    //not installed, or not runnable here
            }
        }
    }
}
