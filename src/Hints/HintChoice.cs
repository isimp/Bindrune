using System;
using System.Collections.Generic;
using System.Linq;
using Bindrune.Personal;

namespace Bindrune.Hints
{
    /// <summary>
    /// The binds you asked Bindrune to show on screen. Opt-in, one id per line.
    ///
    /// Kept with your keys rather than in BepInEx/config: which of your binds are worth a line on
    /// your screen is as personal as the keys themselves, and a profile sync would replace the
    /// list with the profile owner's.
    /// </summary>
    public static class HintChoice
    {
        private static HashSet<string> _chosen;

        /// <summary>Raised when the set changes, so the overlay can rebuild without polling it.</summary>
        public static Action Changed;

        private static HashSet<string> Chosen =>
            _chosen ?? (_chosen = new HashSet<string>(PersonalStore.Lines(PersonalStore.Hints)));

        // A list read again from the file can show different hints, so the overlay hears of it too.
        static HintChoice() => PersonalStore.Reloaded += () =>
        {
            _chosen = null;
            Changed?.Invoke();
        };

        public static int Count => Chosen.Count;

        public static bool Shows(string bindId) => Chosen.Contains(bindId);

        public static void Toggle(string bindId)
        {
            PersonalStore.Sync();
            if (!Chosen.Add(bindId)) Chosen.Remove(bindId);
            Save();
        }

        /// <summary>Adds several at once, for "show all of this mod's binds".</summary>
        public static void Add(IEnumerable<string> bindIds)
        {
            PersonalStore.Sync();
            var added = bindIds.Count(id => Chosen.Add(id));
            if (added > 0) Save();
        }

        public static void Remove(IEnumerable<string> bindIds)
        {
            PersonalStore.Sync();
            var removed = bindIds.Count(id => Chosen.Remove(id));
            if (removed > 0) Save();
        }

        private static void Save()
        {
            PersonalStore.Replace(PersonalStore.Hints, Chosen.OrderBy(id => id));
            Changed?.Invoke();
        }
    }
}
