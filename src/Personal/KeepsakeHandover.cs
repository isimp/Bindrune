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

namespace Bindrune.Personal
{
    /// <summary>
    /// Keepsake keeps single config values through profile syncs. While both are installed,
    /// keybinds are Bindrune's: Keepsake stops writing them, and each keybind it kept becomes
    /// yours here, with the key Keepsake recorded as the profile's. Its line then leaves
    /// Keepsake's file, so a key only ever has one keeper, and what it ends up as never depends on
    /// which of the two wrote last.
    ///
    /// Only while Keepsake is loaded, so the file of a mod that is not installed is left alone,
    /// and only for a file in the version KeepsakeContract reads. Lines are only ever taken out,
    /// never rewritten. The reading itself is in KeepsakeContract; this is the part that touches
    /// the game and the file.
    /// </summary>
    internal static class KeepsakeHandover
    {
        private const string KeepsakeGuid = "isimp.Keepsake";

        private static string PinsFile => Path.Combine(Paths.BepInExRootPath, "keepsake.pins");

        /// <summary>Whether there may be keybinds to take over, cheap enough to ask at every startup.</summary>
        public static bool MayHaveKeys => Chainloader.PluginInfos.ContainsKey(KeepsakeGuid) && File.Exists(PinsFile);

        /// <summary>The keybinds kept in Keepsake that belong to binds Bindrune can keep as yours.</summary>
        public static List<KeepsakeMatch<BindEntry>> Find()
        {
            if (!MayHaveKeys) return new List<KeepsakeMatch<BindEntry>>();

            string[] lines;
            try
            {
                lines = File.ReadAllLines(PinsFile);
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not read keepsake.pins: {ex.Message}", ex);
                return new List<KeepsakeMatch<BindEntry>>();
            }

            var kept = KeepsakeContract.Parse(lines);
            if (kept == null)
            {
                Plugin.WarnOnce("Bindrune: keepsake.pins is not in a version this Bindrune knows " +
                                $"({KeepsakeContract.PinsVersion}), so no keybinds are taken over from it. Updating Bindrune fixes this.");
                return new List<KeepsakeMatch<BindEntry>>();
            }

            var binds = new Dictionary<string, (BindEntry Bind, Type SettingType)>();
            foreach (var bind in BindRegistry.All)
            {
                if (!PersonalKeys.Eligible(bind)) continue;

                var entry = EntryOf(bind);
                var file = Relative(entry?.ConfigFile?.ConfigFilePath);
                if (file == null) continue;

                binds[KeepsakeContract.Name(file, entry.Definition.Section, entry.Definition.Key)] = (bind, entry.SettingType);
            }

            return KeepsakeContract.Match(kept, binds);
        }

        /// <summary>
        /// Takes the given lines out of keepsake.pins, read again first so nothing written since is
        /// lost, and swapped in whole so a crash cannot leave half a file.
        /// </summary>
        public static void Remove(IEnumerable<KeepsakeMatch<BindEntry>> keys)
        {
            try
            {
                var kept = KeepsakeContract.Without(File.ReadAllLines(PinsFile), keys.Select(k => k.Line));

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
    }
}
