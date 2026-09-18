using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace Bindrune.Hints
{
    /// <summary>
    /// The binds you asked Bindrune to show on screen. Opt-in, one id per line.
    ///
    /// Lives in BepInEx/config with the situations and the mutes: which hints are worth having is
    /// knowledge about a modlist, so it should travel with a profile the way they do. See
    /// PersonalKeys for the one kind of state that deliberately does not.
    /// </summary>
    public static class HintChoice
    {
        private static HashSet<string> _chosen;

        /// <summary>Raised when the set changes, so the overlay can rebuild without polling it.</summary>
        public static Action Changed;

        private static string FilePath => TextStore.Ours(Paths.ConfigPath, "hints.txt");

        private static HashSet<string> Chosen
        {
            get
            {
                if (_chosen != null) return _chosen;

                _chosen = new HashSet<string>();
                foreach (var line in TextStore.Read(FilePath))
                {
                    if (TextStore.IsNoise(line)) continue;

                    var id = line.Trim();
                    if (id.Length > 0) _chosen.Add(id);
                }

                return _chosen;
            }
        }

        public static int Count => Chosen.Count;

        public static bool Shows(string bindId) => Chosen.Contains(bindId);

        public static void Toggle(string bindId)
        {
            if (!Chosen.Add(bindId)) Chosen.Remove(bindId);
            Save();
        }

        /// <summary>Adds several at once, for "show all of this mod's binds".</summary>
        public static void Add(IEnumerable<string> bindIds)
        {
            var added = bindIds.Count(id => Chosen.Add(id));
            if (added > 0) Save();
        }

        public static void Remove(IEnumerable<string> bindIds)
        {
            var removed = bindIds.Count(id => Chosen.Remove(id));
            if (removed > 0) Save();
        }

        private static void Save()
        {
            TextStore.Write(FilePath,
                new[]
                {
                    "# Binds Bindrune shows on screen, one id per line.",
                    "# Each is shown only while its situations say it applies."
                },
                Chosen.OrderBy(id => id));

            Changed?.Invoke();
        }
    }
}
