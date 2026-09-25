using System.Collections.Generic;
using System.Linq;
using BepInEx;

namespace Bindrune.Context
{
    /// <summary>
    /// The situations you said a bind applies to: both the "where" tags and the held items,
    /// stored as one set per bind because that is how they are asked for. See SituationTags.
    ///
    /// Kept in memory once read, but checked against the file before every change and whenever
    /// the panel opens: the file is written whole, so without that an edit made by hand while
    /// the game runs would be undone by the next click.
    /// </summary>
    public static class SituationStore
    {
        private static Dictionary<string, HashSet<string>> _situations;
        private static string _stamp;

        private static string FilePath => TextStore.Ours(Paths.ConfigPath, "situations.txt");

        private static Dictionary<string, HashSet<string>> Situations
        {
            get
            {
                if (_situations != null) return _situations;

                // Held open by another program: nothing to go on, and nothing kept in memory, so
                // the next look reads the file again and no change is written over it meanwhile.
                var stamp = TextStore.Stamp(FilePath);
                if (!TextStore.TryRead(FilePath, out var lines)) return new Dictionary<string, HashSet<string>>();

                _situations = new Dictionary<string, HashSet<string>>();
                _stamp = stamp;

                foreach (var line in lines)
                {
                    if (TextStore.IsNoise(line)) continue;

                    var split = line.IndexOf('=');
                    if (split <= 0) continue;

                    var values = line.Substring(split + 1).Split('|').Where(v => v.Length > 0).ToArray();
                    if (values.Length > 0) _situations[line.Substring(0, split)] = new HashSet<string>(values);
                }

                return _situations;
            }
        }

        /// <summary>Forgets what was read, as a new game starts. For the tests, which run many in one process.</summary>
        internal static void Reset()
        {
            _situations = null;
            _stamp = null;
        }

        /// <summary>Forgets what was read if the file has changed since, so the next look reads it again.</summary>
        public static void Sync()
        {
            if (_situations != null && TextStore.ChangedSince(FilePath, _stamp)) _situations = null;
        }

        public static HashSet<string> For(string bindId) =>
            Situations.TryGetValue(bindId, out var set) ? set : new HashSet<string>();

        /// <summary>Marks a bind as applying to a situation, or takes the mark off again. Returns why it could not be saved, or null.</summary>
        public static string Toggle(string bindId, string tag)
        {
            // The change goes onto what the file says now, not onto what it said when first read.
            // Reading it is what the next line is for: a file that cannot be read leaves nothing
            // held, and nothing may then be written over it.
            Sync();
            _ = Situations;
            if (_situations == null)
            {
                Plugin.Log.LogWarning("Bindrune: situations.txt could not be read, so this change is not saved to it until it can.");
                return "situations.txt could not be read, so this was not saved; see the log";
            }

            if (!Situations.TryGetValue(bindId, out var set))
                Situations[bindId] = set = new HashSet<string>();

            // Add returns false when the tag was already there, which makes this a toggle.
            if (!set.Add(tag)) set.Remove(tag);
            if (set.Count == 0) Situations.Remove(bindId);

            return Save() ? null : "situations.txt could not be saved, see the log";
        }

        private static bool Save()
        {
            var written = TextStore.Write(FilePath,
                new[] { "# Situations you marked each bind as applying to." },
                _situations.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + string.Join("|", kv.Value.ToArray())));

            _stamp = TextStore.Stamp(FilePath);
            return written;
        }
    }
}
