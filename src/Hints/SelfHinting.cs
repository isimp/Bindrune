using System.Collections.Generic;

namespace Bindrune.Hints
{
    /// <summary>
    /// Whether a bind already puts itself on screen, so the panel can say "no need" rather than
    /// let you add a second copy of a hint the game or the mod is drawing already.
    ///
    /// It only ever says so - the toggle stays available either way. For mods this is exact, but
    /// for the game it cannot be: vanilla hints are a handful of prefab objects on the KeyHints
    /// component (m_buildHints, m_combatHints, m_inventoryHints and so on), with no list of which
    /// binds they name. The vanilla half is therefore a hand-checked guess, and is worded as one.
    /// </summary>
    public static class SelfHinting
    {
        private enum Kind
        {
            No,
            /// <summary>The mod registered a Jotunn key hint for it. Read, not guessed.</summary>
            Mod,
            /// <summary>The game usually draws this one itself. Curated, so treat it as a hint.</summary>
            GameUsually
        }

        private static readonly HashSet<string> FromKeyHints = new HashSet<string>();

        /// <summary>
        /// Game binds the HUD names in one of its hint panels. Deliberately short: only the ones
        /// worth talking a player out of duplicating, and none of the ones we are unsure about.
        /// </summary>
        private static readonly HashSet<string> GameDraws = new HashSet<string>
        {
            "Attack", "SecondaryAttack", "Block", "Use",
            "BuildMenu", "AltPlace", "Remove", "TabLeft", "TabRight",
            "Inventory"
        };

        /// <summary>Wiped at the start of every scan, like the rest of what a scan discovers.</summary>
        public static void BeginScan() => FromKeyHints.Clear();

        public static void Record(string bindId) => FromKeyHints.Add(bindId);

        private static Kind For(BindEntry bind)
        {
            if (FromKeyHints.Contains(bind.Id)) return Kind.Mod;
            if (bind.Source == BindSource.Vanilla && GameDraws.Contains(bind.Label)) return Kind.GameUsually;
            return Kind.No;
        }

        /// <summary>What to tell the player, or null when nothing is worth saying.</summary>
        public static string Describe(BindEntry bind)
        {
            switch (For(bind))
            {
                case Kind.Mod:
                    return $"{bind.OwnerName} already shows this one in the game's own key hints.";
                case Kind.GameUsually:
                    return "The game usually shows this one in the corner already.";
                default:
                    return null;
            }
        }
    }
}
