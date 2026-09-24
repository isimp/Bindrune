using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Bindrune.Personal
{
    /// <summary>One kept setting in keepsake.pins.</summary>
    public sealed class KeepsakeLine
    {
        public string File;
        public string Section;
        public string Key;
        public string Value;

        /// <summary>The profile's value, or null when the line leaves it off.</summary>
        public string Profile;

        /// <summary>The line as it stands in the file, so exactly that line can be removed.</summary>
        public string Line;

        public string Name => KeepsakeContract.Name(File, Section, Key);
    }

    /// <summary>A keybind kept in Keepsake, matched to a bind, as the keys to take over.</summary>
    public sealed class KeepsakeMatch<T>
    {
        public T Bind;
        public KeyCombo Yours;

        /// <summary>The key Keepsake recorded as the profile's, or null when it has none.</summary>
        public KeyCombo? Profile;

        public string Line;
    }

    /// <summary>
    /// Keepsake's pins file, BepInEx/keepsake.pins, read the way Keepsake writes it: a version
    /// line, then one kept setting per line, tab separated, as the cfg file relative to
    /// BepInEx/config, the section, the setting, the kept value and, optionally, the profile's
    /// value, both in the form the cfg file holds them. Kept apart from the game so it can be
    /// tested on its own. A file of any other version is not touched. See tests/contract.
    /// </summary>
    public static class KeepsakeContract
    {
        public const string PinsVersion = "# keepsake pins v1";

        /// <summary>The kept settings in the lines of keepsake.pins, or null for any version but PinsVersion.</summary>
        public static List<KeepsakeLine> Parse(IEnumerable<string> lines)
        {
            var found = new List<KeepsakeLine>();
            var versionSeen = false;

            foreach (var line in lines)
            {
                if (!versionSeen)
                {
                    if (line.Trim().Length == 0) continue;
                    if (line.Trim() != PinsVersion) return null;
                    versionSeen = true;
                    continue;
                }

                if (line.Length == 0 || line.StartsWith("#")) continue;

                var parts = line.Split('\t');
                if (parts.Length < 4) continue;

                found.Add(new KeepsakeLine
                {
                    File = parts[0],
                    Section = parts[1].Trim(),
                    Key = parts[2].Trim(),
                    Value = parts[3].Trim(),
                    Profile = parts.Length > 4 ? parts[4].Trim() : null,
                    Line = line,
                });
            }

            return versionSeen ? found : null;
        }

        /// <summary>A cfg file, section and setting as one name, the way Keepsake tells settings apart.</summary>
        public static string Name(string file, string section, string key) =>
            file.Trim().Replace('\\', '/').ToLowerInvariant() + "\t" + section + "\t" + key;

        /// <summary>
        /// The kept settings that belong to one of the given binds, named as Name does and read as
        /// the type of setting behind each. Lines whose value that setting cannot hold are left out.
        /// </summary>
        public static List<KeepsakeMatch<T>> Match<T>(IEnumerable<KeepsakeLine> lines, IDictionary<string, (T Bind, Type SettingType)> binds)
        {
            var matches = new List<KeepsakeMatch<T>>();
            foreach (var line in lines)
            {
                if (!binds.TryGetValue(line.Name, out var bind)) continue;
                if (!TryCombo(line.Value, bind.SettingType, out var yours)) continue;

                matches.Add(new KeepsakeMatch<T>
                {
                    Bind = bind.Bind,
                    Yours = yours,
                    Profile = line.Profile != null && TryCombo(line.Profile, bind.SettingType, out var profile) ? profile : (KeyCombo?)null,
                    Line = line.Line,
                });
            }

            return matches;
        }

        /// <summary>The lines of a file with the given lines taken out, the rest as they were.</summary>
        public static string[] Without(IEnumerable<string> lines, IEnumerable<string> taken)
        {
            var remove = new HashSet<string>(taken);
            return lines.Where(l => !remove.Contains(l)).ToArray();
        }

        /// <summary>
        /// A value in the form the cfg file holds it, as a combo, read the way the setting reads it.
        /// False for a value the setting cannot hold, which is then not one to take over.
        /// </summary>
        public static bool TryCombo(string text, Type settingType, out KeyCombo combo)
        {
            combo = KeyCombo.None;
            text = (text ?? "").Trim();

            try
            {
                if (settingType == typeof(KeyboardShortcut))
                {
                    // Read directly rather than through TomlTypeConverter, which only knows the type
                    // once something has touched KeyboardShortcut. Deserialize answers text it
                    // cannot read with no key at all, so that only counts when no key was written.
                    var shortcut = KeyboardShortcut.Deserialize(text);
                    if (shortcut.MainKey == KeyCode.None && !IsNoKey(text)) return false;

                    combo = shortcut.MainKey == KeyCode.None ? KeyCombo.None : new KeyCombo(shortcut.MainKey, shortcut.Modifiers);
                    return true;
                }

                if (settingType == typeof(KeyCode))
                {
                    var key = (KeyCode)TomlTypeConverter.ConvertToValue(text, typeof(KeyCode));
                    combo = key == KeyCode.None ? KeyCombo.None : new KeyCombo(key, null);
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a value this setting can hold.
            }

            return false;
        }

        private static bool IsNoKey(string text) => text.Length == 0 || string.Equals(text, "None", StringComparison.OrdinalIgnoreCase);
    }
}
