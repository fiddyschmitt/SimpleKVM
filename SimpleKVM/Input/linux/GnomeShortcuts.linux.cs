using SimpleKVM.Platform.linux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Tmds.DBus.Protocol;

namespace SimpleKVM.Input.linux
{
    /// <summary>
    /// GNOME's own keyboard shortcuts, read and changed through gsettings: the window manager's,
    /// the shell's and the media keys' settings, the user's custom shortcuts, and those of every
    /// active extension (Ubuntu's Tiling Assistant, its dock), which the shell lists over D-Bus
    /// along with where each keeps its settings. A combination one of them has never reaches an
    /// app, so the hotkey chooser says which, and can take it away from them; each keeps the
    /// rest of its keys. Everything is per user: nothing needs root.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static partial class GnomeShortcuts
    {
        const string MediaKeys = "org.gnome.settings-daemon.plugins.media-keys";
        const string CustomShortcut = MediaKeys + ".custom-keybinding";

        static readonly string[] DesktopSchemas =
        [
            "org.gnome.desktop.wm.keybindings",
            "org.gnome.mutter.keybindings",
            "org.gnome.mutter.wayland.keybindings",
            "org.gnome.shell.keybindings",
            MediaKeys,
        ];

        /// <summary>Settings to read: a schema (with its path, for a custom shortcut), where its compiled schema is when not installed system-wide, and whose it is.</summary>
        sealed record Source(string Schema, string? SchemaDir, string Owner, bool Custom = false);

        /// <summary>A shortcut setting that has the gesture among its keys, and the phrase that tells the user which.</summary>
        public sealed record Use(string Schema, string? SchemaDir, GnomeKeybindings.Setting Setting, string Description);

        public static bool IsGnome =>
            (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "")
                .Split(':')
                .Any(desktop => desktop.Equals("GNOME", StringComparison.OrdinalIgnoreCase));

        public static List<Use> Find(HotkeyGesture gesture)
        {
            var uses = new List<Use>();
            if (!IsGnome || !LinuxKeyCodes.TryGetKeysym(gesture.KeyName, out _, out _)) return uses;

            foreach (var source in Sources())
            {
                var listed = Gsettings(source.SchemaDir, "list-recursively", source.Schema);
                if (listed?.Succeeded != true) continue;

                var settings = listed.StandardOutput
                                     .Split('\n')
                                     .Select(GnomeKeybindings.ParseLine)
                                     .OfType<GnomeKeybindings.Setting>()
                                     .ToList();

                //Dash-to-dock (Ubuntu's dock too) keeps Win+1 and the like for its apps only while its hot-keys switch is on
                bool dockHotkeysOff = settings.Any(s => s.Key == "hot-keys" && s.Value == "false");

                foreach (var setting in settings)
                {
                    if (source.Custom && setting.Key != "binding") continue;
                    if (dockHotkeysOff && setting.Key.StartsWith("app-", StringComparison.Ordinal) && setting.Key.Contains("hotkey", StringComparison.Ordinal)) continue;

                    //Only the label a settings page shows for a shortcut (dash-to-dock's shortcut-text), not one of its own
                    if (setting.Key.EndsWith("-text", StringComparison.Ordinal)) continue;
                    if (!setting.Strings.Any(accelerator => GnomeKeybindings.Matches(accelerator, gesture))) continue;

                    string description;
                    if (source.Custom)
                    {
                        var name = settings.FirstOrDefault(s => s.Key == "name")?.Strings.FirstOrDefault();
                        description = string.IsNullOrEmpty(name) ? "one of your custom shortcuts" : $"your shortcut \"{name}\"";
                    }
                    else
                    {
                        description = $"\"{GnomeKeybindings.Humanize(setting.Key)}\" ({source.Owner})";
                    }

                    //A custom shortcut's schema carries its path, which is what tells it from the others
                    uses.Add(new Use(source.Schema, source.SchemaDir, setting, description));
                }
            }

            return uses;
        }

        /// <summary>Takes the gesture away from every desktop shortcut that has it. Throws, saying why, when a setting can't be changed.</summary>
        public static void Free(HotkeyGesture gesture)
        {
            foreach (var use in Find(gesture))
            {
                var result = Gsettings(use.SchemaDir, "set", use.Schema, use.Setting.Key, GnomeKeybindings.Without(use.Setting, gesture));
                if (result?.Succeeded != true)
                {
                    var why = result?.StandardError.Trim();
                    throw new InvalidOperationException(string.IsNullOrEmpty(why) ? $"gsettings couldn't change {use.Setting.Key}." : why);
                }
            }
        }

        static IEnumerable<Source> Sources()
        {
            foreach (var schema in DesktopSchemas) yield return new Source(schema, null, "GNOME");

            //Custom shortcuts: one relocatable schema, at a path per shortcut
            var custom = Gsettings(null, "get", MediaKeys, "custom-keybindings");
            if (custom?.Succeeded == true)
            {
                foreach (var path in GnomeKeybindings.Strings(custom.StandardOutput))
                {
                    yield return new Source($"{CustomShortcut}:{path}", null, "", Custom: true);
                }
            }

            foreach (var source in ExtensionSources()) yield return source;
        }

        /// <summary>
        /// The settings of every active extension, wherever they are said to be: the schema its
        /// metadata names, else those its schemas folder holds, else those its code names. Ubuntu's
        /// dock does the last (its settings are dash-to-dock's, under another name). An extension
        /// from extensions.gnome.org keeps the compiled schemas in that folder; one installed with
        /// the system has them with everyone else's.
        /// </summary>
        static List<Source> ExtensionSources()
        {
            var sources = new List<Source>();

            var bus = SessionBus.Connection;
            if (bus == null) return sources;

            var extensions = SessionBus.Wait(() => SessionBus.CallAsync(bus, "org.gnome.Shell.Extensions", "/org/gnome/Shell/Extensions",
                                                                         "org.gnome.Shell.Extensions", "ListExtensions",
                                                                         static (Message message, object? _) => ReadExtensions(message)), out _);
            if (extensions == null) return sources;

            foreach (var extension in extensions)
            {
                string? schemaDir = null;
                if (extension.Path != null && File.Exists(Path.Combine(extension.Path, "schemas", "gschemas.compiled")))
                {
                    schemaDir = Path.Combine(extension.Path, "schemas");
                }

                foreach (var schema in SchemasOf(extension)) sources.Add(new Source(schema, schemaDir, extension.Name));
            }

            return sources;
        }

        //An extension's files don't change while it is installed; reading its code once is enough
        static readonly Dictionary<string, List<string>> schemasByExtensionPath = [];

        static List<string> SchemasOf(Extension extension)
        {
            if (extension.SettingsSchema != null) return [extension.SettingsSchema];
            if (extension.Path == null) return [];

            lock (schemasByExtensionPath)
            {
                if (schemasByExtensionPath.TryGetValue(extension.Path, out var known)) return known;

                var schemas = new List<string>();
                try
                {
                    var folder = Path.Combine(extension.Path, "schemas");
                    if (Directory.Exists(folder))
                    {
                        foreach (var xml in Directory.GetFiles(folder, "*.gschema.xml"))
                        {
                            schemas.AddRange(SchemaId().Matches(File.ReadAllText(xml)).Select(m => m.Groups[1].Value));
                        }
                    }

                    if (schemas.Count == 0)
                    {
                        foreach (var script in Directory.EnumerateFiles(extension.Path, "*.js", SearchOption.AllDirectories))
                        {
                            schemas.AddRange(SchemaNamedInCode().Matches(File.ReadAllText(script)).Select(m => m.Value.TrimEnd('.')));
                        }
                    }
                }
                catch
                {
                    //an unreadable folder leaves what was found
                }

                return schemasByExtensionPath[extension.Path] = schemas.Distinct().ToList();
            }
        }

        sealed record Extension(string Name, string? Path, string? SettingsSchema);

        /// <summary>ListExtensions() returns a{sa{sv}}: each extension's uuid and its metadata, with "state" 1 for an active one.</summary>
        static List<Extension> ReadExtensions(Message message)
        {
            var result = new List<Extension>();
            var reader = message.GetBodyReader();

            var end = reader.ReadDictionaryStart();
            while (reader.HasNext(end))
            {
                reader.AlignStruct();
                var uuid = reader.ReadString();
                var info = reader.ReadDictionaryOfStringToVariantValue();

                if (Number(info, "state") != 1) continue;

                result.Add(new Extension(Text(info, "name") ?? uuid, Text(info, "path"), Text(info, "settings-schema")));
            }

            return result;
        }

        static string? Text(Dictionary<string, VariantValue> info, string key)
        {
            if (!info.TryGetValue(key, out var value)) return null;
            while (value.Type == VariantValueType.Variant) value = value.GetVariantValue();
            return value.Type == VariantValueType.String ? value.GetString() : null;
        }

        static double? Number(Dictionary<string, VariantValue> info, string key)
        {
            if (!info.TryGetValue(key, out var value)) return null;
            while (value.Type == VariantValueType.Variant) value = value.GetVariantValue();
            return value.Type == VariantValueType.Double ? value.GetDouble() : null;
        }

        static ExternalTool.Result? Gsettings(string? schemaDir, params string[] arguments)
        {
            var list = new List<string>();
            if (schemaDir != null)
            {
                list.Add("--schemadir");
                list.Add(schemaDir);
            }
            list.AddRange(arguments);

            return ExternalTool.Run("gsettings", list);
        }

        [GeneratedRegex(@"<schema\b[^>]*\bid=""([^""]+)""")]
        private static partial Regex SchemaId();

        [GeneratedRegex(@"org\.gnome\.shell\.extensions\.[A-Za-z0-9._-]+")]
        private static partial Regex SchemaNamedInCode();
    }
}
