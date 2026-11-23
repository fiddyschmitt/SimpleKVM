using SimpleKVM.Platform;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// The rule editor's Fix for "No access to /dev/i2c-*" and "No I2C buses": BusAccess's script,
    /// run as root through pkexec, which shows the desktop's own password dialog. Once the script
    /// has run, the display adapters' buses must really open, or the user is told why they don't.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class I2cAccessFix : IStatusFix
    {
        public static I2cAccessFix Instance { get; } = new();

        /// <summary>systemd's rules that turn the uaccess tag into access for the active session's user.</summary>
        static readonly string[] UaccessRules = ["/usr/lib/udev/rules.d/73-seat-late.rules", "/lib/udev/rules.d/73-seat-late.rules"];

        I2cAccessFix()
        {
        }

        public string Description =>
            "Asks for an administrator's password, then lets whoever is logged in at this computer control its monitors. "
            + $"It adds the udev rule the ddcutil package uses, {BusAccess.RuleFile}, and loads the i2c-dev module where nothing does yet.";

        /// <summary>
        /// Whether the fix can help on this computer: a display adapter has an I2C bus for the rule
        /// to open up (a virtual machine's display has none), systemd's rules are there to act on
        /// the rule's uaccess tag (distributions without systemd don't have them), and pkexec is
        /// there to ask for the password.
        /// </summary>
        public static bool CanHelp()
        {
            return DisplaySystem.DisplayAdapterBuses().Count > 0
                && UaccessRules.Any(File.Exists)
                && Pkexec() != null;
        }

        public async Task<StatusFixResult> RunAsync()
        {
            if (Pkexec() is not string pkexec)
                return new(StatusFixOutcome.Failed, "pkexec isn't installed, so Simple KVM can't ask for the password.");

            int exitCode;
            string standardError;
            try
            {
                var startInfo = new ProcessStartInfo(pkexec)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                startInfo.ArgumentList.Add("/bin/sh");
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add(BusAccess.FixScript);

                using var process = Process.Start(startInfo);
                if (process == null) return new(StatusFixOutcome.Failed, "pkexec didn't start.");

                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();

                //For as long as the password dialog is open
                await process.WaitForExitAsync().ConfigureAwait(false);
                await output.ConfigureAwait(false);
                standardError = await errors.ConfigureAwait(false);
                exitCode = process.ExitCode;
            }
            catch (Exception ex)
            {
                return new(StatusFixOutcome.Failed, $"Couldn't run pkexec: {ex.Message}");
            }

            if (BusAccess.Interpret(exitCode, standardError) is StatusFixResult ended) return ended;

            //The access goes to whoever is logged in at the computer's own screen, so a session
            //elsewhere (a remote one, or one switched away from) still has none
            return DisplaySystem.DisplayAdapterBuses().Any(DdcTransport.CanOpen)
                    ? new(StatusFixOutcome.Fixed)
                    : new(StatusFixOutcome.Failed, "The access is set up, but it goes to whoever is logged in at this computer's own screen, "
                                                   + "and this session still can't open the monitors' buses. The README's Linux notes have other ways.");
        }

        static string? Pkexec()
        {
            return (Environment.GetEnvironmentVariable("PATH") ?? "")
                    .Split(':', StringSplitOptions.RemoveEmptyEntries)
                    .Append("/usr/bin")
                    .Select(dir => Path.Combine(dir, "pkexec"))
                    .FirstOrDefault(File.Exists);
        }
    }
}
