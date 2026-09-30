using Renci.SshNet;
using System.Collections.Concurrent;

namespace SimpleKVM.SystemTests;

/// <summary>
/// One of the rig's desktop VMs, driven over SSH. Deploys the Linux build out of the shared
/// folder into the guest, finds the auto-logged-in desktop session's environment, and runs
/// commands either plainly, inside that session's environment, or as root.
/// </summary>
public sealed class LinuxVm : IDisposable
{
    static readonly ConcurrentDictionary<string, LinuxVm> connected = new();

    /// <summary>Where the build is copied to inside the guest (never run it from the vboxsf share).</summary>
    public const string DeployDir = "/home/vagrant/simplekvm";
    public const string Exe = DeployDir + "/SimpleKVM";

    readonly SshClient ssh;

    public MachineSpec Machine { get; }
    public string Build { get; private set; } = "";

    /// <summary>The desktop session's type as logind reports it: "wayland" or "x11".</summary>
    public string SessionType { get; private set; } = "";
    public string Display { get; private set; } = "";

    LinuxVm(MachineSpec machine)
    {
        Machine = machine;

        var auth = new PrivateKeyAuthenticationMethod(machine.User, new PrivateKeyFile(machine.KeyFile));
        var info = new ConnectionInfo(machine.Host, machine.Port, machine.User, auth) { Timeout = TimeSpan.FromSeconds(15) };
        ssh = new SshClient(info) { KeepAliveInterval = TimeSpan.FromSeconds(15) };
        ssh.Connect();
    }

    /// <summary>Connects (once per machine per test run), deploys the build, and waits for the desktop session.</summary>
    public static LinuxVm Get(MachineSpec machine)
    {
        return connected.GetOrAdd(machine.Name, _ =>
        {
            var vm = new LinuxVm(machine);
            vm.Deploy();
            vm.WaitForSession(TimeSpan.FromMinutes(3));
            return vm;
        });
    }

    public sealed record Result(int ExitCode, string Stdout, string Stderr)
    {
        public string Output => Stdout + (Stderr.Length > 0 ? "\n[stderr]\n" + Stderr : "");
    }

    public Result Run(string command, int timeoutSeconds = 60)
    {
        using var cmd = ssh.CreateCommand(command);
        cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        var stdout = cmd.Execute();
        return new Result((int?)cmd.ExitStatus ?? -1, stdout ?? "", cmd.Error ?? "");
    }

    /// <summary>Runs a command as the vagrant user with the desktop session's environment, so D-Bus and the display are reachable.</summary>
    public Result RunInSession(string command, int timeoutSeconds = 60)
    {
        return Run($"{SessionEnv} bash -lc {Quote(command)}", timeoutSeconds);
    }

    public Result Sudo(string command, int timeoutSeconds = 60)
    {
        return Run($"sudo -n bash -c {Quote(command)}", timeoutSeconds);
    }

    public string SessionEnv =>
        $"env XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus XDG_SESSION_TYPE={SessionType} " +
        $"XDG_CURRENT_DESKTOP={(Machine.Desktop == "kde" ? "KDE" : "GNOME")} WAYLAND_DISPLAY=wayland-0 DISPLAY={Display} HOME=/home/vagrant";

    public static string Quote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    void Deploy()
    {
        var result = Run($"test -f /simplekvm/SimpleKVM && mkdir -p {DeployDir} && cp /simplekvm/SimpleKVM {Exe} && chmod +x {Exe} && cat /simplekvm/BUILD.txt 2>/dev/null");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{Machine.Name}: no Linux build in the shared folder; run publish.ps1 in the rig first ({result.Output})");
        Build = result.Stdout.Trim();
    }

    void WaitForSession(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string last = "";
        while (DateTime.UtcNow < deadline)
        {
            //The auto-logged-in graphical session for vagrant on seat0, with its session bus up
            var probe = Run(
                "id=$(loginctl list-sessions --no-legend | awk '$3==\"vagrant\" && $4==\"seat0\" {print $1; exit}'); " +
                "[ -n \"$id\" ] && [ -S /run/user/1000/bus ] && loginctl show-session \"$id\" -p Type -p Display --value | tr '\\n' ' '");
            last = probe.Stdout.Trim();
            var parts = last.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (probe.ExitCode == 0 && parts.Length >= 1 && parts[0] is "wayland" or "x11")
            {
                SessionType = parts[0];
                Display = parts.Length >= 2 ? parts[1] : (SessionType == "x11" ? ":0" : "");
                //X11 sessions leave Display empty in logind sometimes; find the socket instead
                if (Display.Length == 0)
                {
                    var sockets = Run("ls /tmp/.X11-unix/ 2>/dev/null | sed 's/X/:/' | tail -1").Stdout.Trim();
                    Display = sockets.Length > 0 ? sockets : ":0";
                }
                return;
            }
            Thread.Sleep(3000);
        }
        throw new TimeoutException($"{Machine.Name}: no desktop session for vagrant within {timeout} (last: '{last}'); did you `vagrant reload` after the first provision?");
    }

    public void Dispose()
    {
        ssh.Dispose();
    }
}
