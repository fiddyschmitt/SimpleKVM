using SimpleKVM.Input.linux;
using SimpleKVM.Platform;
using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace SimpleKVM.Utilities.linux
{
    /// <summary>
    /// Idle time on Linux. When the input event devices are readable (the user is in the
    /// <c>input</c> group) it comes straight from them, on any desktop, Wayland included.
    /// Otherwise the desktop is asked over D-Bus: GNOME's Mutter IdleMonitor, then the
    /// freedesktop ScreenSaver interface (KDE). Those go through a <c>gdbus</c> process, so
    /// the answer is refreshed at most once a second and extrapolated in between; the
    /// no-longer-idle trigger polls ten times a second and only needs to see the drop.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public class LinuxIdle : IIdleProvider
    {
        static readonly TimeSpan QueryInterval = TimeSpan.FromSeconds(1);

        sealed record DbusQuery(string Name, string Arguments, bool RepliesInSeconds);

        static readonly DbusQuery[] Queries =
        [
            new("Mutter IdleMonitor",
                "call --session --dest org.gnome.Mutter.IdleMonitor --object-path /org/gnome/Mutter/IdleMonitor/Core --method org.gnome.Mutter.IdleMonitor.GetIdletime",
                RepliesInSeconds: false),
            new("freedesktop ScreenSaver",
                "call --session --dest org.freedesktop.ScreenSaver --object-path /org/freedesktop/ScreenSaver --method org.freedesktop.ScreenSaver.GetSessionIdleTime",
                RepliesInSeconds: false),   //KDE answers in milliseconds despite the spec's wording
        ];

        readonly object sync = new();
        readonly HashSet<string> failedQueries = [];
        DbusQuery? workingQuery;
        TimeSpan lastReported;
        DateTime lastQueryAt = DateTime.MinValue;
        bool haveReport;

        /// <summary>Which source answered last: "evdev", a D-Bus interface name, or null when nothing has.</summary>
        public string? Source { get; private set; }

        /// <summary>
        /// Set once every D-Bus interface has refused. KDE Plasma on Wayland answers the
        /// ScreenSaver call with "not supported", so there only /dev/input can tell.
        /// </summary>
        public string? StatusMessage
        {
            get
            {
                lock (sync)
                {
                    return Source == null && failedQueries.Count == Queries.Length
                        ? "Idle detection needs read access to /dev/input (add your user to the input group and log in again): this desktop doesn't report idle time over D-Bus."
                        : null;
                }
            }
        }

        public TimeSpan GetIdleTimeSpan()
        {
            if (EvdevInput.Instance.ReadableDeviceCount > 0)
            {
                Source = "evdev";
                return EvdevInput.Instance.IdleTime;
            }

            lock (sync)
            {
                var now = DateTime.UtcNow;
                if (!haveReport || now - lastQueryAt >= QueryInterval)
                {
                    var reported = QueryDbus();
                    lastQueryAt = now;
                    if (reported != null)
                    {
                        lastReported = reported.Value;
                        haveReport = true;
                    }
                }

                if (!haveReport) return TimeSpan.Zero;

                //Idle keeps growing between queries; the next query shows the reset when the user returns
                return lastReported + (DateTime.UtcNow - lastQueryAt);
            }
        }

        TimeSpan? QueryDbus()
        {
            //The interface that answered before is asked first; ones that failed are not asked again
            IEnumerable<DbusQuery> order = workingQuery != null ? [workingQuery, .. Queries] : Queries;

            foreach (var query in order)
            {
                if (failedQueries.Contains(query.Name)) continue;

                var result = ExternalTool.Run("gdbus", query.Arguments, timeoutMs: 2000);
                var idle = result != null && result.Succeeded ? ParseIdleReply(result.StandardOutput, query.RepliesInSeconds) : null;

                if (idle != null)
                {
                    workingQuery = query;
                    Source = query.Name;
                    return idle;
                }

                failedQueries.Add(query.Name);
                if (workingQuery == query) workingQuery = null;
            }

            return null;
        }

        /// <summary>Reads gdbus's reply, e.g. <c>(uint64 5432,)</c>, into an idle duration.</summary>
        public static TimeSpan? ParseIdleReply(string text, bool inSeconds)
        {
            try
            {
                if (GVariantText.ParseSingleResult(text) is not long value || value < 0) return null;
                return inSeconds ? TimeSpan.FromSeconds(value) : TimeSpan.FromMilliseconds(value);
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
