using SimpleKVM.Displays.linux;
using SimpleKVM.Platform;

namespace SimpleKVM.Tests;

// The Linux Fix for monitors the app can't reach: the rule, the script that installs it as root,
// and what pkexec's exit codes mean. The script itself has been run for real, on Ubuntu 26.04;
// here it is checked for what would break it on every machine.
public class LinuxBusAccessTests
{
    [Fact]
    public void The_rule_is_ddcutils_for_the_buses_of_display_adapters()
    {
        Assert.Equal("SUBSYSTEM==\"i2c-dev\", KERNEL==\"i2c-[0-9]*\", ATTRS{class}==\"0x03*\", TAG+=\"uaccess\"", BusAccess.Rule);
    }

    [Fact]
    public void The_rule_file_is_read_before_systemd_acts_on_the_uaccess_tag()
    {
        // 73-seat-late.rules turns the tag into the user's access; rules are read in name order
        Assert.StartsWith("/etc/udev/rules.d/", BusAccess.RuleFile);
        Assert.True(string.CompareOrdinal(Path.GetFileName(BusAccess.RuleFile), "73-seat-late.rules") < 0);
    }

    [Fact]
    public void The_script_has_unix_line_endings_whatever_the_source_file_has()
    {
        Assert.DoesNotContain('\r', BusAccess.FixScript);
        Assert.EndsWith("exit 0", BusAccess.FixScript);
    }

    [Fact]
    public void The_rule_goes_into_the_file_word_for_word()
    {
        // Written inside single quotes, which nothing in the rule may close
        Assert.DoesNotContain("'", BusAccess.Rule);
        Assert.Contains($"'{BusAccess.Rule}' > {BusAccess.RuleFile}.new && mv -f {BusAccess.RuleFile}.new {BusAccess.RuleFile}", BusAccess.FixScript);
    }

    [Fact]
    public void New_buses_are_born_with_the_rule_and_existing_ones_get_it_after()
    {
        var script = BusAccess.FixScript;
        int write = script.IndexOf($"> {BusAccess.RuleFile}.new", StringComparison.Ordinal);
        int reload = script.IndexOf("udevadm control --reload", StringComparison.Ordinal);
        int modprobe = script.IndexOf("modprobe i2c-dev", StringComparison.Ordinal);
        int trigger = script.IndexOf("udevadm trigger --subsystem-match=i2c-dev --action=change", StringComparison.Ordinal);
        int settle = script.IndexOf("udevadm settle", StringComparison.Ordinal);

        Assert.True(write >= 0 && write < reload, "the rule must be written before udev reloads");
        Assert.True(reload < modprobe, "udev must have the rule before the module makes new buses");
        Assert.True(modprobe < trigger && trigger < settle, "the existing buses get it after, and it's waited for");
    }

    [Fact]
    public void Every_step_that_can_fail_says_so_with_exit_code_1()
    {
        // No set -e (the shell ignores it inside functions and || lists): each step has its own
        // "|| fail", and fail exits 1, so pkexec's 126 and 127 can't come from the script
        var script = BusAccess.FixScript;
        Assert.DoesNotContain("set -e", script);
        Assert.Contains("fail() { echo \"$*\" >&2; exit 1; }", script);
        foreach (var step in new[] { "mkdir -p /etc/udev/rules.d", "udevadm control --reload", "modprobe i2c-dev", "udevadm trigger" })
        {
            var line = script.Split('\n').Single(l => l.Contains(step, StringComparison.Ordinal));
            Assert.Contains("|| fail", line);
        }
    }

    [Fact]
    public void Ran_to_the_end_leaves_the_verdict_to_the_check_that_follows()
    {
        Assert.Null(BusAccess.Interpret(0, ""));
    }

    [Fact]
    public void A_dismissed_password_dialog_is_a_cancel_with_nothing_to_say()
    {
        var result = BusAccess.Interpret(126, "Error executing command as another user: Not authorized\n\nThis incident has been reported.\n");

        Assert.Equal(StatusFixOutcome.Cancelled, result?.Outcome);
        Assert.Null(result?.Message);
    }

    [Fact]
    public void When_pkexec_cant_ask_its_first_line_says_why()
    {
        var result = BusAccess.Interpret(127, "Error executing command as another user: No authentication agent found.\n");

        Assert.Equal(StatusFixOutcome.Failed, result?.Outcome);
        Assert.Equal("Couldn't ask for an administrator's password: Error executing command as another user: No authentication agent found.", result?.Message);
    }

    [Fact]
    public void A_failed_step_is_named_by_the_scripts_last_line()
    {
        var result = BusAccess.Interpret(1, "modprobe: FATAL: Module i2c-dev not found in directory /lib/modules/7.0.0\nCould not load the i2c-dev kernel module.\n");

        Assert.Equal(StatusFixOutcome.Failed, result?.Outcome);
        Assert.Equal("Could not load the i2c-dev kernel module.", result?.Message);
    }

    [Fact]
    public void A_failure_without_words_still_says_something()
    {
        var result = BusAccess.Interpret(2, "");

        Assert.Equal(StatusFixOutcome.Failed, result?.Outcome);
        Assert.Equal("The fix stopped with exit code 2.", result?.Message);
    }
}
