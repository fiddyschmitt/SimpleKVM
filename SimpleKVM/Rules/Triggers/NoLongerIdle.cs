using SimpleKVM.Utilities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleKVM.Rules.Triggers
{
    public class NoLongerIdle : Trigger
    {
        /// <summary>
        /// How long the user must have been idle for their next input to count as "no longer
        /// idle". Without it any input that lands between two polls makes the sampled idle time
        /// drop, and the rule fires many times a second during ordinary use.
        /// </summary>
        public static readonly TimeSpan IdleThreshold = TimeSpan.FromSeconds(1);

        static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        public override string GetTriggerAsFriendlyString()
        {
            var result = $"when no longer idle";
            return result;
        }

        Task? monitorIdleTime;
        CancellationTokenSource? monitorIdleTimeToken;

        public override void StartMonitoring()
        {
            StopMonitoring();

            var cancellation = new CancellationTokenSource();
            monitorIdleTimeToken = cancellation;

            monitorIdleTime = Task.Factory.StartNew(() =>
            {
                TimeSpan? lastIdleTime = null;

                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var idleTime = IdleUtility.GetIdleTimeSpan();

                        if (ShouldFire(lastIdleTime, idleTime))
                        {
                            RaiseTriggered();
                        }

                        lastIdleTime = idleTime;
                    }
                    catch (Exception ex)
                    {
                        //A failed idle read must not end the watch
                        Console.WriteLine($"Idle watch: {ex.Message}");
                    }

                    cancellation.Token.WaitHandle.WaitOne(PollInterval);
                }
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>True when the user was idle for at least the threshold and has now provided input.</summary>
        public static bool ShouldFire(TimeSpan? previousIdleTime, TimeSpan idleTime)
        {
            return previousIdleTime >= IdleThreshold && idleTime < previousIdleTime;
        }

        public override void StopMonitoring()
        {
            monitorIdleTimeToken?.Cancel();
            monitorIdleTimeToken = null;

            //Bounded: a late trigger from a loop still winding down is filtered by the rule's status
            monitorIdleTime?.Wait(PollInterval * 2);
            monitorIdleTime = null;
        }
    }
}
