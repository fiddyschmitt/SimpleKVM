using System;

namespace SimpleKVM.Utilities.linux
{
    /// <summary>
    /// Idle time kept from the two things a desktop announces rather than a clock that has to be
    /// read over and over: "the user has now been idle for the threshold" and "the user is
    /// active again". While the user is active the idle time is somewhere below the threshold
    /// and reported as zero, which is all the no-longer-idle trigger needs: it fires on the
    /// drop from at-least-the-threshold to less.
    /// </summary>
    public sealed class IdleWatchState(TimeSpan threshold)
    {
        DateTime? idleSince;    //null while the user is active

        public TimeSpan Threshold { get; } = threshold;

        public bool IsIdle => idleSince != null;

        /// <summary>Starts over from an idle time the desktop reported directly.</summary>
        public void Sync(TimeSpan idle, DateTime now)
        {
            idleSince = idle >= Threshold ? now - idle : null;
        }

        /// <summary>
        /// The desktop says the idle time has reached the threshold. It says so at once for a
        /// watch added while the user was already idle, so an idle spell already known about
        /// keeps its start: moving it would look like the user having come back.
        /// </summary>
        public void BecameIdle(DateTime now)
        {
            idleSince ??= now - Threshold;
        }

        /// <summary>The desktop says there was input.</summary>
        public void BecameActive()
        {
            idleSince = null;
        }

        public TimeSpan IdleAt(DateTime now)
        {
            if (idleSince == null) return TimeSpan.Zero;

            var idle = now - idleSince.Value;
            return idle > TimeSpan.Zero ? idle : TimeSpan.Zero;
        }

        /// <summary>
        /// Whether a direct reading contradicts what the announcements added up to, by more
        /// than the slack two clocks and a message in flight account for: a missed
        /// announcement, or the desktop shell having restarted and forgotten the watches.
        /// </summary>
        public bool Contradicts(TimeSpan reportedIdle, DateTime now, TimeSpan slack)
        {
            if (IsIdle) return reportedIdle + slack < Threshold || (IdleAt(now) - reportedIdle).Duration() > slack;
            return reportedIdle > Threshold + slack;
        }
    }
}
