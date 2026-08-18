using Newtonsoft.Json;
using SimpleKVM.Configuration;
using SimpleKVM.Rules;

namespace SimpleKVM.Tests;

// TypeNameHandling in rules.json is an RCE vector; this binder is the gate that only lets types
// from the app's own assembly (plus collections/primitives) through.
public class SafeSerializationBinderTests
{
    static readonly SafeSerializationBinder Binder = SafeSerializationBinder.Instance;

    [Fact]
    public void Allows_a_type_from_the_app_assembly()
    {
        var t = typeof(Rule);
        var bound = Binder.BindToType(t.Assembly.GetName().Name, t.FullName!);
        Assert.Equal(typeof(Rule), bound);
    }

    [Fact]
    public void Rejects_a_resolvable_type_from_another_assembly()
    {
        // JsonConvert is definitely loaded and resolvable, and definitely not ours -> refused.
        var foreign = typeof(JsonConvert);
        var ex = Assert.Throws<JsonSerializationException>(
            () => Binder.BindToType(foreign.Assembly.GetName().Name, foreign.FullName!));
        Assert.Contains("Refusing to deserialize", ex.Message);
    }

    [Theory]
    [InlineData("System.Int32")]
    [InlineData("System.String")]
    [InlineData("System.DateTime")]
    public void Allows_primitives_string_and_datetime(string typeName)
    {
        // These carry no code, so they are safe scalar payloads and must pass.
        var bound = Binder.BindToType(null, typeName);
        Assert.NotNull(bound);
    }

    [Fact]
    public void Maps_the_other_platforms_Monitor_type_to_this_platforms()
    {
        // A rules.json written on the other OS names that OS's concrete Monitor; the binder
        // normalises it to the one that works here (the id-carrier is re-resolved at run time).
        var asm = typeof(Rule).Assembly.GetName().Name;
        var (foreign, native) = OperatingSystem.IsWindows()
            ? ("SimpleKVM.Displays.mac.Monitor", "SimpleKVM.Displays.win.Monitor")
            : ("SimpleKVM.Displays.win.Monitor", "SimpleKVM.Displays.mac.Monitor");

        var bound = Binder.BindToType(asm, foreign);

        Assert.Equal(native, bound.FullName);
    }

    [Fact]
    public void Binds_this_platforms_own_Monitor_type_unchanged()
    {
        var asm = typeof(Rule).Assembly.GetName().Name;
        var native = OperatingSystem.IsWindows()
            ? "SimpleKVM.Displays.win.Monitor"
            : "SimpleKVM.Displays.mac.Monitor";

        var bound = Binder.BindToType(asm, native);

        Assert.Equal(native, bound.FullName);
    }
}
