using System;
using System.Collections.Generic;
using System.Linq;
using Bindrune.Context;

namespace Bindrune.Hints
{
    /// <summary>
    /// Which situations are true right now, read from the game rather than assumed.
    ///
    /// Every tag in SituationTags has a public state to ask, so nothing here is a heuristic:
    /// Player.InPlaceMode, Hud.IsPieceSelectionVisible, InventoryGui.IsVisible/IsContainerOpen,
    /// Player.GetCurrentCraftingStation, Minimap.IsOpen, Chat.HasFocus, Player.IsAttachedToShip,
    /// and the two equipped items. "Custom" is the exception and is documented where it is used.
    ///
    /// Sampled on a cadence rather than per frame: this runs while you play, and reading it sixty
    /// times a second to redraw a list that changes when you open a menu would be waste.
    /// </summary>
    public static class SituationNow
    {
        private const float Interval = 0.2f;

        private static float _nextSample;
        private static HashSet<string> _where = new HashSet<string>();
        private static HashSet<string> _held = new HashSet<string>();

        // Changes exactly when what is on screen should change, and is cheap to compare.
        private static string _signature = "";

        /// <summary>True while there is a player to have a situation at all.</summary>
        public static bool InGame { get; private set; }

        /// <summary>Re-reads the world if enough time has passed. Returns true when it changed.</summary>
        public static bool Sample()
        {
            if (UnityEngine.Time.realtimeSinceStartup < _nextSample) return false;
            _nextSample = UnityEngine.Time.realtimeSinceStartup + Interval;

            var where = new HashSet<string>();
            var held = new HashSet<string>();
            InGame = Read(where, held);

            var signature = InGame
                ? string.Join(",", where.OrderBy(w => w).Concat(held.OrderBy(h => h)).ToArray())
                : "";

            if (signature == _signature) return false;

            _where = where;
            _held = held;
            _signature = signature;
            return true;
        }

        private static bool Read(HashSet<string> where, HashSet<string> held)
        {
            try
            {
                var player = Player.m_localPlayer;
                if (player == null || player.IsDead()) return false;

                var inventory = InventoryGui.IsVisible();
                var map = Minimap.instance != null && Minimap.IsOpen();

                if (inventory)
                {
                    where.Add("Inventory");
                    if (InventoryGui.instance != null && InventoryGui.instance.IsContainerOpen()) where.Add("Container");
                    if (player.GetCurrentCraftingStation() != null) where.Add("Crafting");
                }

                var typing = Chat.instance != null && Chat.instance.HasFocus();

                if (map) where.Add("Map");
                if (Hud.IsPieceSelectionVisible()) where.Add("Build menu");
                if (player.InPlaceMode()) where.Add("Build placement");
                if (typing) where.Add("Chat");
                if (player.IsAttachedToShip()) where.Add("Vehicle");

                // Playing, as opposed to looking at something that covers the screen or typing
                // into it. Build placement is still the world, so it does not count against this;
                // the chat box having the keyboard does, because then nothing else is getting it.
                if (!inventory && !map && !typing) where.Add("World");

                // The accessors are protected, so read the fields the game keeps them in. Both
                // hands, because a bind can want the shield as easily as the weapon.
                Add(held, Equipped(player, RightItem));
                Add(held, Equipped(player, LeftItem));
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Bindrune: could not read the current situation: {ex.Message}");
                return false;
            }
        }

        // Resolved once. This runs several times a second for as long as you play, which is no
        // place to be looking a field up by name.
        private static readonly System.Reflection.FieldInfo RightItem =
            HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_rightItem");

        private static readonly System.Reflection.FieldInfo LeftItem =
            HarmonyLib.AccessTools.Field(typeof(Humanoid), "m_leftItem");

        private static ItemDrop.ItemData Equipped(Humanoid player, System.Reflection.FieldInfo field) =>
            field?.GetValue(player) as ItemDrop.ItemData;

        private static void Add(HashSet<string> held, ItemDrop.ItemData item)
        {
            // The prefab name is what EquippedItems keys on, so a tag and a held item can be
            // compared without going through display names, which are localised.
            var prefab = item?.m_dropPrefab;
            if (prefab != null) held.Add(prefab.name);
        }

        /// <summary>
        /// Whether a bind is live right now. The two axes are independent and both must pass, the
        /// same rule ConflictEngine.NeverTogether uses to decide two binds can never meet: a bind
        /// that says where it applies has to be in one of those places, and one that needs
        /// something in hand has to have it.
        ///
        /// Saying nothing means no objection, so a bind with no situations shows whenever the
        /// overlay is up. "Custom" is the same: it stands for a mod's own mode, which nothing out
        /// here can see, so it is not treated as a place that could fail to match.
        /// </summary>
        public static bool Shows(BindEntry bind)
        {
            if (!InGame) return false;

            var situations = KnownSituations.For(bind, out SituationSource _);
            if (situations.Count == 0) return true;

            var wanted = situations.Where(t => !EquippedItems.IsHeldTag(t) && t != "Custom").ToList();
            if (wanted.Count > 0 && !wanted.Any(_where.Contains)) return false;

            var items = situations.Where(EquippedItems.IsHeldTag).ToList();
            if (items.Count == 0) return true;

            return items.Select(EquippedItems.Expand).Any(prefabs => prefabs != null && prefabs.Overlaps(_held));
        }
    }
}
