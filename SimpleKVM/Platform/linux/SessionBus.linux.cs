using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace SimpleKVM.Platform.linux
{
    /// <summary>
    /// The app's connection to the desktop session's D-Bus, shared by everything that talks to
    /// the desktop: Mutter's display and idle interfaces, the freedesktop screen saver, and the
    /// XDG portals (which identify an app by its connection, so there must be exactly one).
    /// Spoken with Tmds.DBus.Protocol, the library Avalonia itself brings along for the tray
    /// icon, so nothing is spawned and signals can be listened for.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static class SessionBus
    {
        public const int CallTimeoutMs = 3000;

        static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);

        static readonly object sync = new();
        static DBusConnection? connection;
        static DateTime nextAttempt = DateTime.MinValue;

        public delegate void BodyWriter(ref MessageWriter writer);

        /// <summary>
        /// The connection, or null when there is no session bus to reach (a plain SSH shell, a
        /// system service). A failed attempt is not repeated for a while: callers poll.
        /// </summary>
        public static DBusConnection? Connection
        {
            get
            {
                lock (sync)
                {
                    if (connection != null) return connection;
                    if (DateTime.UtcNow < nextAttempt) return null;

                    try
                    {
                        var address = DBusAddress.Session;
                        if (string.IsNullOrEmpty(address)) throw new InvalidOperationException("no session bus address");

                        var candidate = new DBusConnection(address);
                        if (!candidate.ConnectAsync().AsTask().Wait(CallTimeoutMs))
                        {
                            candidate.Dispose();
                            throw new TimeoutException("the session bus did not answer");
                        }

                        connection = candidate;
                    }
                    catch
                    {
                        nextAttempt = DateTime.UtcNow + RetryInterval;
                    }

                    return connection;
                }
            }
        }

        /// <summary>Calls a method and reads its reply. Throws what the bus throws; see <see cref="IsMissing"/>.</summary>
        public static Task<T> CallAsync<T>(DBusConnection bus, string destination, string path, string @interface, string member,
                                           MessageValueReader<T> readReply, string? signature = null, BodyWriter? writeBody = null)
        {
            return bus.CallMethodAsync(CreateCall(bus, destination, path, @interface, member, signature, writeBody), readReply, null);
        }

        /// <summary>Calls a method whose reply carries nothing of interest.</summary>
        public static Task CallAsync(DBusConnection bus, string destination, string path, string @interface, string member,
                                     string? signature = null, BodyWriter? writeBody = null)
        {
            return bus.CallMethodAsync(CreateCall(bus, destination, path, @interface, member, signature, writeBody));
        }

        static MessageBuffer CreateCall(DBusConnection bus, string destination, string path, string @interface, string member, string? signature, BodyWriter? writeBody)
        {
            var writer = bus.GetMessageWriter();
            try
            {
                writer.WriteMethodCallHeader(destination: destination, path: path, @interface: @interface, member: member, signature: signature);
                writeBody?.Invoke(ref writer);
                return writer.CreateMessage();
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// Waits for a call from synchronous code. Default when it fails or takes longer than the
        /// timeout; <paramref name="error"/> then says which (null for a timeout).
        /// </summary>
        public static T? Wait<T>(Func<Task<T>> call, out Exception? error, int timeoutMs = CallTimeoutMs)
        {
            error = null;
            try
            {
                //Started on the pool so nothing in the call can be captured by a UI thread's
                //synchronization context, which this thread would then be blocking
                var task = Task.Run(call);
                if (task.Wait(timeoutMs)) return task.Result;

                error = new TimeoutException("the desktop did not answer in time");
                return default;
            }
            catch (AggregateException ex)
            {
                error = ex.InnerException ?? ex;
                return default;
            }
            catch (Exception ex)
            {
                error = ex;
                return default;
            }
        }

        /// <summary>
        /// True when the error says the thing asked for isn't there at all (no such service,
        /// interface or method, or the desktop saying it doesn't support it), as opposed to a
        /// call that merely failed this time.
        /// </summary>
        public static bool IsMissing(Exception? error)
        {
            return error is DBusErrorReplyException reply && reply.ErrorName is
                "org.freedesktop.DBus.Error.ServiceUnknown" or
                "org.freedesktop.DBus.Error.NameHasNoOwner" or
                "org.freedesktop.DBus.Error.UnknownMethod" or
                "org.freedesktop.DBus.Error.UnknownInterface" or
                "org.freedesktop.DBus.Error.UnknownObject" or
                "org.freedesktop.DBus.Error.NotSupported" or
                "org.freedesktop.DBus.Error.AccessDenied";
        }
    }
}
