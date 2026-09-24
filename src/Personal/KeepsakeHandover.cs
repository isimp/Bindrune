using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Bindrune.Discovery;
using Jotunn.Configs;
using UnityEngine;

namespace Bindrune.Personal
{
    /// <summary>A keybind kept in Keepsake, matched to the bind it belongs to.</summary>
    internal sealed class KeepsakeKey
    {
        public BindEntry Bind;
        public KeyCombo Yours;
        public KeyCombo Profile;

        /// <summary>The line as it stands in keepsake.pins, so exactly that line is removed.</summary>
        public string Line;
    }

    /// <summary>
    /// Keepsake keeps single config values through profile syncs. While both are installed,
    /// keybinds are Bindrune's: Keepsake stops writing them, and each keybind it kept becomes
    /// yours here, with the key Keepsake recorded as the profile's. Its line then leaves
    /// Keepsake's file, so a key only ever has one keeper, and what it ends up as never depends on
    /// which of the two wrote last.
    ///
    /// Keepsake's file is BepInEx/keepsake.pins, one kept setting per line, tab separated: the cfg
    /// file relative to BepInEx/config, the section, the setting, the kept value and, optionally,
    /// the profile's value, both in the form the cfg file holds them. Lines are only ever taken
    /// out, never rewritten, and only while Keepsake is loaded, so the file of a mod that is not
    /// installed is left alone.
    /// </summary>
    internal static class KeepsakeHandover
    {
        private const string KeepsakeGuid = "isimp.Keepsake";

        private static string PinsFile => Path.Combine(Paths.BepInExRootPath, "keepsake.pins");

        /// <summary>The keybinds kept in Keepsake that belong to binds Bindrune can keep as yours.</summary>
        public static List<KeepsakeKey> Find()
        {
            var found = new List<KeepsakeKey>();
            if (!Chainloader.PluginInfos.ContainsKey(KeepsakeGuid) || !File.Exists(PinsFile)) return found;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(PinsFile);
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not read keepsake.pins: {ex.Message}", ex);
                return found;
            }

            var binds = new Dictionary<string, (BindEntry Bind, ConfigEntryBase Entry)>();
            foreach (var bind in BindRegistry.All)
            {
                if (!PersonalKeys.Eligible(bind)) continue;

                var entry = EntryOf(bind);
                var file = Relative(entry?.ConfigFile?.ConfigFilePath);
                if (file == null) continue;

                binds[Name(file, entry.Definition.Section, entry.Definition.Key)] = (bind, entry);
            }

            foreach (var line in lines)
            {
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                if (!binds.TryGetValue(Name(parts[0], parts[1].Trim(), parts[2].Trim()), out var match)) continue;
                if (!TryCombo(parts[3], match.Entry, out var yours)) continue;

                var profile = parts.Length > 4 && TryCombo(parts[4], match.Entry, out var recorded)
                    ? recorded
                    : match.Bind.Combo;

                found.Add(new KeepsakeKey { Bind = match.Bind, Yours = yours, Profile = profile, Line = line });
            }

            return found;
        }

        /// <summary>
        /// Takes the given lines out of keepsake.pins, read again first so nothing written since is
        /// lost, and swapped in whole so a crash cannot leave half a file.
        /// </summary>
        public static void Remove(IEnumerable<KeepsakeKey> keys)
        {
            try
            {
                var taken = new HashSet<string>(keys.Select(k => k.Line));
                var kept = File.ReadAllLines(PinsFile).Where(l => !taken.Contains(l)).ToArray();

                var temp = PinsFile + ".bindrune.tmp";
                File.WriteAllLines(temp, kept, new UTF8Encoding(false));
                File.Replace(temp, PinsFile, null);
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not update keepsake.pins: {ex.Message}", ex);
            }
        }

        private static ConfigEntryBase EntryOf(BindEntry bind)
        {
            if (bind.Handle is ConfigEntryBase entry) return entry;
            if (bind.Handle is ButtonConfig button) return (ConfigEntryBase)button.ShortcutConfig ?? button.Config;
            return null;
        }

        /// <summary>A cfg file, section and setting as one name, the way Keepsake tells settings apart.</summary>
        private static string Name(string file, string section, string key) =>
            file.Trim().Replace('\\', '/').ToLowerInvariant() + "\t" + section + "\t" + key;

        private static string Relative(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            try
            {
                var root = Path.GetFullPath(Paths.ConfigPath).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                var full = Path.GetFullPath(path);
                return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A value in the form the cfg file holds it, as a combo, read the way the setting reads it.</summary>
        private static bool TryCombo(string text, ConfigEntryBase entry, out KeyCombo combo)
        {
            combo = KeyCombo.None;
            try
            {
                var value = TomlTypeConverter.ConvertToValue(text.Trim(), entry.SettingType);
                if (value is KeyboardShortcut shortcut)
                {
                    combo = shortcut.MainKey == KeyCode.None ? KeyCombo.None : new KeyCombo(shortcut.MainKey, shortcut.Modifiers);
                    return true;
                }

                if (value is KeyCode key)
                {
                    combo = key == KeyCode.None ? KeyCombo.None : new KeyCombo(key, null);
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a value this setting can hold, so not one to take over.
            }

            return false;
        }
    }
}
