using SimpleKVM.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleKVM.Rules.Triggers
{
    public class NoLongerIdle : Trigger
    {
        public override string GetTriggerAsFriendlyString()
        {
            var result = $"when no longer idle";
            return result;
        }

        Task? monitorIdleTime;
        CancellationTokenSource? monitorIdleTimeToken;

        public override void StartMonitoring()
        {
            monitorIdleTimeToken = new CancellationTokenSource();

            TimeSpan? lastIdleTime = null;
            monitorIdleTime = Task.Factory.StartNew(() =>
            {
                while (!monitorIdleTimeToken.IsCancellationRequested)
                {
                    var idleTime = IdleUtility.GetIdleTimeSpan();

                    if (lastIdleTime > idleTime)
                    {
                        RaiseTriggered();
                    }

                    lastIdleTime = idleTime;

                    Thread.Sleep(100);
                }
            });
        }

        public override void StopMonitoring()
        {
            monitorIdleTimeToken?.Cancel();
            monitorIdleTime?.Wait();
        }
    }
}
