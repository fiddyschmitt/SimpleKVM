using SimpleKVM.Platform;
using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// Global hotkeys in a Wayland session, through the XDG desktop portal's GlobalShortcuts
    /// interface (GNOME 48 and later, KDE Plasma 6, Hyprland): the desktop itself watches for
    /// the keys, so they are exclusive and no permission is needed. The price is that the
    /// desktop decides. The app only proposes a key combination per shortcut; the desktop
    /// shows the user a dialog the first time it sees a shortcut, remembers the answer, and
    /// lets the user change the keys in its own keyboard settings afterwards.
    ///
    /// A portal session takes its shortcuts once, so the hotkeys of all rules are bound
    /// together: whenever the set changes (a rule added, edited, disabled), the old session is
    /// closed and a new one bound with the whole set, a moment after the last change so that
    /// a burst of changes (every rule stopping and starting around the rule editor) costs one
    /// rebind or none. A shortcut is known to the desktop by an id made from its key
    /// combination, so a rule given a different combination is a new shortcut there, not a
    /// silent change to the keys of an old one, which desktops ignore.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public sealed class PortalHotkeys : IHotkeyBackend
    {
        const string Service = "org.freedesktop.portal.Desktop";
        const string ObjectPath = "/org/freedesktop/portal/desktop";
        const string Interface = "org.freedesktop.portal.GlobalShortcuts";

        //The part of a session's object path that is ours to choose. KDE files the shortcuts of
        //an app it can't otherwise identify under this token, so it must not change between runs.
        const string SessionToken = "simplekvm";

        static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(400);

        const int StartupPatienceMs = 25000;

        /// <summary>
        /// A held key repeats its Activated signal on GNOME. Until the keys are reported up
        /// again, anything closer together than this (the repeat delay is half a second) is
        /// the same press.
        /// </summary>
        static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(1);

        sealed class Shortcut(HotkeyGesture gesture)
        {
            public HotkeyGesture Gesture { get; } = gesture;
            public List<Action> Actions { get; } = [];
        }

        sealed record Response(uint Code, string? SessionHandle, List<(string Id, string Trigger)>? Shortcuts);

        readonly DBusConnection bus;
        readonly string requestPathPrefix;

        readonly object sync = new();
        readonly Dictionary<string, Shortcut> wanted = new(StringComparer.Ordinal);
        readonly Dictionary<string, DateTime> lastActivated = new(StringComparer.Ordinal);
        HashSet<string> bound = [];
        string? session;
        bool rebinding;
        int requests;
        string? problem;

        PortalHotkeys(DBusConnection bus)
        {
            this.bus = bus;

            //A request's reply arrives as a signal on a path built from the caller's bus name
            //(":1.42" becomes "1_42") and a token of the caller's choosing
            requestPathPrefix = $"/org/freedesktop/portal/desktop/request/{bus.UniqueName![1..].Replace('.', '_')}/";
        }

        /// <summary>The portal backend, or null when this desktop's portal has no GlobalShortcuts interface (or there is no portal).</summary>
        public static PortalHotkeys? TryCreate()
        {
            var bus = SessionBus.Connection;
            if (bus == null) return null;

            //Asking for the interface's version is the cheapest way to learn whether it exists.
            //A desktop without a portal, or a portal without the interface, says so at once.
            //The patience is for the portal that is there but not up yet: it is started on
            //demand, and at login, when the desktop starts this app, that can take seconds.
            //Giving up early would leave the whole session on the input devices.
            SessionBus.Wait(() => SessionBus.CallAsync(bus, Service, ObjectPath, "org.freedesktop.DBus.Properties", "Get",
                                                       static (Message message, object? _) => message.GetBodyReader().ReadVariantValue().GetUInt32(),
                                                       signature: "ss",
                                                       writeBody: static (ref MessageWriter writer) =>
                                                       {
                                                           writer.WriteString(Interface);
                                                           writer.WriteString("version");
                                                       }), out var error, StartupPatienceMs);
            if (error != null) return null;

            var portal = new PortalHotkeys(bus);
            portal.Introduce();
            return portal.ListenForActivations() ? portal : null;
        }

        /// <summary>
        /// Tells the portal which application this connection is. An app started from its
        /// launcher is recognised by the scope the desktop put it in; one started from a
        /// terminal or a script is nobody until it says so, and GNOME binds shortcuts for
        /// nobody. The name has to lead to a .desktop file, so that is made sure of first.
        /// </summary>
        void Introduce()
        {
            LinuxMenuEntry.EnsureExists();

            //Fails when the desktop already knows who this is, or on a portal too old to be told; both are fine
            SessionBus.Wait(async () =>
            {
                await SessionBus.CallAsync(bus, Service, ObjectPath, "org.freedesktop.host.portal.Registry", "Register",
                                           signature: "sa{sv}",
                                           writeBody: static (ref MessageWriter writer) =>
                                           {
                                               writer.WriteString(DesktopEntry.AppId);
                                               writer.WriteDictionary(new Dictionary<string, VariantValue>());
                                           }).ConfigureAwait(false);
                return true;
            }, out _);
        }

        /// <summary>Activated(o session, s shortcut, t timestamp, a{sv} options) when the keys go down, Deactivated when they come up.</summary>
        bool ListenForActivations()
        {
            return Listen("Activated", OnActivated) && Listen("Deactivated", OnDeactivated);

            bool Listen(string signal, Action<string, string> handle)
            {
                var subscription = SessionBus.Wait(async () => await bus.WatchSignalAsync(
                    Service, ObjectPath, Interface, signal,
                    static (Message message, object? _) =>
                    {
                        var reader = message.GetBodyReader();
                        return (Session: reader.ReadObjectPathAsString(), Id: reader.ReadString());
                    },
                    (Notification<(string Session, string Id)> notification) =>
                    {
                        //A handler must not throw: that would drop the bus connection
                        try { if (notification.HasValue) handle(notification.Value.Session, notification.Value.Id); } catch { }
                    },
                    ObserverFlags.None, emitOnCapturedContext: false, state: null).ConfigureAwait(false), out _);

                return subscription != null;
            }
        }

        public string? Note
        {
            get
            {
                lock (sync)
                {
                    return problem ?? "Your desktop asks you to confirm each new hotkey, and its keyboard settings can change the keys afterwards.";
                }
            }
        }

        public IDisposable Register(HotkeyGesture gesture, Action action)
        {
            //The same keys the other Linux backends accept, so a rule means the same everywhere
            if (!LinuxKeyCodes.TryGet(gesture.KeyName, out _))
                throw new ArgumentException($"Key '{gesture.KeyName}' is not supported for hotkeys on Linux");

            string id = ShortcutId(gesture);

            lock (sync)
            {
                if (!wanted.TryGetValue(id, out var shortcut)) wanted[id] = shortcut = new Shortcut(gesture);
                shortcut.Actions.Add(action);
                RebindSoon();
            }

            return new Registration(() =>
            {
                lock (sync)
                {
                    if (!wanted.TryGetValue(id, out var shortcut)) return;

                    shortcut.Actions.Remove(action);
                    if (shortcut.Actions.Count == 0) wanted.Remove(id);
                    RebindSoon();
                }
            });
        }

        /// <summary>The name the desktop keeps a shortcut under: its key combination, e.g. "ctrl-alt-f1".</summary>
        public static string ShortcutId(HotkeyGesture gesture)
        {
            return gesture.ToString().Replace('+', '-').ToLowerInvariant();
        }

        //Called with the lock held
        void RebindSoon()
        {
            if (rebinding) return;
            rebinding = true;
            _ = Task.Run(RebindLoopAsync);
        }

        async Task RebindLoopAsync()
        {
            while (true)
            {
                await Task.Delay(SettleTime).ConfigureAwait(false);

                List<Shortcut> shortcuts;
                lock (sync)
                {
                    if (bound.SetEquals(wanted.Keys))
                    {
                        rebinding = false;
                        return;
                    }

                    shortcuts = wanted.Values.ToList();
                }

                string? failure = null;
                try
                {
                    failure = await BindAsync(shortcuts).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    failure = $"The desktop's shortcut service failed: {ex.Message}";
                }

                lock (sync)
                {
                    //What was asked for counts as bound even when the desktop or the user said no:
                    //asking again unprompted would only put the same dialog back on the screen
                    bound = shortcuts.Select(s => ShortcutId(s.Gesture)).ToHashSet();
                    problem = failure;
                }

                if (failure != null) Console.WriteLine($"Hotkeys: {failure}");
            }
        }

        /// <summary>Replaces the session with one bound to the given shortcuts. Returns what the user should know went wrong, or null.</summary>
        async Task<string?> BindAsync(List<Shortcut> shortcuts)
        {
            string? previous;
            lock (sync)
            {
                previous = session;
                session = null;
            }

            if (previous != null)
            {
                try
                {
                    await SessionBus.CallAsync(bus, Service, previous, "org.freedesktop.portal.Session", "Close").ConfigureAwait(false);
                }
                catch
                {
                    //Already closed by the desktop
                }
            }

            if (shortcuts.Count == 0) return null;

            var created = await RequestAsync("CreateSession", "a{sv}", (ref MessageWriter writer, string token) =>
            {
                writer.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    ["handle_token"] = VariantValue.String(token),
                    ["session_handle_token"] = VariantValue.String(SessionToken),
                });
            }).ConfigureAwait(false);

            if (created.Code != 0 || created.SessionHandle == null)
                return "The desktop's shortcut service refused to start a session.";

            string handle = created.SessionHandle;
            lock (sync) session = handle;

            var bindReply = await RequestAsync("BindShortcuts", "oa(sa{sv})sa{sv}", (ref MessageWriter writer, string token) =>
            {
                writer.WriteObjectPath(handle);

                var array = writer.WriteArrayStart(DBusType.Struct);
                foreach (var shortcut in shortcuts)
                {
                    var properties = new Dictionary<string, VariantValue>
                    {
                        ["description"] = VariantValue.String($"Rule hotkey {shortcut.Gesture}"),
                    };
                    if (LinuxKeyCodes.ToShortcutTrigger(shortcut.Gesture) is string trigger)
                    {
                        properties["preferred_trigger"] = VariantValue.String(trigger);
                    }

                    writer.WriteStructureStart();
                    writer.WriteString(ShortcutId(shortcut.Gesture));
                    writer.WriteDictionary(properties);
                }
                writer.WriteArrayEnd(array);

                writer.WriteString("");     //no parent window: the rules bind their hotkeys whether or not the window is open
                writer.WriteDictionary(new Dictionary<string, VariantValue> { ["handle_token"] = VariantValue.String(token) });
            }).ConfigureAwait(false);

            //GNOME answers "other" for shortcuts it already knew, yet lists them as bound, so
            //what counts is what the session holds afterwards
            var bindings = bindReply.Shortcuts;
            if (bindings == null)
            {
                var listed = await RequestAsync("ListShortcuts", "oa{sv}", (ref MessageWriter writer, string token) =>
                {
                    writer.WriteObjectPath(handle);
                    writer.WriteDictionary(new Dictionary<string, VariantValue> { ["handle_token"] = VariantValue.String(token) });
                }).ConfigureAwait(false);

                bindings = listed.Shortcuts ?? [];
            }

            var withKeys = bindings.Where(b => b.Trigger.Length > 0).ToDictionary(b => b.Id, b => b.Trigger);
            var without = new List<string>();
            foreach (var shortcut in shortcuts)
            {
                //Said on the console (the journal, for a copy the desktop started): the keys are the desktop's call, not the rule's
                if (withKeys.TryGetValue(ShortcutId(shortcut.Gesture), out var keys)) Console.WriteLine($"Hotkeys: the desktop bound {shortcut.Gesture} to \"{keys}\"");
                else without.Add(shortcut.Gesture.ToString());
            }

            if (without.Count == 0) return null;

            return bindReply.Code == 1
                ? "The desktop's request to add the hotkeys was cancelled, so they are not active. Disable and enable the rule to be asked again."
                : $"The desktop has no keys assigned to {string.Join(", ", without)}. Assign them in its keyboard settings, under this app's global shortcuts.";
        }

        delegate void RequestWriter(ref MessageWriter writer, string handleToken);

        /// <summary>
        /// A portal request: the method returns at once with the path of a request object, and
        /// the answer comes later (after the user has dealt with a dialog, if there is one) as
        /// that object's Response signal. The signal is listened for before the call is made,
        /// so an answer can't slip through between the two.
        /// </summary>
        async Task<Response> RequestAsync(string method, string signature, RequestWriter writeBody)
        {
            string token = $"simplekvm{Interlocked.Increment(ref requests)}";
            string requestPath = requestPathPrefix + token;

            var answer = new TaskCompletionSource<Response>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var subscription = await bus.WatchSignalAsync(
                Service, requestPath, "org.freedesktop.portal.Request", "Response",
                static (Message message, object? _) => ReadResponse(message),
                (Notification<Response> notification) =>
                {
                    if (notification.HasValue) answer.TrySetResult(notification.Value);
                    else if (notification.IsCompletion) answer.TrySetException(new InvalidOperationException("the session bus connection closed"));
                },
                ObserverFlags.EmitOnConnectionClosed, emitOnCapturedContext: false, state: null).ConfigureAwait(false);

            await SessionBus.CallAsync(bus, Service, ObjectPath, Interface, method,
                                       static (Message message, object? _) => message.GetBodyReader().ReadObjectPathAsString(),
                                       signature, (ref MessageWriter writer) => writeBody(ref writer, token)).ConfigureAwait(false);

            return await answer.Task.ConfigureAwait(false);
        }

        /// <summary>Response(u code, a{sv} results): 0 is success, 1 the user cancelling, 2 anything else.</summary>
        static Response ReadResponse(Message message)
        {
            var reader = message.GetBodyReader();
            uint code = reader.ReadUInt32();
            var results = reader.ReadDictionaryOfStringToVariantValue();

            string? sessionHandle = results.TryGetValue("session_handle", out var handle) ? Text(handle) : null;

            //shortcuts: a(sa{sv}), each an id and its properties, of which trigger_description names the keys (empty when there are none)
            List<(string, string)>? shortcuts = null;
            if (results.TryGetValue("shortcuts", out var list) && Unwrap(list) is { Type: VariantValueType.Array } array)
            {
                shortcuts = [];
                for (int i = 0; i < array.Count; i++)
                {
                    var entry = Unwrap(array.GetItem(i));
                    if (entry.Type != VariantValueType.Struct || entry.Count < 2) continue;

                    string trigger = "";
                    var properties = Unwrap(entry.GetItem(1));
                    if (properties.Type == VariantValueType.Dictionary)
                    {
                        for (int p = 0; p < properties.Count; p++)
                        {
                            var property = properties.GetDictionaryEntry(p);
                            if (Text(property.Key) == "trigger_description") trigger = Text(property.Value) ?? "";
                        }
                    }

                    shortcuts.Add((Text(entry.GetItem(0)) ?? "", trigger));
                }
            }

            return new Response(code, sessionHandle, shortcuts);
        }

        static VariantValue Unwrap(VariantValue value)
        {
            while (value.Type == VariantValueType.Variant) value = value.GetVariantValue();
            return value;
        }

        static string? Text(VariantValue value)
        {
            value = Unwrap(value);
            return value.Type switch
            {
                VariantValueType.String => value.GetString(),
                VariantValueType.ObjectPath => value.GetObjectPathAsString(),
                _ => null,
            };
        }

        //On the bus's reader thread
        void OnActivated(string sessionHandle, string id)
        {
            List<Action> toFire;
            lock (sync)
            {
                if (sessionHandle != session || !wanted.TryGetValue(id, out var shortcut)) return;

                var now = DateTime.UtcNow;
                bool repeat = lastActivated.TryGetValue(id, out var previous) && now - previous < RepeatWindow;
                lastActivated[id] = now;
                if (repeat) return;

                toFire = shortcut.Actions.Distinct().ToList();
            }

            //Off this thread: a monitor switch takes a few hundred ms of DDC traffic
            foreach (var action in toFire)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { action(); } catch (Exception ex) { Console.WriteLine($"Hotkey action failed: {ex.Message}"); }
                });
            }
        }

        void OnDeactivated(string sessionHandle, string id)
        {
            lock (sync) lastActivated.Remove(id);
        }

        sealed class Registration(Action onDispose) : IDisposable
        {
            int disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) onDispose();
            }
        }
    }
}
