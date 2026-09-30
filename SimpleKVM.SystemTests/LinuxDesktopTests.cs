using SimpleKVM.Displays;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace SimpleKVM.SystemTests;

/// <summary>
/// The Linux build inside real desktop sessions. Each test runs against every VM of the rig
/// that is up, and skips when none is. A VM has no I2C buses, so DDC/CI itself is out of
/// reach here; everything around it is not: enumeration and layout from the compositor,
/// idle detection both ways, hotkeys through evdev, autostart, and the GUI starting.
/// </summary>
public class LinuxDesktopTests(ITestOutputHelper output)
{
    LinuxVm Vm(string machineName)
    {
        Skip.If(VmRig.Instance.Unavailable != null, VmRig.Instance.Unavailable);
        var machine = VmRig.Instance.Find(machineName);
        Skip.If(machine == null, $"{machineName} is not running");
        var vm = LinuxVm.Get(machine!);
        output.WriteLine($"{machine}: build {vm.Build}, session {vm.SessionType} on {vm.Display}");
        return vm;
    }

    static string Quote(string s) => LinuxVm.Quote(s);

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_desktop_session_is_of_the_configured_type(string machine)
    {
        var vm = Vm(machine);

        Assert.Equal(vm.Machine.Session, vm.SessionType);
        Assert.True(vm.RunInSession("test -S \"$XDG_RUNTIME_DIR/bus\"").ExitCode == 0, "the session bus is not there");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void ListMonitors_finds_the_virtual_display_through_the_compositor(string machine)
    {
        var vm = Vm(machine);

        var result = vm.RunInSession($"{LinuxVm.Exe} --list-monitors");
        output.WriteLine(result.Output);
        Assert.Equal(0, result.ExitCode);

        //Exactly one screen, laid out by the compositor rather than guessed
        var bounds = Regex.Matches(result.Stdout, @"^Screen bounds: (-?\d+),(-?\d+),(-?\d+),(-?\d+) -> id (\w+)", RegexOptions.Multiline);
        var screen = Assert.Single(bounds);
        int left = int.Parse(screen.Groups[1].Value), top = int.Parse(screen.Groups[2].Value);
        int right = int.Parse(screen.Groups[3].Value), bottom = int.Parse(screen.Groups[4].Value);
        Assert.Equal((0, 0), (left, top));
        Assert.True(right >= 640 && bottom >= 480, $"implausible screen size {right}x{bottom}");
        Assert.Equal(MonitorIdentity.FromBounds(left, top, right, bottom), screen.Groups[5].Value);

        var source = Regex.Match(result.Stdout, @"^Layout source: (\w+)", RegexOptions.Multiline);
        Assert.True(source.Success, "no layout source line");
        var expected = vm.SessionType == "x11" ? "xrandr" : vm.Machine.Desktop == "kde" ? "kscreen" : "mutter";
        Assert.Equal(expected, source.Groups[1].Value);

        //The one monitor is the virtual display, without DDC, and the app says why: either the VM
        //exposes no I2C buses at all, or (VirtualBox's chipset does expose one) the user can't open them
        Assert.Matches(@"^1 monitor\(s\):", Regex.Match(result.Stdout, @"^\d+ monitor\(s\):", RegexOptions.Multiline).Value);
        Assert.Matches(@"^Note: (No I2C buses|No access to /dev/i2c-\*)", Regex.Match(result.Stdout, @"^Note: .*", RegexOptions.Multiline).Value);

        //On X11 the size must agree with what xrandr itself reports as the current mode
        if (vm.SessionType == "x11")
        {
            var current = vm.RunInSession("xrandr --current | grep -oE '[0-9]+x[0-9]+' | head -1").Stdout.Trim();
            Assert.Equal($"{right - left}x{bottom - top}", current);
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void GetCaps_reports_the_missing_ddc_transport_cleanly(string machine)
    {
        var vm = Vm(machine);

        var result = vm.RunInSession($"{LinuxVm.Exe} --get-caps 1");
        output.WriteLine(result.Output);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No DDC transport", result.Stdout);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_reports_an_idle_time_that_grows(string machine)
    {
        var vm = Vm(machine);

        //Without the input group this is the D-Bus path (Mutter's IdleMonitor or the ScreenSaver
        //interface); with it, evdev. Either way nobody is typing in the VM, so idle must grow
        //by about the sampled span, and be far past the 64 ms the broken regex reported.
        var result = vm.RunInSession($"timeout 5 {LinuxVm.Exe} --watch-idle", timeoutSeconds: 30);
        output.WriteLine(result.Output);

        var samples = Samples(result.Stdout);
        Assert.True(samples.Count >= 3, $"expected several samples, got {samples.Count}");
        Assert.True(samples.Zip(samples.Skip(1)).All(p => p.Second >= p.First - 0.2), "idle time went backwards with nobody typing");
        Assert.True(samples[^1] - samples[0] >= 2.0, $"idle grew only {samples[^1] - samples[0]:F1} s over the run");
        Assert.True(samples[^1] > 1.0, $"idle stuck at {samples[^1]:F3} s");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_falls_back_to_the_desktop_without_the_input_group(string machine)
    {
        var vm = Vm(machine);

        //Run as vagrant with the supplementary groups dropped, so /dev/input is unreadable
        //whatever the VM's setting and the D-Bus path (Mutter's IdleMonitor, or KDE's
        //ScreenSaver interface) is what answers. If a desktop answered in seconds instead
        //of milliseconds the growth over five seconds would be a few thousandths.
        Skip.If(vm.Run("command -v setpriv").ExitCode != 0, "setpriv is not available on this VM");

        var result = vm.Sudo($"setpriv --reuid=1000 --regid=1000 --clear-groups {vm.SessionEnv} timeout 5 {LinuxVm.Exe} --watch-idle", timeoutSeconds: 30);
        output.WriteLine(result.Output);

        //KDE Plasma on Wayland refuses org.freedesktop.ScreenSaver.GetSessionIdleTime ("not
        //supported on this platform") and has no Mutter-style interface, so there the app
        //must say that the input group is the only way, rather than report zero forever
        var note = Regex.Match(result.Stdout, @"^Note: (.*)$", RegexOptions.Multiline);
        if (note.Success)
        {
            Assert.Contains("input group", note.Groups[1].Value);
            Skip.If(true, $"this desktop offers no idle time over D-Bus; the app says so: {note.Groups[1].Value}");
        }

        var samples = Samples(result.Stdout);
        Assert.True(samples.Count >= 3, $"expected several samples, got {samples.Count}");
        Assert.True(samples[^1] - samples[0] >= 2.0, $"idle grew only {samples[^1] - samples[0]:F3} s over the run: wrong units, or no desktop answered");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_resets_when_a_key_is_pressed(string machine)
    {
        var vm = Vm(machine);

        //A virtual keyboard presses Shift on its own mid-run: harmless to the desktop, but input.
        //The injector waits before pressing so evdev's 2 s device rescan has opened it.
        vm.Run("rm -f /tmp/skvm-idle.log");
        vm.RunInSession($"nohup timeout 9 {LinuxVm.Exe} --watch-idle > /tmp/skvm-idle.log 2>&1 &");
        Thread.Sleep(4000);
        var inject = vm.Sudo("simplekvm-inject-keys --settle-ms 2500 KEY_LEFTSHIFT", timeoutSeconds: 30);
        Assert.True(inject.ExitCode == 0, $"key injection failed: {inject.Output}");
        Thread.Sleep(4000);

        var log = vm.Run("cat /tmp/skvm-idle.log").Stdout;
        output.WriteLine(log);

        var samples = Samples(log);
        Assert.True(samples.Count >= 5, $"expected several samples, got {samples.Count}");
        bool dropped = samples.Zip(samples.Skip(1)).Any(p => p.Second < p.First - 0.5);
        Assert.True(dropped, "the idle time never reset after the key press");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_fires_on_an_injected_chord_as_root(string machine)
    {
        var vm = Vm(machine);
        AssertHotkeyFires(vm, asRoot: true);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_fires_for_a_user_in_the_input_group(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.Machine.InputGroup, "this VM's user is deliberately outside the input group");
        AssertHotkeyFires(vm, asRoot: false);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_explains_the_input_group_when_devices_are_unreadable(string machine)
    {
        var vm = Vm(machine);
        Skip.If(vm.Machine.InputGroup, "this VM's user is in the input group");

        var result = vm.RunInSession($"{LinuxVm.Exe} --test-hotkey Ctrl+Shift+F9");
        output.WriteLine(result.Output);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("input", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    void AssertHotkeyFires(LinuxVm vm, bool asRoot)
    {
        //Ctrl+Shift+F9: nothing on a stock desktop binds it, and unlike Ctrl+Alt+Fn it doesn't switch consoles
        vm.Run("rm -f /tmp/skvm-hotkey.log");
        var launch = $"nohup timeout 10 {LinuxVm.Exe} --test-hotkey Ctrl+Shift+F9 > /tmp/skvm-hotkey.log 2>&1 &";
        if (asRoot) vm.Sudo(launch); else vm.RunInSession(launch);
        Thread.Sleep(3000);

        var inject = vm.Sudo("simplekvm-inject-keys --settle-ms 2500 KEY_LEFTCTRL+KEY_LEFTSHIFT+KEY_F9", timeoutSeconds: 30);
        Assert.True(inject.ExitCode == 0, $"key injection failed: {inject.Output}");
        Thread.Sleep(2000);

        var log = vm.Run("cat /tmp/skvm-hotkey.log").Stdout;
        output.WriteLine(log);
        Assert.Contains("Registered Ctrl+Shift+F9", log);
        Assert.Contains("hotkey fired: Ctrl+Shift+F9", log);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void SetStartup_writes_and_removes_the_autostart_entry(string machine)
    {
        var vm = Vm(machine);
        const string entry = "/home/vagrant/.config/autostart/simplekvm.desktop";

        try
        {
            var on = vm.RunInSession($"{LinuxVm.Exe} --set-startup on");
            output.WriteLine(on.Output);
            Assert.Equal(0, on.ExitCode);
            Assert.Contains("enabled", on.Stdout);

            var file = vm.Run($"cat {entry}").Stdout;
            output.WriteLine(file);
            Assert.Contains("[Desktop Entry]", file);
            Assert.Contains($"Exec=\"{LinuxVm.Exe}\" --minimized", file);

            Assert.Contains("enabled", vm.RunInSession($"{LinuxVm.Exe} --set-startup status").Stdout);

            var off = vm.RunInSession($"{LinuxVm.Exe} --set-startup off");
            Assert.Contains("disabled", off.Stdout);
            Assert.NotEqual(0, vm.Run($"test -f {entry}").ExitCode);
        }
        finally
        {
            vm.Run($"rm -f {entry}");
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_gui_starts_in_the_session_and_a_second_launch_defers_to_it(string machine)
    {
        var vm = Vm(machine);

        try
        {
            vm.Run("pkill -f " + Quote(LinuxVm.Exe) + " ; rm -f /tmp/skvm-gui.log");
            vm.RunInSession($"nohup {LinuxVm.Exe} > /tmp/skvm-gui.log 2>&1 &");
            Thread.Sleep(8000);

            var running = vm.Run("pgrep -fc " + Quote(LinuxVm.Exe)).Stdout.Trim();
            var log = vm.Run("cat /tmp/skvm-gui.log").Stdout;
            output.WriteLine(log);
            Assert.True(running == "1", $"expected the GUI to be running (found {running}); log:\n{log}");
            Assert.DoesNotContain("Unhandled exception", log);

            //A second launch must hand over to the first and exit, leaving one process
            var second = vm.RunInSession($"timeout 10 {LinuxVm.Exe}; echo exit=$?", timeoutSeconds: 30);
            output.WriteLine(second.Output);
            Assert.Contains("exit=0", second.Stdout);
            Assert.Equal("1", vm.Run("pgrep -fc " + Quote(LinuxVm.Exe)).Stdout.Trim());
        }
        finally
        {
            vm.Run("pkill -f " + Quote(LinuxVm.Exe) + " || true");
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void VerifyRules_parses_a_rules_file_in_the_trimmed_build(string machine)
    {
        var vm = Vm(machine);

        //A Windows-written rules.json: the binder maps its Monitor type to the Linux one
        const string rules = """
            [{"Trigger":{"$type":"SimpleKVM.Rules.Triggers.HotkeyTrigger, SimpleKVM","HotkeyAsString":"Win+NumPad1"},
              "Actions":[{"$type":"SimpleKVM.Rules.Actions.SetMonitorSourceAction, SimpleKVM",
                          "Monitor":{"$type":"SimpleKVM.Displays.win.Monitor, SimpleKVM","MonitorUniqueId":"ABC"},"SetMonitorSourceIdTo":17}],
              "RunCount":3,"Status":0,"Name":"Switch to this computer","DelaySeconds":0}]
            """;
        vm.Run($"cat > /tmp/skvm-rules.json <<'EOF'\n{rules}\nEOF");

        var result = vm.Run($"{LinuxVm.Exe} --verify-rules /tmp/skvm-rules.json");
        output.WriteLine(result.Output);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Parsed 1 rule(s)", result.Stdout);
        Assert.Contains("Switch to this computer", result.Stdout);
    }

    static List<double> Samples(string watchIdleOutput)
    {
        return Regex.Matches(watchIdleOutput, @"idle: ([\d.]+) s")
                    .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                    .ToList();
    }
}
