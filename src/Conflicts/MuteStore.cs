using System.Collections.Generic;
using System.Linq;
using Bindrune.Personal;

namespace Bindrune.Conflicts
{
    /// <summary>
    /// Remembers which conflicts you have already decided are fine.
    ///
    /// Kept with your keys rather than in BepInEx/config: which clashes you have looked at and
    /// waved through is your own decision about your own modlist, and a profile sync would both
    /// hand you someone else's and delete yours. Held as pairs of bind ids, so a mute survives
    /// either bind moving to another key.
    /// </summary>
    public static class MuteStore
    {
        private static HashSet<string> _muted;

        private static HashSet<string> Muted =>
            _muted ?? (_muted = new HashSet<string>(PersonalStore.Lines(PersonalStore.Muted)));

        public static bool IsMuted(Conflict conflict) => Muted.Contains(conflict.PairKey);

        public static void Toggle(Conflict conflict)
        {
            if (!Muted.Add(conflict.PairKey)) Muted.Remove(conflict.PairKey);

            PersonalStore.Replace(PersonalStore.Muted, Muted.OrderBy(k => k));
        }
    }
}
