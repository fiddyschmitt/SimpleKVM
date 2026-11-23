using SimpleKVM.Platform;
using System;
using System.Linq;

namespace SimpleKVM.Displays.linux
{
    /// <summary>
    /// The Fix for monitors the app can't reach on Linux, where /dev/i2c-* belong to root. A udev
    /// rule, the one the ddcutil package installs, tags the I2C buses of display adapters with
    /// uaccess, which systemd turns into access for whoever is logged in at the computer's own
    /// screen: at once, without logging out, and not for remote sessions. Where the i2c-dev module,
    /// which makes the /dev/i2c-* nodes in the first place, is neither built into the kernel nor
    /// loaded at boot by something else, it is loaded now and at every boot. That takes root once,
    /// which I2cAccessFix asks for through pkexec. The parts here are pure, so they can be tested
    /// on any OS.
    /// </summary>
    public static class BusAccess
    {
        public const string RuleFile = "/etc/udev/rules.d/60-simplekvm-i2c.rules";
        public const string ModuleFile = "/etc/modules-load.d/simplekvm-i2c-dev.conf";

        /// <summary>
        /// The buses whose parent is a PCI display controller (class 0x03xxxx), the same ones
        /// DisplaySystem.BelongsToDisplayAdapter picks. The SMBus, where the memory modules answer
        /// at the address a monitor's EDID has, stays root's.
        /// </summary>
        public const string Rule = "SUBSYSTEM==\"i2c-dev\", KERNEL==\"i2c-[0-9]*\", ATTRS{class}==\"0x03*\", TAG+=\"uaccess\"";

        /// <summary>
        /// Run as root by "/bin/sh -c". The rule is in place and udev has reloaded before the
        /// module can make new nodes, so those get it as they appear; a change event applies it
        /// to the nodes that exist already, and settle waits until it has. A step that fails ends
        /// the script with exit code 1 and a sentence on stderr, which keeps pkexec's own 126 and
        /// 127 unambiguous. No "set -e": the shell ignores it inside functions and || lists.
        /// The line endings are made Unix ones whatever this file has, or sh would read a CR
        /// into every command.
        /// </summary>
        public static readonly string FixScript = $$"""
            fail() { echo "$*" >&2; exit 1; }
            umask 022
            mkdir -p /etc/udev/rules.d || fail "Could not create /etc/udev/rules.d."
            printf '%s\n' \
              '# Written by Simple KVM. Lets whoever is logged in at this computer control its monitors' \
              '# over DDC/CI, on the I2C buses of its graphics cards: the rule the ddcutil package installs.' \
              '# Delete this file to take the access away again.' \
              '{{Rule}}' > {{RuleFile}}.new && mv -f {{RuleFile}}.new {{RuleFile}} || fail "Could not write {{RuleFile}}."
            udevadm control --reload || fail "Could not reload the udev rules."
            if ! grep -qs '/i2c-dev[.]ko' "/lib/modules/$(uname -r)/modules.builtin"; then
              if ! grep -qsx 'i2c[-_]dev' /etc/modules-load.d/*.conf /run/modules-load.d/*.conf /usr/local/lib/modules-load.d/*.conf /usr/lib/modules-load.d/*.conf /lib/modules-load.d/*.conf; then
                mkdir -p /etc/modules-load.d &&
                  printf '%s\n' '# Written by Simple KVM: loads i2c-dev, which makes the /dev/i2c-* buses it controls monitors through.' i2c-dev > {{ModuleFile}} ||
                  fail "Could not write {{ModuleFile}}."
              fi
              modprobe i2c-dev || fail "Could not load the i2c-dev kernel module."
            fi
            udevadm trigger --subsystem-match=i2c-dev --action=change || fail "Could not apply the udev rule."
            udevadm settle --timeout=10
            exit 0
            """.ReplaceLineEndings("\n");

        /// <summary>
        /// What pkexec's exit code says: null when the script ran to its end, otherwise how the
        /// fix ended. 126 is the password dialog dismissed, or the password refused, which needs
        /// no message; 127 is pkexec unable to ask at all, as without an authentication agent,
        /// and its first line says why; any other code is the script's, whose last line names
        /// the step that failed.
        /// </summary>
        public static StatusFixResult? Interpret(int exitCode, string standardError)
        {
            var lines = standardError.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return exitCode switch
            {
                0 => null,
                126 => new StatusFixResult(StatusFixOutcome.Cancelled),
                127 => new StatusFixResult(StatusFixOutcome.Failed,
                                           "Couldn't ask for an administrator's password" + (lines.Length > 0 ? $": {lines[0]}" : ".")),
                _ => new StatusFixResult(StatusFixOutcome.Failed,
                                         lines.LastOrDefault() ?? $"The fix stopped with exit code {exitCode}.")
            };
        }
    }
}
