using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleKVM.Utilities
{
    public static class Logger
    {
        static readonly object lck = new object();
        public static string Filename = @"C:\\Temp\\SimpleKVM.log";

        public static void Log(string message)
        {
            lock (lck)
            {
                var folder = Path.GetDirectoryName(Filename);
                if (folder == null) return;
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var logLine = $"{DateTime.Now}: {message}{Environment.NewLine}";

                File.AppendAllText(Filename, logLine);
            }
        }
    }
}
