using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace Bindrune.Context
{
    /// <summary>
    /// The situations you said a bind applies to: both the "where" tags and the held items,
    /// stored as one set per bind because that is how they are asked for. See SituationTags.
    /// </summary>
    public static class SituationStore
    {
        private static Dictionary<string, HashSet<string>> _situations;

        private static string FilePath => TextStore.Ours(Paths.ConfigPath, "situations.txt");

        private static Dictionary<string, HashSet<string>> Situations
        {
            get
            {
                if (_situations != null) return _situations;

                _situations = new Dictionary<string, HashSet<string>>();

                foreach (var line in TextStore.Read(FilePath))
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

        public static HashSet<string> For(string bindId) =>
            Situations.TryGetValue(bindId, out var set) ? set : new HashSet<string>();

        public static void Toggle(string bindId, string tag)
        {
            if (!Situations.TryGetValue(bindId, out var set))
                Situations[bindId] = set = new HashSet<string>();

            // Add returns false when the tag was already there, which makes this a toggle.
            if (!set.Add(tag)) set.Remove(tag);
            if (set.Count == 0) Situations.Remove(bindId);

            Save();
        }

        private static void Save() =>
            TextStore.Write(FilePath,
                new[] { "# Situations you marked each bind as applying to." },
                _situations.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + string.Join("|", kv.Value.ToArray())));
    }
}
