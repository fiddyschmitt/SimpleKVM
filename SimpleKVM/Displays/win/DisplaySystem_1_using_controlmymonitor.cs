using DDCKVMService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleKVM.Displays.win
{
    public static class DisplaySystem_1
    {
        public static string ControlMyMonitorExe => Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", @"ext\win\controlmymonitor\ControlMyMonitor.exe");

        public static IList<Monitor> GetMonitors()
        {
            var selfPath = AppDomain.CurrentDomain.BaseDirectory;

            if (selfPath == null)
            {
                return new List<Monitor>();
            }

            var controlMyMonitorExe = Path.Combine(selfPath, @"ext\win\controlmymonitor\ControlMyMonitor.exe");

            var monitorListCommand = new ProcessStartInfo
            {
                FileName = ControlMyMonitorExe,
                Arguments = "/smonitors"
            };

            var monitorListStdout = monitorListCommand.StartAndReadStdout();

            /*
            Monitor Device Name: "\\.\DISPLAY1\Monitor0"
            Monitor Name: "VX4380 SERIES"
            Serial Number: ""
            Adapter Name: "NVIDIA GeForce GTX 760"
            Monitor ID: "MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0004"

            Monitor Device Name: "\\.\DISPLAY2\Monitor0"
            Monitor Name: "VX4380 SERIES"
            Serial Number: ""
            Adapter Name: "NVIDIA GeForce GTX 760"
            Monitor ID: "MONITOR\VSC5B34\{4d36e96e-e325-11ce-bfc1-08002be10318}\0000"
            */

            var result = monitorListStdout
                            .SplitAndKeep("Monitor Device Name")
                            .Where(line => !string.IsNullOrEmpty(line))
                            .AsParallel()
                            .AsOrdered()
                            .Select(block =>
                            {
                                var dict = Regex
                                            .Split(block, "\r\n|\r|\n")
                                            .Where(line => !string.IsNullOrEmpty(line))
                                            .Select(line => line.Split(new[] { ": " }, StringSplitOptions.None))
                                            .ToDictionary(tokens => tokens[0], tokens => tokens[1].Replace("\"", "").RemoveNonPrintable());

                                var monitorInfoCommand = new ProcessStartInfo
                                {
                                    FileName = controlMyMonitorExe,
                                    Arguments = $"/scomma \"\" \"{dict["Monitor Name"]}"
                                };

                                var monitorInfo = monitorInfoCommand.StartAndReadStdout();

                                //VPC Code
                                //60,Input Select,Read+Write,18,19,"15, 16, 17, 18"
                                var inputSelectLine = Regex
                                                        .Split(monitorInfo, "\r\n|\r|\n")
                                                        .Where(line => line.StartsWith("60"))
                                                        .FirstOrDefault();

                                var inputSelectLineTokens = Regex.Split(inputSelectLine, "(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)") //split based on commas, taking into account double-quotes
                                                            .Where(line => !string.IsNullOrEmpty(line))
                                                            .Select(line => line.Replace("\"", ""))
                                                            .ToList();


                                var currentSource = int.Parse(inputSelectLineTokens[3]);

                                var validSources = inputSelectLineTokens[5]
                                                        .Split(new[] { ", " }, StringSplitOptions.None)
                                                        .Select(source => int.Parse(source))
                                                        .OrderBy(source => source)
                                                        .ToList();

                                return new Monitor()
                                {
                                    //AdapterName = dict["Adapter Name"],
                                    //MonitorDeviceName = dict["Monitor Device Name"],
                                    //MonitorId = dict["Monitor ID"],
                                    //MonitorName = dict["Monitor Name"],
                                    //SerialNumber = dict["Serial Number"],

                                    CurrentSource = currentSource,
                                    ValidSources = validSources
                                };
                            })
                            .ToList();

            return result;
        }
    }
}
