using Newtonsoft.Json;
using SimpleKVM.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace SimpleKVM.Rules
{
    public static class RuleStore
    {
        public static List<Rule> Rules { get; set; } = [];

        public static void Load()
        {
            if (!File.Exists(AppPaths.RulesFile)) return;

            try
            {
                var rulesJson = File.ReadAllText(AppPaths.RulesFile);
                var loadedRules = rulesJson?.DeserializeJson<List<Rule>>() ?? [];
                Rules.AddRange(loadedRules);
            }
            catch (Exception ex)
            {
                //A rules file that can't be parsed must never brick startup. Keep the
                //original for the user to recover and continue with no rules.
                Console.WriteLine($"Could not load rules: {ex.Message}");

                try
                {
                    var backupFilename = $"{AppPaths.RulesFile}.bad-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Copy(AppPaths.RulesFile, backupFilename, overwrite: true);
                }
                catch { }

                Rules = [];
            }
        }

        /// <summary>Why the last save failed, or null when it succeeded. Shown by the main window.</summary>
        public static string? LastSaveError { get; private set; }

        public static void Save()
        {
            try
            {
                var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                var rulesJson = JsonConvert.SerializeObject(Rules, Formatting.Indented, settings);
                Extensions.WriteTextFile(AppPaths.RulesFile, rulesJson);
                LastSaveError = null;
            }
            catch (Exception ex)
            {
                //Saving runs after every rule trigger and must never take the app down; it is
                //retried on the next change. The usual cause is the exe sitting in a folder the
                //user can't write to, such as Program Files.
                LastSaveError = $"Could not save {AppPaths.RulesFile}: {ex.Message}";
                Console.WriteLine(LastSaveError);
            }
        }
    }
}
