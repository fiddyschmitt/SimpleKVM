using SimpleKVM.Input.linux;
using SimpleKVM.Platform;
using SimpleKVM.Platform.linux;
using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace SimpleKVM.Utilities.linux
{
    /// <summary>
    /// Idle time on Linux, from whichever of these the machine offers, in this order:
    /// GNOME's Mutter, which announces when the user goes idle and comes back; the
    /// freedesktop ScreenSaver interface (KDE on X11), asked once a second; and the input
    /// event devices, readable only by a user in the <c>input</c> group but there on every
    /// desktop. KDE Plasma on Wayland has neither D-Bus interface, so there the devices are
    /// the only source. SIMPLEKVM_IDLE_SOURCE=desktop or =evdev pins one for troubleshooting.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxIdle : IIdleProvider
    {
        /// <summary>
        /// What "idle" is asked of the desktop as: the same second the no-longer-idle trigger
        /// wants before input counts as a return (NoLongerIdle.IdleThreshold).
        /// </summary>
        public static readonly TimeSpan DesktopIdleThreshold = TimeSpan.FromSeconds(1);

        readonly MutterIdleMonitor mutter = new();
        readonly ScreenSaverIdle screenSaver = new();
        readonly string? pinned = Environment.GetEnvironmentVariable("SIMPLEKVM_IDLE_SOURCE")?.ToLowerInvariant();

        /// <summary>Which source answered last: "Mutter IdleMonitor", "freedesktop ScreenSaver", "evdev", or null when none has.</summary>
        public string? SourceName { get; private set; }

        /// <summary>Set once no source is left: nothing on the session bus reports idle time and /dev/input can't be read.</summary>
        public string? StatusMessage { get; private set; }

        public TimeSpan GetIdleTimeSpan()
        {
            if (pinned != "evdev")
            {
                if (mutter.TryGetIdle(out var idle))
                {
                    return Answer("Mutter IdleMonitor", idle);
                }

                if (screenSaver.TryGetIdle(out idle))
                {
                    return Answer("freedesktop ScreenSaver", idle);
                }
            }

            if (pinned != "desktop" && EvdevInput.Instance.ReadableDeviceCount > 0)
            {
                return Answer("evdev", EvdevInput.Instance.IdleTime);
            }

            SourceName = null;
            StatusMessage = mutter.Missing && screenSaver.Missing
                ? "Idle detection needs read access to /dev/input (add your user to the input group and log in again): this desktop doesn't report idle time over D-Bus."
                : null;     //a desktop source that merely didn't answer this time is asked again

            return TimeSpan.Zero;
        }

        TimeSpan Answer(string source, TimeSpan idle)
        {
            SourceName = source;
            StatusMessage = null;
            return idle;
        }

        /// <summary>
        /// A D-Bus source that is either there or not. A call that fails for another reason
        /// (a slow desktop at login) is tried again after a pause instead of being written off.
        /// </summary>
        abstract class DesktopSource
        {
            static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

            protected readonly object Sync = new();
            DateTime retryAt = DateTime.MinValue;

            /// <summary>The desktop has no such interface; never asked again.</summary>
            public bool Missing { get; private set; }

            protected bool MayAsk(out DBusConnection? bus)
            {
                bus = null;
                if (Missing || DateTime.UtcNow < retryAt) return false;

                bus = SessionBus.Connection;
                if (bus == null)
                {
                    //No session bus at all: there is no desktop to ask, now or later
                    Missing = true;
                    return false;
                }

                return true;
            }

            protected void Failed(Exception? error)
            {
                if (SessionBus.IsMissing(error)) Missing = true;
                else retryAt = DateTime.UtcNow + RetryInterval;
            }
        }

        /// <summary>
        /// <c>org.gnome.Mutter.IdleMonitor</c>: one idle watch at the threshold, which fires
        /// each time the user has been idle that long, and after each a one-shot user-active
        /// watch, which fires on the next input. Between the two the state is known without
        /// asking. A direct reading every so often catches anything missed (the shell
        /// restarting forgets the watches) and sets them up again.
        /// </summary>
        sealed class MutterIdleMonitor : DesktopSource
        {
            const string Service = "org.gnome.Mutter.IdleMonitor";
            const string ObjectPath = "/org/gnome/Mutter/IdleMonitor/Core";
            const string Interface = "org.gnome.Mutter.IdleMonitor";

            static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
            static readonly TimeSpan Slack = TimeSpan.FromSeconds(1.5);

            readonly IdleWatchState state = new(DesktopIdleThreshold);
            IDisposable? subscription;
            uint idleWatch;
            bool watching;
            bool checking;
            DateTime lastCheck;

            public bool TryGetIdle(out TimeSpan idle)
            {
                lock (Sync)
                {
                    idle = default;

                    if (!watching)
                    {
                        if (!MayAsk(out var bus) || !Start(bus!)) return false;
                    }
                    else if (!checking && DateTime.UtcNow - lastCheck >= CheckInterval && SessionBus.Connection is DBusConnection connection)
                    {
                        checking = true;
                        _ = CheckAsync(connection);
                    }

                    idle = state.IdleAt(DateTime.UtcNow);
                    return true;
                }
            }

            //Called with the lock held
            bool Start(DBusConnection bus)
            {
                var reading = SessionBus.Wait(() => ReadIdleAsync(bus), out var error);
                if (error != null)
                {
                    Failed(error);
                    return false;
                }

                subscription ??= SessionBus.Wait(async () => await bus.WatchSignalAsync(
                    Service, ObjectPath, Interface, "WatchFired",
                    static (Message message, object? _) => message.GetBodyReader().ReadUInt32(),
                    (Notification<uint> notification) =>
                    {
                        //A handler must not throw: that would drop the bus connection
                        try { if (notification.HasValue) OnWatchFired(bus, notification.Value); } catch { }
                    },
                    ObserverFlags.None, emitOnCapturedContext: false, state: null).ConfigureAwait(false), out error);

                if (subscription == null)
                {
                    Failed(error);
                    return false;
                }

                uint added = SessionBus.Wait(() => AddIdleWatchAsync(bus), out error);
                if (error != null)
                {
                    Failed(error);
                    return false;
                }

                idleWatch = added;
                state.Sync(reading, DateTime.UtcNow);
                if (state.IsIdle) _ = AddUserActiveWatchAsync(bus);

                lastCheck = DateTime.UtcNow;
                watching = true;
                return true;
            }

            //On the bus's reader thread: no waiting here
            void OnWatchFired(DBusConnection bus, uint id)
            {
                lock (Sync)
                {
                    if (id == idleWatch)
                    {
                        //Fires at once when the watch was added during an idle spell, which
                        //already has its user-active watch
                        bool alreadyIdle = state.IsIdle;
                        state.BecameIdle(DateTime.UtcNow);
                        if (!alreadyIdle) _ = AddUserActiveWatchAsync(bus);
                    }
                    else if (id > idleWatch)
                    {
                        //Mutter numbers watches in the order they are added, so a later one is a
                        //user-active watch; an earlier one is left over from before the idle
                        //watch was replaced and says nothing new
                        state.BecameActive();
                    }
                }
            }

            async Task CheckAsync(DBusConnection bus)
            {
                try
                {
                    var reading = await ReadIdleAsync(bus).ConfigureAwait(false);

                    bool contradicted;
                    lock (Sync) contradicted = state.Contradicts(reading, DateTime.UtcNow, Slack);

                    if (contradicted)
                    {
                        //Missed announcements: set the watches up afresh and start from the reading
                        uint added = await AddIdleWatchAsync(bus).ConfigureAwait(false);
                        reading = await ReadIdleAsync(bus).ConfigureAwait(false);

                        lock (Sync)
                        {
                            uint stale = idleWatch;
                            idleWatch = added;
                            state.Sync(reading, DateTime.UtcNow);
                            if (state.IsIdle) _ = AddUserActiveWatchAsync(bus);
                            _ = RemoveWatchAsync(bus, stale);
                        }
                    }
                }
                catch
                {
                    //Asked again at the next check; if Mutter is gone for good the state simply stays as it is
                }
                finally
                {
                    lock (Sync)
                    {
                        lastCheck = DateTime.UtcNow;
                        checking = false;
                    }
                }
            }

            static async Task<TimeSpan> ReadIdleAsync(DBusConnection bus)
            {
                ulong milliseconds = await SessionBus.CallAsync(bus, Service, ObjectPath, Interface, "GetIdletime",
                                                                static (Message message, object? _) => message.GetBodyReader().ReadUInt64()).ConfigureAwait(false);
                return TimeSpan.FromMilliseconds(milliseconds);
            }

            static Task<uint> AddIdleWatchAsync(DBusConnection bus)
            {
                return SessionBus.CallAsync(bus, Service, ObjectPath, Interface, "AddIdleWatch",
                                            static (Message message, object? _) => message.GetBodyReader().ReadUInt32(),
                                            signature: "t",
                                            writeBody: static (ref MessageWriter writer) => writer.WriteUInt64((ulong)DesktopIdleThreshold.TotalMilliseconds));
            }

            static async Task AddUserActiveWatchAsync(DBusConnection bus)
            {
                try
                {
                    await SessionBus.CallAsync(bus, Service, ObjectPath, Interface, "AddUserActiveWatch",
                                               static (Message message, object? _) => message.GetBodyReader().ReadUInt32()).ConfigureAwait(false);
                }
                catch
                {
                    //The periodic check notices a return that was never announced
                }
            }

            static async Task RemoveWatchAsync(DBusConnection bus, uint id)
            {
                try
                {
                    await SessionBus.CallAsync(bus, Service, ObjectPath, Interface, "RemoveWatch",
                                               signature: "u", writeBody: (ref MessageWriter writer) => writer.WriteUInt32(id)).ConfigureAwait(false);
                }
                catch
                {
                    //Already gone, which is what was wanted
                }
            }
        }

        /// <summary>
        /// <c>org.freedesktop.ScreenSaver.GetSessionIdleTime</c>, in milliseconds (KDE answers
        /// so despite the spec's wording). It can only be asked, so it is asked at most once a
        /// second and the answer extrapolated in between; the next answer shows the reset.
        /// </summary>
        sealed class ScreenSaverIdle : DesktopSource
        {
            static readonly TimeSpan QueryInterval = TimeSpan.FromSeconds(1);

            TimeSpan lastReported;
            DateTime lastQueryAt = DateTime.MinValue;
            bool haveReport;

            public bool TryGetIdle(out TimeSpan idle)
            {
                lock (Sync)
                {
                    idle = default;

                    var now = DateTime.UtcNow;
                    if (now - lastQueryAt >= QueryInterval && MayAsk(out var bus))
                    {
                        uint milliseconds = SessionBus.Wait(() => SessionBus.CallAsync(bus!, "org.freedesktop.ScreenSaver", "/org/freedesktop/ScreenSaver", "org.freedesktop.ScreenSaver", "GetSessionIdleTime",
                                                                                          static (Message message, object? _) => message.GetBodyReader().ReadUInt32()), out var error);
                        if (error == null)
                        {
                            lastReported = TimeSpan.FromMilliseconds(milliseconds);
                            lastQueryAt = DateTime.UtcNow;
                            haveReport = true;
                        }
                        else
                        {
                            Failed(error);
                            if (Missing) haveReport = false;
                        }
                    }

                    if (!haveReport) return false;

                    idle = lastReported + (DateTime.UtcNow - lastQueryAt);
                    return true;
                }
            }
        }
    }
}
