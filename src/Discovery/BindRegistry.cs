using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace Bindrune.Discovery
{
    internal static class BindRegistry
    {
        public static List<BindEntry> All { get; private set; } = new List<BindEntry>();

        /// <summary>
        /// The other bind the game reads for the same thing: the same name with Alt on the end,
        /// as each hotbar slot has. Null unless that bind exists and is one the player can set.
        /// </summary>
        public static BindEntry AlternateOf(BindEntry bind)
        {
            if (bind.Source != BindSource.Vanilla) return null;

            var id = BindIds.Vanilla(bind.Label + "Alt");
            var alternate = All.FirstOrDefault(b => b.Id == id);
            return alternate != null && alternate.Editable ? alternate : null;
        }

        public static void Refresh()
        {
            // Jotunn runs first: it reports which config entries back its buttons, and those
            // entries are then skipped by the config scan so a bind is never listed twice.
            Context.KnownSituations.BeginScan();
            Hints.SelfHinting.BeginScan();
            KeyLabels.Forget();

            var backingEntries = new HashSet<ConfigEntryBase>();
            var claimedButtonNames = new HashSet<string>();
            var jotunn = Guarded("Jotunn", () => JotunnScanner.Scan(backingEntries, claimedButtonNames));
            var configs = Guarded("config", () => BepInExScanner.Scan(backingEntries));
            var vanilla = Guarded("vanilla", () => VanillaScanner.Scan(claimedButtonNames));
            var gamepad = Guarded("gamepad", GamepadButtons.Scan);
            var game = Guarded("game keys", () => GameKeys.Scan(jotunn.Concat(configs)));

            All = vanilla.Concat(game).Concat(gamepad).Concat(jotunn).Concat(configs)
                .OrderBy(b => b.OwnerName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Section, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();

            JotunnHints.Read(All);

            // Mods bind their configs at different times, so there is no single moment when every
            // entry exists. Reconciling on each scan catches the late ones; it is a no-op when
            // nothing has drifted.
            Personal.PersonalKeys.Reconcile();

            var breakdown = string.Join(", ", All.GroupBy(b => b.Source)
                .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
                .Select(g => $"{g.Key} {g.Count()}")
                .ToArray());
            Plugin.Log.LogDebug($"Bindrune: {All.Count} binds ({breakdown}), {All.Count(b => b.Internal)} internal.");
        }

        /// <summary>One misbehaving mod must not cost us every other mod's binds.</summary>
        private static List<BindEntry> Guarded(string what, Func<List<BindEntry>> scan)
        {
            try
            {
                return scan();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: {what} scan failed: {ex}");
                return new List<BindEntry>();
            }
        }
    }
}
