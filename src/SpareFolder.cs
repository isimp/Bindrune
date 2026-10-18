using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Bindrune
{
    /// <summary>
    /// Which profiles get a spare copy of what Bindrune keeps, where it goes, how your answer on
    /// keeping one is read, and how a copy is removed. The sibling mod keeps this file the same
    /// but for the mod's name, as both follow the same two managers.
    ///
    /// Update existing profile in Thunderstore Mod Manager and r2modman builds the new profile
    /// beside the old one, deletes the old folder and renames the new one to its name. Only a
    /// profile of those two managers gets a spare copy, told by the mods.yml they keep in every
    /// profile folder, in a folder named profiles. The copy goes one level up, beside the managers'
    /// own cache and exports folders, in Bindrune/ and the profile's folder name. Not inside
    /// profiles, where the managers list every folder as a profile. Gale never replaces a profile
    /// folder, and a profile anywhere else gets no spare copy.
    ///
    /// Nothing here writes to the log: a preloader may take this too, before any plugin exists to
    /// log through, so each caller tells its own failures.
    /// </summary>
    internal static class SpareFolder
    {
        /// <summary>The first line of the answer file, which says the rest is written as this version writes it.</summary>
        public const string AnswerVersion = "# bindrune spare v1";

        /// <summary>The spare copy's folder for the profile whose BepInEx folder this is, or null when it is not one of a manager that replaces profile folders.</summary>
        public static string For(string bepInExRoot)
        {
            try
            {
                var profile = Directory.GetParent(bepInExRoot);
                if (profile == null || !File.Exists(Path.Combine(profile.FullName, "mods.yml"))) return null;

                var profiles = profile.Parent;
                if (profiles == null || !string.Equals(profiles.Name, "profiles", StringComparison.OrdinalIgnoreCase)) return null;

                var game = profiles.Parent;
                return game == null ? null : Path.Combine(Path.Combine(game.FullName, "Bindrune"), profile.Name);
            }
            catch (Exception)
            {
                // A profile folder whose parents cannot be read is not one a manager replaces.
                return null;
            }
        }

        /// <summary>
        /// Whether the answer file's lines say yes or no to a spare copy: null before you answered,
        /// and for a file this version did not write.
        /// </summary>
        public static bool? WantedIn(IEnumerable<string> lines)
        {
            var all = lines.ToList();
            if (all.Count == 0 || all[0].Trim() != AnswerVersion) return null;

            foreach (var parts in all.Skip(1).Where(l => !l.StartsWith("#", StringComparison.Ordinal)).Select(l => l.Split('\t')))
                if (parts.Length >= 2 && parts[0].Trim() == "copy")
                    return parts[1].Trim() == "yes" ? true : parts[1].Trim() == "no" ? (bool?)false : null;
            return null;
        }

        /// <summary>Removes a spare copy, and the Bindrune folder beside profiles once it holds nothing. Throws where a file is held open.</summary>
        public static void Remove(string spare)
        {
            if (Directory.Exists(spare)) Directory.Delete(spare, true);
            var root = Path.GetDirectoryName(spare);
            if (root != null && Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
        }
    }
}
