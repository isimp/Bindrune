using System;
using System.Collections.Generic;
using System.Linq;
using Bindrune.Personal;

namespace Bindrune.Conflicts
{
    /// <summary>
    /// Remembers which conflicts you have already decided are fine.
    ///
    /// A mute is about the clash you looked at, not about the pair for good. It records what that
    /// clash was, and stops applying once the same pair does something worse - a note that becomes
    /// a hard clash, or one that turns out to apply in the same situation, is something you have
    /// not seen yet. It is also dropped once the clash is gone, so moving a key away and back
    /// reports it again rather than hiding it on the strength of an old decision.
    ///
    /// Kept with your keys rather than in BepInEx/config: which clashes you have waved through is
    /// your own decision about your own modlist, and a profile sync would both hand you someone
    /// else's and delete yours. Held as pairs of bind ids, so a mute survives either bind moving
    /// to another key.
    /// </summary>
    public static class MuteStore
    {
        /// <summary>What was waved through, so a worse version of it can be told apart.</summary>
        private class Mute
        {
            /// <summary>Null in a line from before mutes recorded this, which mutes the pair outright.</summary>
            public Severity? Severity;
            public bool Confirmed;
        }

        private static Dictionary<string, Mute> _muted;

        private static Dictionary<string, Mute> Muted => _muted ?? (_muted = Load());

        static MuteStore() => PersonalStore.Reloaded += () => _muted = null;

        public static bool IsMuted(Conflict conflict)
        {
            if (!Muted.TryGetValue(conflict.PairKey, out var mute)) return false;

            // Written before a mute recorded what it dismissed, so it can only mean all of it.
            if (mute.Severity == null) return true;

            // Severity counts down: Hard is worse than Soft, which is worse than Note.
            if (conflict.Severity < mute.Severity.Value) return false;

            // Finding out that both binds are live in the same place is worse news than not
            // knowing, so a mute given before that was known does not cover it either.
            return !conflict.Confirmed || mute.Confirmed;
        }

        public static void Toggle(Conflict conflict)
        {
            PersonalStore.Sync();

            // By what the button says, not by what is on file: a mute that has stopped applying
            // reads as unmuted, and clicking it has to mute what the clash is now.
            if (IsMuted(conflict)) Muted.Remove(conflict.PairKey);
            else Muted[conflict.PairKey] = new Mute { Severity = conflict.Severity, Confirmed = conflict.Confirmed };

            Save();
        }

        /// <summary>
        /// Brings the list up to date against a scan: old lines are recorded properly, and mutes
        /// for clashes that no longer exist are dropped, so a decision about one arrangement of
        /// your keys does not quietly carry over to a future one.
        ///
        /// Both only where the binds were there to be compared: mods bind their configs at their
        /// own pace, and at the main menu a bind that appears once a world loads is missing rather
        /// than gone. Judging those would throw away the mute of anyone who opened the panel early.
        /// </summary>
        public static void Settle(IEnumerable<string> compared, IEnumerable<Conflict> found)
        {
            PersonalStore.Sync();

            if (Muted.Count == 0) return;

            var present = new HashSet<string>(compared);
            var live = new Dictionary<string, Conflict>();
            foreach (var conflict in found) live[conflict.PairKey] = conflict;

            var recorded = Record(live);

            var resolved = Muted.Keys.Where(pair => !live.ContainsKey(pair) && BothPresent(pair, present)).ToList();
            foreach (var pair in resolved) Muted.Remove(pair);

            if (recorded > 0 || resolved.Count > 0) Save();
        }

        /// <summary>
        /// Gives an old line the clash it is sitting on. Those lines say only that the pair is
        /// fine, from before a mute recorded what it dismissed, and the clash in front of us is
        /// what they have been hiding: writing that down hides exactly as much as they do today
        /// and no more, so anything worse than it is reported from here on.
        /// </summary>
        private static int Record(Dictionary<string, Conflict> live)
        {
            var older = Muted.Where(m => m.Value.Severity == null).Select(m => m.Key).ToList();
            var recorded = 0;

            foreach (var pair in older)
            {
                if (!live.TryGetValue(pair, out var conflict)) continue;

                Muted[pair] = new Mute { Severity = conflict.Severity, Confirmed = conflict.Confirmed };
                recorded++;
            }

            return recorded;
        }

        private static bool BothPresent(string pair, HashSet<string> present)
        {
            var parts = pair.Split('|');

            // Not a pair we can read is not a pair we should throw away.
            return parts.Length == 2 && present.Contains(parts[0]) && present.Contains(parts[1]);
        }

        private static Dictionary<string, Mute> Load()
        {
            var muted = new Dictionary<string, Mute>();

            foreach (var line in PersonalStore.Lines(PersonalStore.Muted))
            {
                var parts = line.Split('\t');
                var pair = parts[0].Trim();
                if (pair.Length == 0) continue;

                var mute = new Mute();

                // Anything we cannot read where the severity should be leaves it unset, which
                // mutes the pair outright: the safe reading of a line someone typed by hand.
                if (parts.Length > 1 && Enum.TryParse(parts[1].Trim(), true, out Severity severity)
                                     && Enum.IsDefined(typeof(Severity), severity))
                {
                    mute.Severity = severity;
                    mute.Confirmed = parts.Length > 2 && parts[2].Trim() == "1";
                }

                muted[pair] = mute;
            }

            return muted;
        }

        /// <summary>A line as the file holds it: the pair, then what was waved through if it was recorded.</summary>
        private static string Line(string pair, Mute mute) =>
            mute.Severity == null
                ? pair
                : $"{pair}\t{mute.Severity.ToString().ToLowerInvariant()}\t{(mute.Confirmed ? "1" : "0")}";

        private static void Save() =>
            PersonalStore.Replace(PersonalStore.Muted, Muted.OrderBy(m => m.Key).Select(m => Line(m.Key, m.Value)));
    }
}
