using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SimpleKVM.SystemTests;

/// <summary>
/// The Linux desktop VMs from the provisioning rig next to the repo: which are defined (from
/// settings.yml), which are running (from vagrant status), and how to reach each one (from
/// vagrant ssh-config). Discovered once per test run and shared by every test.
/// </summary>
public sealed class VmRig
{
    public static readonly VmRig Instance = Discover();

    /// <summary>Why the rig is unavailable, or null when at least one VM is running.</summary>
    public string? Unavailable { get; private init; }

    public string? RigDir { get; private init; }

    public IReadOnlyList<MachineSpec> Machines { get; private init; } = [];

    /// <summary>The running machines, or a single placeholder row so a theory has data to skip on.</summary>
    public static IEnumerable<object[]> RunningMachines =>
        Instance.Machines.Count > 0
            ? Instance.Machines.Select(m => new object[] { m.Name })
            : [[ "(no VM running)" ]];

    public MachineSpec? Find(string name) => Machines.FirstOrDefault(m => m.Name == name);

    static VmRig Discover()
    {
        try
        {
            var rigDir = Environment.GetEnvironmentVariable("SIMPLEKVM_VM_DIR") ?? DefaultRigDir();
            if (rigDir == null || !File.Exists(Path.Combine(rigDir, "Vagrantfile")))
                return new VmRig { Unavailable = $"no provisioning rig at {rigDir ?? "(unknown)"}; set SIMPLEKVM_VM_DIR" };

            var specs = ReadSettings(Path.Combine(rigDir, "settings.yml"));

            var status = Vagrant(rigDir, "status --machine-readable", timeoutMs: 60000);
            if (status == null) return new VmRig { RigDir = rigDir, Unavailable = "vagrant is not installed or did not answer" };

            var running = Regex.Matches(status, @"^\d+,([^,]+),state,running", RegexOptions.Multiline)
                               .Select(m => m.Groups[1].Value)
                               .ToHashSet();

            var machines = new List<MachineSpec>();
            foreach (var spec in specs.Where(s => running.Contains(s.Name)))
            {
                var sshConfig = Vagrant(rigDir, $"ssh-config {spec.Name}", timeoutMs: 60000);
                if (sshConfig == null) continue;

                string? Value(string key) => Regex.Match(sshConfig, $@"^\s*{key}\s+""?([^""\r\n]+)""?", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
                var host = Value("HostName");
                var port = Value("Port");
                var user = Value("User");
                var key = Value("IdentityFile");
                if (host == null || port == null || user == null || key == null) continue;

                machines.Add(spec with { Host = host, Port = int.Parse(port), User = user, KeyFile = key });
            }

            return new VmRig
            {
                RigDir = rigDir,
                Machines = machines,
                Unavailable = machines.Count == 0 ? $"no VM from {rigDir} is running (vagrant up there first)" : null,
            };
        }
        catch (Exception ex)
        {
            return new VmRig { Unavailable = $"rig discovery failed: {ex.Message}" };
        }
    }

    /// <summary>..\SimpleKVM local\provisioning\linux, relative to the repo root (found by walking up to the .sln).</summary>
    static string? DefaultRigDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SimpleKVM.sln"))) dir = dir.Parent;
        if (dir?.Parent == null) return null;
        return Path.Combine(dir.Parent.FullName, "SimpleKVM local", "provisioning", "linux");
    }

    /// <summary>The machines: list of settings.yml: name, desktop, session, input_group. A line-based read, enough for that shape.</summary>
    static List<MachineSpec> ReadSettings(string path)
    {
        var result = new List<MachineSpec>();
        MachineSpec? current = null;
        bool inMachines = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Split('#')[0].TrimEnd();
            if (line.Length == 0) continue;

            if (!line.StartsWith(' ')) { inMachines = line.StartsWith("machines:"); continue; }
            if (!inMachines) continue;

            var text = line.TrimStart();
            if (text.StartsWith("- "))
            {
                if (current != null) result.Add(current);
                current = new MachineSpec("");
                text = text[2..];
            }

            var colon = text.IndexOf(':');
            if (current == null || colon < 0) continue;
            var key = text[..colon].Trim();
            var value = text[(colon + 1)..].Trim().Trim('"');

            current = key switch
            {
                "name" => current with { Name = value },
                "desktop" => current with { Desktop = value },
                "session" => current with { Session = value },
                "input_group" => current with { InputGroup = value.Equals("true", StringComparison.OrdinalIgnoreCase) },
                _ => current,
            };
        }
        if (current != null) result.Add(current);

        return result.Where(m => m.Name.Length > 0).ToList();
    }

    static string? Vagrant(string rigDir, string arguments, int timeoutMs)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("vagrant", arguments)
            {
                WorkingDirectory = rigDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process == null) return null;

            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs)) { try { process.Kill(true); } catch { } return null; }

            return process.ExitCode == 0 ? stdout.Result : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record MachineSpec(string Name)
{
    public string Desktop { get; init; } = "";
    public string Session { get; init; } = "";
    public bool InputGroup { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string User { get; init; } = "";
    public string KeyFile { get; init; } = "";

    public override string ToString() => $"{Name} ({Desktop}/{Session}, input group: {(InputGroup ? "yes" : "no")})";
}
