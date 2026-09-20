using System.Collections.Generic;
using System.Linq;

namespace Bindrune.Conflicts
{
    /// <summary>
    /// Every conflict found in the last scan, and which binds each one touches.
    ///
    /// Kept here rather than in the panel because "what clashes with this bind" is a fact about
    /// the binds; the panel only decides how to draw it. Muting is applied on the way out rather
    /// than baked in, so unmuting one is a redraw and not a rescan.
    /// </summary>
    public static class ConflictIndex
    {
        private static readonly List<Conflict> None = new List<Conflict>();

        private static Dictionary<string, List<Conflict>> _byBind = new Dictionary<string, List<Conflict>>();

        public static List<Conflict> All { get; private set; } = new List<Conflict>();

        public static void Rebuild(IEnumerable<BindEntry> binds)
        {
            var compared = binds as IList<BindEntry> ?? binds.ToList();

            All = ConflictEngine.Find(compared);
            _byBind = new Dictionary<string, List<Conflict>>();

            foreach (var conflict in All)
            {
                Attach(conflict.A.Id, conflict);
                Attach(conflict.B.Id, conflict);
            }

            // Here rather than in the panel because what is left of a mute is a fact about the
            // binds too, and this is the one place that knows both what was compared and what
            // came of it.
            MuteStore.Settle(compared.Select(b => b.Id), All);
        }

        /// <summary>Everything found against a bind, muted or not.</summary>
        public static List<Conflict> For(string bindId) =>
            _byBind.TryGetValue(bindId, out var list) ? list : None;

        /// <summary>The ones that still count, which is what the marks and counters are about.</summary>
        public static List<Conflict> Live(string bindId) =>
            For(bindId).Where(c => !MuteStore.IsMuted(c)).ToList();

        public static int MutedCount(string bindId) => For(bindId).Count(MuteStore.IsMuted);

        /// <summary>The severity a bind is marked with, or null when nothing is left to say.</summary>
        public static Severity? Worst(string bindId)
        {
            var live = Live(bindId);
            return live.Count == 0 ? (Severity?)null : live.Min(c => c.Severity);
        }

        private static void Attach(string bindId, Conflict conflict)
        {
            if (!_byBind.TryGetValue(bindId, out var list))
                _byBind[bindId] = list = new List<Conflict>();

            list.Add(conflict);
        }
    }
}
