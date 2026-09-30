using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace SimpleKVM
{
    /// <summary>
    /// One running copy per user. A second launch hands its request to the first over a named
    /// pipe (a Unix domain socket on Linux and macOS) and exits, so launching the app again is
    /// how you get its window back where there is no tray icon to click: GNOME without an
    /// AppIndicator extension, or Windows when the tray is hidden. Two copies would otherwise
    /// both act on every hotkey and USB event.
    /// </summary>
    public static class SingleInstance
    {
        static readonly string PipeName = $"SimpleKVM-{Environment.UserName}";

        public const string ShowRequest = "show";
        public const string PingRequest = "ping";

        /// <summary>
        /// Tells an already-running copy to show its window (or just that we exist, when this
        /// launch was meant to stay minimized). True when one answered, in which case this
        /// process should exit.
        /// </summary>
        public static bool NotifyExistingInstance(bool showWindow)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(timeout: 500);

                using var writer = new StreamWriter(client);
                writer.WriteLine(showWindow ? ShowRequest : PingRequest);
                writer.Flush();
                return true;
            }
            catch (Exception)
            {
                //Nothing listening (or a stale socket from a crashed copy): we are the instance
                return false;
            }
        }

        /// <summary>Listens for later launches for the life of the process; <paramref name="onShow"/> runs on a background thread.</summary>
        public static void Listen(Action onShow)
        {
            var thread = new Thread(() =>
            {
                bool clearedStaleSocket = false;

                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, maxNumberOfServerInstances: 1, PipeTransmissionMode.Byte, PipeOptions.None);
                        server.WaitForConnection();

                        using var reader = new StreamReader(server);
                        if (reader.ReadLine() == ShowRequest) onShow();
                    }
                    catch (Exception)
                    {
                        //On Unix a crashed copy leaves its socket file behind and the bind fails; clear it once
                        if (!clearedStaleSocket && !OperatingSystem.IsWindows())
                        {
                            clearedStaleSocket = true;
                            try { File.Delete(Path.Combine(Path.GetTempPath(), "CoreFxPipe_" + PipeName)); } catch { }
                            continue;
                        }

                        Thread.Sleep(1000);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "Single-instance listener"
            };
            thread.Start();
        }
    }
}
