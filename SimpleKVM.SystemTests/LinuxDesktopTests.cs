using SimpleKVM.Displays;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace SimpleKVM.SystemTests;

/// <summary>
/// The Linux build inside real desktop sessions. Each test runs against every VM of the rig
/// that is up, and skips when none is. A VM has no I2C buses, so DDC/CI itself is out of
/// reach here; everything around it is not: enumeration and layout from the compositor,
/// idle detection and hotkeys by every route (the desktop's own, and the input devices),
/// autostart and the applications menu, the window's life, and the release tarball.
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

    //The deployed binary's own processes, matched on the start of their command line so the
    //shells and timeout wrappers that merely mention its path are left alone
    static string ExePattern => Quote("^" + LinuxVm.Exe);

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_desktop_session_is_of_the_configured_type(string machine)
    {
        var vm = Vm(machine);

        Assert.Equal(vm.Machine.Session, vm.SessionType);
        Assert.True(vm.RunInSession("test -S \"$XDG_RUNTIME_DIR/bus\"").ExitCode == 0, "the session bus is not there");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void ListMonitors_lays_out_every_virtual_screen_through_the_compositor(string machine)
    {
        var vm = Vm(machine);

        var result = vm.RunInSession($"{LinuxVm.Exe} --list-monitors");
        output.WriteLine(result.Output);
        Assert.Equal(0, result.ExitCode);

        //One screen per virtual screen the rig gives the VM, laid out by the compositor rather than guessed
        var screens = Regex.Matches(result.Stdout, @"^Screen bounds: (-?\d+),(-?\d+),(-?\d+),(-?\d+) -> id (\w+)", RegexOptions.Multiline)
                           .Select(m => (Left: int.Parse(m.Groups[1].Value), Top: int.Parse(m.Groups[2].Value),
                                         Right: int.Parse(m.Groups[3].Value), Bottom: int.Parse(m.Groups[4].Value),
                                         Id: m.Groups[5].Value))
                           .OrderBy(s => s.Left)
                           .ToList();
        Assert.Equal(vm.Machine.Monitors, screens.Count);

        foreach (var s in screens)
        {
            Assert.True(s.Right - s.Left >= 640 && s.Bottom - s.Top >= 480, $"implausible screen size {s.Right - s.Left}x{s.Bottom - s.Top}");
            Assert.Equal(MonitorIdentity.FromBounds(s.Left, s.Top, s.Right, s.Bottom), s.Id);
        }

        //Side by side from the desktop's origin, as the rig lays them out: no gaps, no overlaps
        Assert.Equal((0, 0), (screens[0].Left, screens[0].Top));
        for (int i = 1; i < screens.Count; i++)
        {
            Assert.Equal(screens[i - 1].Right, screens[i].Left);
            Assert.Equal(screens[i - 1].Top, screens[i].Top);
        }

        var source = Regex.Match(result.Stdout, @"^Layout source: (\w+)", RegexOptions.Multiline);
        Assert.True(source.Success, "no layout source line");
        var expected = vm.SessionType == "x11" ? "xrandr" : vm.Machine.Desktop == "kde" ? "kscreen" : "mutter";
        Assert.Equal(expected, source.Groups[1].Value);

        //One monitor per screen, numbered left to right like the layout view and config.json
        Assert.Matches($@"^{screens.Count} monitor\(s\):", Regex.Match(result.Stdout, @"^\d+ monitor\(s\):", RegexOptions.Multiline).Value);
        var numbered = Regex.Matches(result.Stdout, @"^\[(\d+)\] .* id=(\w+)", RegexOptions.Multiline)
                            .Select(m => (Number: int.Parse(m.Groups[1].Value), Id: m.Groups[2].Value))
                            .OrderBy(m => m.Number)
                            .Select(m => m.Id);
        Assert.Equal(screens.Select(s => s.Id), numbered);

        //No DDC in a VM, and the app says why: either the VM exposes no I2C buses at all, or
        //(VirtualBox's chipset does expose one) the user can't open them
        Assert.Matches(@"^Note: (No I2C buses|No access to /dev/i2c-\*)", Regex.Match(result.Stdout, @"^Note: .*", RegexOptions.Multiline).Value);

        //On X11, the screens must be exactly the monitors xrandr itself reports
        if (vm.SessionType == "x11")
        {
            //e.g. " 0: +*Virtual-1 1280/338x800/211+0+0  Virtual-1"
            var reported = Regex.Matches(vm.RunInSession("xrandr --listmonitors").Stdout, @"(\d+)/\d+x(\d+)/\d+\+(-?\d+)\+(-?\d+)")
                                .Select(m =>
                                {
                                    int w = int.Parse(m.Groups[1].Value), h = int.Parse(m.Groups[2].Value);
                                    int x = int.Parse(m.Groups[3].Value), y = int.Parse(m.Groups[4].Value);
                                    return (x, y, x + w, y + h);
                                })
                                .OrderBy(r => r.x);
            Assert.Equal(screens.Select(s => (s.Left, s.Top, s.Right, s.Bottom)), reported);
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void Monitor_ids_stay_the_same_when_the_desktop_is_scaled(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.Machine.Desktop == "kde", "the scale is set with kscreen-doctor, which only the KDE VM has");

        //Ids are made from the screens' bounds, which must be in pixels: at 125% a Wayland desktop
        //reports a 1280x800 screen as 1024x640, and ids taken from that would orphan every rule
        static string Ids(string listMonitors) =>
            string.Join(" ", Regex.Matches(listMonitors, @"^Screen bounds: .* -> id (\w+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value).Order());

        var before = vm.RunInSession($"{LinuxVm.Exe} --list-monitors");
        output.WriteLine(before.Output);
        Assert.Equal(2, Ids(before.Stdout).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        try
        {
            //What the display settings do: scale the screens, mixed scales here, and keep them
            //side by side (the first screen is 1024 logical pixels wide at 125%)
            var scaled = vm.RunInSession("kscreen-doctor output.Virtual-1.scale.1.25 output.Virtual-1.position.0,0 " +
                                         "output.Virtual-2.scale.1.5 output.Virtual-2.position.1024,0");
            output.WriteLine(scaled.Output);
            Thread.Sleep(2000);

            var state = vm.RunInSession("kscreen-doctor -j").Stdout;
            Assert.Matches(@"""scale"":\s*1\.25", state);    //the desktop really is scaled now

            var after = vm.RunInSession($"{LinuxVm.Exe} --list-monitors");
            output.WriteLine(after.Output);
            Assert.Equal(Ids(before.Stdout), Ids(after.Stdout));
        }
        finally
        {
            vm.RunInSession("kscreen-doctor output.Virtual-1.scale.1 output.Virtual-1.position.0,0 " +
                            "output.Virtual-2.scale.1 output.Virtual-2.position.1280,0");
            Thread.Sleep(2000);
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

    // ------------------------------------------------------------------ idle

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_reports_an_idle_time_that_grows(string machine)
    {
        var vm = Vm(machine);

        //GNOME announces idle and active over D-Bus. KDE Plasma on Wayland offers neither of
        //the D-Bus idle interfaces, so there it is the input devices. Either way nobody is
        //typing in the VM, so idle must grow by about the sampled span.
        var result = vm.RunInSession($"timeout 5 {LinuxVm.Exe} --watch-idle", timeoutSeconds: 30);
        output.WriteLine(result.Output);

        Assert.Equal(vm.Machine.Desktop == "kde" ? "evdev" : "Mutter IdleMonitor", IdleSource(result.Stdout));

        var samples = Samples(result.Stdout);
        Assert.True(samples.Count >= 3, $"expected several samples, got {samples.Count}");
        Assert.True(samples.Zip(samples.Skip(1)).All(p => p.Second >= p.First - 0.2), "idle time went backwards with nobody typing");
        Assert.True(samples[^1] - samples[0] >= 2.0, $"idle grew only {samples[^1] - samples[0]:F1} s over the run");
        Assert.True(samples[^1] > 1.0, $"idle stuck at {samples[^1]:F3} s");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_needs_no_input_group_where_the_desktop_reports_idle_time(string machine)
    {
        var vm = Vm(machine);

        //Run as vagrant with the supplementary groups dropped, so /dev/input is unreadable
        //whatever the VM's setting and only the desktop can answer
        Skip.If(vm.Run("command -v setpriv").ExitCode != 0, "setpriv is not available on this VM");

        var result = vm.Sudo($"{WithoutGroups(vm)} timeout 5 {LinuxVm.Exe} --watch-idle", timeoutSeconds: 30);
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

        Assert.Equal("Mutter IdleMonitor", IdleSource(result.Stdout));

        var samples = Samples(result.Stdout);
        Assert.True(samples.Count >= 3, $"expected several samples, got {samples.Count}");
        Assert.True(samples[^1] - samples[0] >= 2.0, $"idle grew only {samples[^1] - samples[0]:F3} s over the run: wrong units, or no desktop answered");
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_resets_when_a_key_is_pressed(string machine)
    {
        var vm = Vm(machine);

        //A virtual keyboard presses Shift on its own mid-run: harmless to the desktop, but input.
        //The injector waits before pressing so that, where the devices are read directly,
        //the 2 s device rescan has opened it.
        var log = WatchIdleAcrossAKeyPress(vm, environment: "");
        AssertIdleDropped(log);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void WatchIdle_reads_the_input_devices_when_pinned_to_them(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.Machine.InputGroup, "this VM's user is deliberately outside the input group");

        //The route every desktop has, whatever it offers of its own
        var log = WatchIdleAcrossAKeyPress(vm, environment: "SIMPLEKVM_IDLE_SOURCE=evdev ");
        Assert.Equal("evdev", IdleSource(log));
        AssertIdleDropped(log);
    }

    string WatchIdleAcrossAKeyPress(LinuxVm vm, string environment)
    {
        vm.Run("rm -f /tmp/skvm-idle.log");
        vm.RunInSession($"{environment}nohup timeout 9 {LinuxVm.Exe} --watch-idle > /tmp/skvm-idle.log 2>&1 &");
        Thread.Sleep(4000);
        var inject = vm.Sudo("simplekvm-inject-keys --settle-ms 2500 KEY_LEFTSHIFT", timeoutSeconds: 30);
        Assert.True(inject.ExitCode == 0, $"key injection failed: {inject.Output}");
        Thread.Sleep(4000);

        var log = vm.Run("cat /tmp/skvm-idle.log").Stdout;
        output.WriteLine(log);
        return log;
    }

    static void AssertIdleDropped(string log)
    {
        var samples = Samples(log);
        Assert.True(samples.Count >= 5, $"expected several samples, got {samples.Count}");
        bool dropped = samples.Zip(samples.Skip(1)).Any(p => p.Second < p.First - 0.5);
        Assert.True(dropped, "the idle time never reset after the key press");
    }

    // ------------------------------------------------------------------ hotkeys

    //Ctrl+Shift+F9: nothing on a stock desktop binds it, and unlike Ctrl+Alt+Fn it doesn't switch consoles
    const string Hotkey = "Ctrl+Shift+F9";
    const string HotkeyChord = "KEY_LEFTCTRL+KEY_LEFTSHIFT+KEY_F9";

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_needs_no_input_group_on_x11(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.SessionType == "x11", "key grabs are the X11 session's route");

        //Groups dropped: the keys can only arrive through the X server's grab
        StartHotkeyWatch(vm, $"{WithoutGroups(vm)} nohup timeout 30 {LinuxVm.Exe} --test-hotkey {Hotkey}", asRoot: true);

        //The grab is exclusive: while this copy holds the combination another is refused it
        var second = vm.RunInSession($"timeout 5 {LinuxVm.Exe} --test-hotkey {Hotkey}");
        output.WriteLine(second.Output);
        Assert.Equal(1, second.ExitCode);
        Assert.Contains("already taken", second.Stdout);

        var log = PressAndCollect(vm);
        Assert.Contains("Hotkeys through: x11", log);
        AssertFiredOncePerPress(log);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_goes_through_the_desktop_portal_on_wayland(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.SessionType == "wayland", "the portal is the Wayland session's route");
        Skip.IfNot(HasShortcutPortal(vm), "this desktop's portal has no GlobalShortcuts interface (GNOME before 48)");

        //GNOME keeps only the shortcuts of an app's latest request, so this test's would push
        //out the ones a copy being tried by hand (play.ps1) has had confirmed. They are put
        //back afterwards, which also means GNOME shows its dialog on every run of this test.
        const string gnomeStore = "/org/gnome/settings-daemon/global-shortcuts/";
        bool gnome = vm.Machine.Desktop == "gnome";
        if (gnome) vm.RunInSession($"dconf dump {gnomeStore} > /tmp/skvm-shortcuts.dconf");

        try
        {
            //Groups dropped: the keys can only arrive through the desktop
            StartHotkeyWatch(vm, $"{WithoutGroups(vm)} nohup timeout 40 {LinuxVm.Exe} --test-hotkey {Hotkey}", asRoot: true);

            //A shortcut the desktop hasn't seen before is put to the user in a dialog, which the
            //test answers on the keyboard; one it knows is bound at once. The app says when it is.
            if (!WaitForLog(vm, "the desktop bound", seconds: 6))
            {
                var confirm = vm.Machine.Desktop == "kde" ? "KEY_ENTER" : "KEY_LEFTSHIFT+KEY_TAB KEY_ENTER";   //KDE: OK is the default; GNOME: Add is one step back from the list
                vm.Sudo($"simplekvm-inject-keys --settle-ms 800 {confirm}", timeoutSeconds: 30);
                Assert.True(WaitForLog(vm, "the desktop bound", seconds: 12),
                            "the desktop never bound the shortcut; log:\n" + vm.Run("cat /tmp/skvm-hotkey.log").Stdout);
            }

            var log = PressAndCollect(vm);
            Assert.Contains("Hotkeys through: portal", log);
            AssertFiredOncePerPress(log);
        }
        finally
        {
            vm.Run("pkill -f " + ExePattern + " || true");
            if (gnome) vm.RunInSession($"dconf reset -f {gnomeStore}; dconf load {gnomeStore} < /tmp/skvm-shortcuts.dconf; rm -f /tmp/skvm-shortcuts.dconf");
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_falls_back_to_the_input_devices_where_the_desktop_has_no_shortcut_service(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.SessionType == "wayland" && !HasShortcutPortal(vm), "this desktop has a better route than the input devices");

        if (vm.Machine.InputGroup)
        {
            StartHotkeyWatch(vm, $"nohup timeout 30 {LinuxVm.Exe} --test-hotkey {Hotkey}", asRoot: false);
            var log = PressAndCollect(vm);
            Assert.Contains("Hotkeys through: evdev", log);
            AssertFiredOncePerPress(log);
            return;
        }

        //Outside the input group there is no way left, and the app has to say which group that is
        var result = vm.RunInSession($"{LinuxVm.Exe} --test-hotkey {Hotkey}");
        output.WriteLine(result.Output);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Hotkeys through: evdev", result.Stdout);
        Assert.Contains("'input' group", result.Stdout);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_reads_the_input_devices_when_pinned_to_them(string machine)
    {
        var vm = Vm(machine);
        Skip.IfNot(vm.Machine.InputGroup, "this VM's user is deliberately outside the input group");

        StartHotkeyWatch(vm, $"SIMPLEKVM_HOTKEYS=evdev nohup timeout 30 {LinuxVm.Exe} --test-hotkey {Hotkey}", asRoot: false);

        var log = PressAndCollect(vm);
        Assert.Contains("Hotkeys through: evdev", log);
        AssertFiredOncePerPress(log);
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void TestHotkey_reads_the_input_devices_as_root(string machine)
    {
        var vm = Vm(machine);

        //Root has no desktop session to ask, and can read every device
        StartHotkeyWatch(vm, $"SIMPLEKVM_HOTKEYS=evdev nohup timeout 30 {LinuxVm.Exe} --test-hotkey {Hotkey}", asRoot: true);

        var log = PressAndCollect(vm);
        Assert.Contains("Hotkeys through: evdev", log);
        AssertFiredOncePerPress(log);
    }

    /// <summary>The vagrant user with the session's environment but none of its supplementary groups (so not "input"); for vm.Sudo.</summary>
    static string WithoutGroups(LinuxVm vm) => $"setpriv --reuid=1000 --regid=1000 --clear-groups {vm.SessionEnv}";

    static bool HasShortcutPortal(LinuxVm vm)
    {
        return vm.RunInSession("gdbus introspect --session --dest org.freedesktop.portal.Desktop --object-path /org/freedesktop/portal/desktop " +
                               "| grep -q 'interface org.freedesktop.portal.GlobalShortcuts'").ExitCode == 0;
    }

    void StartHotkeyWatch(LinuxVm vm, string launch, bool asRoot)
    {
        vm.Run("sudo rm -f /tmp/skvm-hotkey.log");
        launch += " > /tmp/skvm-hotkey.log 2>&1 &";
        if (asRoot) vm.Sudo(launch); else vm.RunInSession(launch);

        Assert.True(WaitForLog(vm, "Registered " + Hotkey, seconds: 10),
                    "the hotkey was not registered; log:\n" + vm.Run("cat /tmp/skvm-hotkey.log").Stdout);
    }

    static bool WaitForLog(LinuxVm vm, string text, int seconds)
    {
        for (int i = 0; i < seconds * 2; i++)
        {
            if (vm.Run($"grep -qF {Quote(text)} /tmp/skvm-hotkey.log").ExitCode == 0) return true;
            Thread.Sleep(500);
        }
        return false;
    }

    /// <summary>A tap, a press held long enough to auto-repeat, and two near misses; returns the watch's log.</summary>
    string PressAndCollect(LinuxVm vm)
    {
        var inject = vm.Sudo($"simplekvm-inject-keys --settle-ms 2500 {HotkeyChord}", timeoutSeconds: 30);
        Assert.True(inject.ExitCode == 0, $"key injection failed: {inject.Output}");
        Thread.Sleep(1500);

        vm.Sudo($"simplekvm-inject-keys --settle-ms 2500 --hold-ms 1500 {HotkeyChord}", timeoutSeconds: 30);
        Thread.Sleep(1500);

        vm.Sudo("simplekvm-inject-keys --settle-ms 2500 KEY_LEFTSHIFT+KEY_F9 KEY_LEFTCTRL+KEY_LEFTSHIFT+KEY_F10", timeoutSeconds: 30);
        Thread.Sleep(1500);

        var log = vm.Run("cat /tmp/skvm-hotkey.log").Stdout;
        output.WriteLine(log);

        //The watch has done its job; left to its timeout it would still hold the keys when the next test wants them
        vm.Sudo("pkill -f " + ExePattern + " || true");
        return log;
    }

    static void AssertFiredOncePerPress(string log)
    {
        int fired = Regex.Matches(log, "hotkey fired: " + Regex.Escape(Hotkey)).Count;
        Assert.True(fired == 2, $"expected the hotkey to fire once for the tap and once for the held press, and not for the near misses; it fired {fired} time(s)");
    }

    // ------------------------------------------------------------------ autostart and the applications menu

    const string AppId = "io.github.fiddyschmitt.simplekvm";

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void SetStartup_writes_and_removes_the_autostart_entry(string machine)
    {
        var vm = Vm(machine);

        //Scratch XDG folders: nothing of the VM user's own is touched
        const string config = "/tmp/skvm-startup-config", data = "/tmp/skvm-startup-data";
        string env = $"XDG_CONFIG_HOME={config} XDG_DATA_HOME={data}";
        string entry = $"{config}/autostart/{AppId}.desktop";

        try
        {
            vm.Run($"rm -rf {config} {data}");

            var on = vm.RunInSession($"{env} {LinuxVm.Exe} --set-startup on");
            output.WriteLine(on.Output);
            Assert.Equal(0, on.ExitCode);
            Assert.Contains("enabled", on.Stdout);

            var file = vm.Run($"cat {entry}").Stdout;
            output.WriteLine(file);
            Assert.Contains("[Desktop Entry]", file);
            Assert.Contains($"Exec=\"{LinuxVm.Exe}\" --minimized", file);

            //The copy the desktop starts at login is known to it by the entry's name, which has to
            //lead to a launcher entry; one is written, out of the menu, when there is none
            var launcher = vm.Run($"cat {data}/applications/{AppId}.desktop").Stdout;
            Assert.Contains("NoDisplay=true", launcher);

            Assert.Contains("enabled", vm.RunInSession($"{env} {LinuxVm.Exe} --set-startup status").Stdout);

            var off = vm.RunInSession($"{env} {LinuxVm.Exe} --set-startup off");
            Assert.Contains("disabled", off.Stdout);
            Assert.NotEqual(0, vm.Run($"test -f {entry}").ExitCode);
        }
        finally
        {
            vm.Run($"rm -rf {config} {data}");
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void SetMenuEntry_shows_hides_and_removes_the_launcher(string machine)
    {
        var vm = Vm(machine);

        const string data = "/tmp/skvm-menu-data";
        string env = $"XDG_DATA_HOME={data}";
        string entry = $"{data}/applications/{AppId}.desktop";
        string icon = $"{data}/icons/hicolor/256x256/apps/{AppId}.png";

        try
        {
            vm.Run($"rm -rf {data}");
            Assert.Contains("absent", vm.RunInSession($"{env} {LinuxVm.Exe} --set-menu-entry status").Stdout);

            var on = vm.RunInSession($"{env} {LinuxVm.Exe} --set-menu-entry on");
            output.WriteLine(on.Output);
            Assert.Equal(0, on.ExitCode);
            Assert.Contains("shown", on.Stdout);

            var file = vm.Run($"cat {entry}").Stdout;
            output.WriteLine(file);
            Assert.Contains($"Exec=\"{LinuxVm.Exe}\"", file);
            Assert.Contains($"Icon={AppId}", file);
            Assert.Contains("NoDisplay=false", file);
            Assert.Contains($"Exec=\"{LinuxVm.Exe}\" --quit", file);    //the launcher's right-click Quit

            //The icon it names is there, and is a PNG
            Assert.Equal("PNG", vm.Run($"head -c 4 {icon} | tail -c 3").Stdout);

            //The desktop's own checker agrees it is a well-formed entry, where there is one
            if (vm.Run("command -v desktop-file-validate").ExitCode == 0)
            {
                var validation = vm.Run($"desktop-file-validate {entry}");
                Assert.True(validation.ExitCode == 0, $"desktop-file-validate: {validation.Output}");
            }

            Assert.Contains("hidden", vm.RunInSession($"{env} {LinuxVm.Exe} --set-menu-entry hidden").Stdout);
            Assert.Contains("NoDisplay=true", vm.Run($"cat {entry}").Stdout);

            Assert.Contains("absent", vm.RunInSession($"{env} {LinuxVm.Exe} --set-menu-entry off").Stdout);
            Assert.NotEqual(0, vm.Run($"test -e {entry} -o -e {icon}").ExitCode);
        }
        finally
        {
            vm.Run($"rm -rf {data}");
        }
    }

    // ------------------------------------------------------------------ the window

    //Scratch XDG folders for every start of the window: a first run each time, with no rules
    //of the VM user's to bind hotkeys for and nothing written into the real menu
    const string GuiConfig = "/tmp/skvm-gui-config", GuiData = "/tmp/skvm-gui-data";
    static string GuiEnv => $"XDG_CONFIG_HOME={GuiConfig} XDG_DATA_HOME={GuiData}";

    void StartGui(LinuxVm vm)
    {
        vm.Run("pkill -f " + ExePattern + $" ; rm -rf {GuiConfig} {GuiData} /tmp/skvm-gui.log");
        vm.RunInSession($"{GuiEnv} nohup {LinuxVm.Exe} > /tmp/skvm-gui.log 2>&1 &");

        Assert.True(WaitFor(() => Windows(vm) == 1, seconds: 20),
                    $"the window never appeared; log:\n{vm.Run("cat /tmp/skvm-gui.log").Stdout}");
    }

    void StopGui(LinuxVm vm)
    {
        vm.Run("pkill -f " + ExePattern + $" || true; rm -rf {GuiConfig} {GuiData}");
    }

    static int Processes(LinuxVm vm) => int.Parse(vm.Run("pgrep -fc " + ExePattern).Stdout.Trim());

    /// <summary>The app's windows on screen, as the window manager lists them (a hidden window isn't listed).</summary>
    static int Windows(LinuxVm vm) => int.Parse(vm.RunInSession("wmctrl -l | grep -c 'Simple KVM'").Stdout.Trim());

    static bool WaitFor(Func<bool> condition, int seconds)
    {
        for (int i = 0; i < seconds * 2; i++)
        {
            if (condition()) return true;
            Thread.Sleep(500);
        }
        return condition();
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_gui_starts_in_the_session_and_a_second_launch_defers_to_it(string machine)
    {
        var vm = Vm(machine);

        try
        {
            StartGui(vm);

            var log = vm.Run("cat /tmp/skvm-gui.log").Stdout;
            output.WriteLine(log);
            Assert.Equal(1, Processes(vm));
            Assert.DoesNotContain("Unhandled exception", log);

            //A second launch must hand over to the first and exit, leaving one process
            var second = vm.RunInSession($"{GuiEnv} timeout 10 {LinuxVm.Exe}; echo exit=$?", timeoutSeconds: 30);
            output.WriteLine(second.Output);
            Assert.Contains("exit=0", second.Stdout);
            Assert.Equal(1, Processes(vm));
        }
        finally
        {
            StopGui(vm);
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_first_run_makes_a_lower_case_data_folder_and_touches_no_menu(string machine)
    {
        var vm = Vm(machine);

        try
        {
            StartGui(vm);

            //Data lives in $XDG_CONFIG_HOME/simplekvm: lower case, and under the config home
            //even though that folder didn't exist when the app started
            output.WriteLine(vm.Run($"find {GuiConfig} {GuiData} | sort").Stdout);
            Assert.True(WaitFor(() => vm.Run($"test -d {GuiConfig}/simplekvm").ExitCode == 0, seconds: 10), $"no {GuiConfig}/simplekvm");
            Assert.NotEqual(0, vm.Run($"test -e {GuiConfig}/SimpleKVM").ExitCode);

            //The applications menu is the installer's or the user's to add the app to, not the
            //app's own: nothing appears there on its own (and this run has no hotkey rules, so
            //no portal needed the hidden entry either)
            Assert.NotEqual(0, vm.Run($"test -e {GuiData}/applications").ExitCode);
        }
        finally
        {
            StopGui(vm);
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void Closing_the_window_leaves_the_app_running_and_quit_stops_it(string machine)
    {
        var vm = Vm(machine);

        try
        {
            StartGui(vm);

            //The close button (what wmctrl -c asks the window manager for) hides the window; the rules keep running
            vm.RunInSession("wmctrl -c 'Simple KVM'");
            Assert.True(WaitFor(() => Windows(vm) == 0, seconds: 10), "the window is still there after closing it");
            Assert.Equal(1, Processes(vm));

            //Launching the app again is how the window comes back where there is no tray icon
            var again = vm.RunInSession($"{GuiEnv} timeout 10 {LinuxVm.Exe}; echo exit=$?", timeoutSeconds: 30);
            Assert.Contains("exit=0", again.Stdout);
            Assert.True(WaitFor(() => Windows(vm) == 1, seconds: 10), "launching again did not bring the window back");
            Assert.Equal(1, Processes(vm));

            //And --quit (the launcher's right-click action) is how it stops
            var quit = vm.RunInSession($"{LinuxVm.Exe} --quit");
            output.WriteLine(quit.Output);
            Assert.Equal(0, quit.ExitCode);
            Assert.True(WaitFor(() => Processes(vm) == 0, seconds: 10), "the app is still running after --quit");

            //With nothing running, --quit says so through its exit code
            Assert.Equal(1, vm.RunInSession($"{LinuxVm.Exe} --quit").ExitCode);
        }
        finally
        {
            StopGui(vm);
        }
    }

    // ------------------------------------------------------------------ the release tarball

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_release_tarball_installs_upgrades_and_uninstalls(string machine)
    {
        var vm = Vm(machine);

        const string tarball = "/simplekvm/simplekvm-linux-x64.tar.gz";
        Skip.If(vm.Run($"test -f {tarball}").ExitCode != 0, "no tarball in the shared folder; run the rig's publish.ps1");

        //A scratch home: the install goes to ~/.local/bin and the XDG folders under it
        const string home = "/tmp/skvm-install-home", unpacked = "/tmp/skvm-install-unpacked";
        string installed = $"{home}/.local/bin/simplekvm";
        string In(string command) => $"cd {unpacked}/simplekvm && env -u XDG_CONFIG_HOME -u XDG_DATA_HOME HOME={home} {command}";

        try
        {
            vm.Run("pkill -f " + ExePattern + $" ; rm -rf {home} {unpacked}; mkdir -p {home} {unpacked}");

            //The archive has to carry the executable bit: it was made on Windows, which has none
            Assert.Equal(0, vm.Run($"tar -xzf {tarball} -C {unpacked}").ExitCode);
            Assert.Equal(0, vm.Run($"test -x {unpacked}/simplekvm/simplekvm -a -x {unpacked}/simplekvm/install.sh -a -x {unpacked}/simplekvm/uninstall.sh").ExitCode);

            var install = vm.Run(In("./install.sh --startup"), timeoutSeconds: 120);
            output.WriteLine(install.Output);
            Assert.Equal(0, install.ExitCode);
            Assert.Contains($"Simple KVM is installed: {installed}", install.Stdout);

            Assert.Equal(0, vm.Run($"test -x {installed}").ExitCode);
            Assert.Contains($"Exec=\"{installed}\"", vm.Run($"cat {home}/.local/share/applications/{AppId}.desktop").Stdout);
            Assert.Contains("NoDisplay=false", vm.Run($"cat {home}/.local/share/applications/{AppId}.desktop").Stdout);
            Assert.Contains($"Exec=\"{installed}\" --minimized", vm.Run($"cat {home}/.config/autostart/{AppId}.desktop").Stdout);

            //The installed program runs, and installing again over it (an upgrade) works
            Assert.Contains("enabled", vm.Run(In($"{installed} --set-startup status"), timeoutSeconds: 60).Stdout);
            Assert.Equal(0, vm.Run(In("./install.sh"), timeoutSeconds: 120).ExitCode);

            var uninstall = vm.Run(In("./uninstall.sh"), timeoutSeconds: 60);
            output.WriteLine(uninstall.Output);
            Assert.Equal(0, uninstall.ExitCode);

            //Nothing of it is left but the runtime's own unpacking cache (and the user's settings, had there been any)
            var left = vm.Run($"find {home} -type f -not -path '*/.net/*' -not -path '*/.config/simplekvm/*'").Stdout.Trim();
            Assert.True(left.Length == 0, $"left behind after uninstalling:\n{left}");
        }
        finally
        {
            vm.Run($"rm -rf {home} {unpacked}");
        }
    }

    [SkippableTheory, MemberData(nameof(VmRig.RunningMachines), MemberType = typeof(VmRig))]
    public void The_arm64_build_starts_under_emulation(string machine)
    {
        var vm = Vm(machine);

        const string tarball = "/simplekvm/simplekvm-linux-arm64.tar.gz";
        const string arm64Root = "/usr/aarch64-linux-gnu";
        Skip.If(vm.Run($"command -v qemu-aarch64-static && test -e {arm64Root}/lib/ld-linux-aarch64.so.1").ExitCode != 0,
                "this VM has no arm64 emulation (arm64_emulation in the rig's settings.yml)");
        Skip.If(vm.Run($"test -f {tarball}").ExitCode != 0, "no arm64 tarball in the shared folder; run the rig's publish.ps1 -Arm64");

        const string unpacked = "/tmp/skvm-arm64";
        const string rules = """[{"Trigger":{"$type":"SimpleKVM.Rules.Triggers.HotkeyTrigger, SimpleKVM","HotkeyAsString":"Ctrl+Alt+F1"},"Actions":[],"RunCount":3,"Status":0,"Name":"arm64 smoke","DelaySeconds":0}]""";

        try
        {
            vm.Run($"rm -rf {unpacked}; mkdir -p {unpacked} && tar -xzf {tarball} -C {unpacked}");
            vm.Run($"cat > {unpacked}/rules.json <<'EOF'\n{rules}\nEOF");

            //Far enough to show the cross-published, trimmed binary unpacks itself and runs managed
            //code: it parses a rules file. (No arm64 ICU here, hence the invariant globalization;
            //no arm64 OpenSSL either, so nothing that hashes a monitor id.) Emulated, this takes a while.
            var result = vm.Run($"QEMU_LD_PREFIX={arm64Root} DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 timeout 300 {unpacked}/simplekvm/simplekvm --verify-rules {unpacked}/rules.json", timeoutSeconds: 330);
            output.WriteLine(result.Output);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Parsed 1 rule(s)", result.Stdout);
            Assert.Contains("arm64 smoke", result.Stdout);
        }
        finally
        {
            vm.Run($"rm -rf {unpacked}");
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

    static string IdleSource(string watchIdleOutput)
    {
        return Regex.Match(watchIdleOutput, @"^Idle source: (.+)$", RegexOptions.Multiline).Groups[1].Value.Trim();
    }

    static List<double> Samples(string watchIdleOutput)
    {
        return Regex.Matches(watchIdleOutput, @"idle: ([\d.]+) s")
                    .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                    .ToList();
    }
}
