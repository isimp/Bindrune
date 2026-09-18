using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Bindrune.Context
{
    public class HeldItem
    {
        /// <summary>Every prefab that is this item. Mods clone gear for their own spawns, and a
        /// clone is still the same weapon as far as "what am I holding" is concerned.</summary>
        public List<string> Prefabs = new List<string>();

        public string Display;
        public string Type;
        public string Skill;

        /// <summary>Whether a player can craft, loot, pick up or buy it. Mods hand items out in
        /// ways we cannot see, so this sorts the list rather than filtering it.</summary>
        public bool Obtainable = true;

        public string Key => Prefabs.Count > 0 ? Prefabs[0] : "";
    }

    /// <summary>One line the panel can offer: a group such as "any pickaxe", or a single item.</summary>
    public class ItemChoice
    {
        /// <summary>The situation tag this line adds. Groups are always obtainable.</summary>
        public string Tag;

        public string Label;
        public bool Obtainable = true;
    }

    /// <summary>
    /// Everything a player can actually hold, read from the game's own databases so modded gear
    /// is included automatically. Used to say "this bind only matters with a pickaxe in hand".
    ///
    /// The databases are only populated in a world, so the list is cached to disk the first time
    /// it is seen and reused everywhere after that.
    /// </summary>
    public static class EquippedItems
    {
        public const string Prefix = "Equipped: ";

        // Item types that occupy a hand. Armour, materials and trophies cannot be "held".
        private static readonly HashSet<string> Holdable = new HashSet<string>
        {
            "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow",
            "Shield", "Torch", "Tool", "Attach_Atgeir"
        };

        private static List<HeldItem> _items;

        private static string CachePath => TextStore.Ours(Paths.CachePath, "items.txt");

        public static List<HeldItem> Items
        {
            get
            {
                if (_items == null) Load();
                return _items;
            }
        }

        public static bool Known => Items.Count > 0;

        private static int _scannedFor;

        /// <summary>
        /// Scans the item database if it is available, once per loaded world.
        ///
        /// The scan walks every prefab in ZNetScene calling GetComponent several times each,
        /// which with a large mod list is tens of thousands of calls, and it exists only to sort
        /// a dropdown, so it is not repeated on every panel open or rebind.
        /// </summary>
        public static void Refresh()
        {
            var db = ObjectDB.instance;
            if (db == null) return;

            var id = db.GetInstanceID();
            if (id == _scannedFor && Items.Count > 0) return;

            var scanned = Scan();
            if (scanned == null || scanned.Count == 0) return;

            _scannedFor = id;
            _items = scanned;
            Save();
        }

        private static List<HeldItem> Scan()
        {
            var db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0) return null;

            var candidates = new List<HeldItem>();
            var iconless = 0;

            foreach (var prefab in db.m_items)
            {
                if (prefab == null) continue;

                var drop = prefab.GetComponent<ItemDrop>();
                var shared = drop?.m_itemData?.m_shared;
                if (shared == null) continue;

                var type = shared.m_itemType.ToString();
                if (!Holdable.Contains(type)) continue;

                // Creature attacks are modelled as weapons too - a boar's bite is a
                // OneHandedWeapon in the same database. Real gear needs an inventory icon.
                if (!HasIcon(shared))
                {
                    iconless++;
                    continue;
                }

                var display = Localize(shared.m_name, prefab.name);
                if (string.IsNullOrEmpty(display.Trim()))
                {
                    iconless++;
                    continue;
                }

                candidates.Add(new HeldItem
                {
                    Prefabs = new List<string> { prefab.name },
                    Display = display,
                    Type = type,
                    Skill = shared.m_skillType.ToString()
                });
            }

            // Marked, never dropped: a mod can grant an item through its own piece or quest, and
            // that item is exactly the one you would want to tag a bind with.
            var reachable = Reachable(db);
            var unreachable = 0;
            if (reachable != null && reachable.Count > 0)
                foreach (var candidate in candidates)
                {
                    candidate.Obtainable = candidate.Prefabs.Any(reachable.Contains);
                    if (!candidate.Obtainable) unreachable++;
                }

            // One entry per real weapon: clones share a name, a type and a skill, and differ
            // only in which mod spawned them.
            var merged = candidates
                .GroupBy(c => c.Display + "|" + c.Type + "|" + c.Skill)
                .Select(g => new HeldItem
                {
                    Prefabs = g.SelectMany(c => c.Prefabs).Distinct().OrderBy(p => p.Length).ToList(),
                    Display = g.First().Display,
                    Type = g.First().Type,
                    Skill = g.First().Skill,
                    Obtainable = g.Any(c => c.Obtainable)
                })
                .OrderBy(i => i.Display)
                .ToList();

            Plugin.Log.LogDebug(
                $"Bindrune: items - {iconless} without icons, {candidates.Count - merged.Count} clones merged, " +
                $"{merged.Count} kept ({unreachable} of them not obtainable by normal means).");

            return merged;
        }

        /// <summary>
        /// Prefabs a player can reach: crafted from a recipe, dropped by a creature, left by
        /// something destroyed, found in a container, picked up from the world, or bought.
        /// Anything else in the database is a developer or NPC-only entry.
        /// </summary>
        private static HashSet<string> Reachable(ObjectDB db)
        {
            var reachable = new HashSet<string>();

            try
            {
                if (db.m_recipes != null)
                    foreach (var recipe in db.m_recipes)
                        if (recipe?.m_item != null) reachable.Add(recipe.m_item.gameObject.name);

                var scene = ZNetScene.instance;
                if (scene?.m_prefabs == null) return reachable;

                foreach (var prefab in scene.m_prefabs)
                {
                    if (prefab == null) continue;

                    var characterDrop = prefab.GetComponent<CharacterDrop>();
                    if (characterDrop?.m_drops != null)
                        foreach (var drop in characterDrop.m_drops)
                            if (drop?.m_prefab != null) reachable.Add(drop.m_prefab.name);

                    var destroyed = prefab.GetComponent<DropOnDestroyed>();
                    AddTable(reachable, destroyed?.m_dropWhenDestroyed);

                    var container = prefab.GetComponent<Container>();
                    AddTable(reachable, container?.m_defaultItems);

                    var pickable = prefab.GetComponent<Pickable>();
                    if (pickable?.m_itemPrefab != null) reachable.Add(pickable.m_itemPrefab.name);

                    var trader = prefab.GetComponent<Trader>();
                    if (trader?.m_items != null)
                        foreach (var sold in trader.m_items)
                            if (sold?.m_prefab != null) reachable.Add(sold.m_prefab.gameObject.name);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not work out which items are obtainable ({ex.Message}); listing all of them.");
                return null;
            }

            return reachable;
        }

        private static void AddTable(HashSet<string> reachable, DropTable table)
        {
            if (table?.m_drops == null) return;
            foreach (var drop in table.m_drops)
                if (drop.m_item != null) reachable.Add(drop.m_item.name);
        }

        private static bool HasIcon(ItemDrop.ItemData.SharedData shared)
        {
            var icons = shared.m_icons;
            if (icons == null) return false;

            foreach (var icon in icons)
                if (icon != null) return true;

            return false;
        }

        private static string Localize(string token, string fallback)
        {
            try
            {
                if (Localization.instance != null)
                {
                    var text = Localization.instance.Localize(token);

                    // Valheim returns "[token]" when a translation is missing, which is not a
                    // name anyone would recognise in a list.
                    if (!string.IsNullOrEmpty(text) && !(text.StartsWith("[") && text.EndsWith("]")))
                        return text;
                }
            }
            catch
            {
                // Localisation is not ready yet.
            }

            // The prefab name beats an untranslated token: "TorchMist" over "item_torchmist".
            return fallback;
        }

        /// <summary>
        /// Group tags first ("any pickaxe"), then every individual item, the ones a player can
        /// actually get before the ones we found no route to. Unobtainable items are sorted down
        /// and never dropped: mods hand items out in ways we cannot trace.
        /// </summary>
        public static List<ItemChoice> Choices()
        {
            var choices = new List<ItemChoice>();

            foreach (var skill in Items.Select(i => i.Skill).Distinct().Where(s => s != "None").OrderBy(s => s))
                choices.Add(new ItemChoice { Tag = Prefix + "skill:" + skill, Label = SkillLabel(skill) });

            foreach (var type in Items.Select(i => i.Type).Distinct().OrderBy(t => t))
                choices.Add(new ItemChoice { Tag = Prefix + "type:" + type, Label = "Any " + Spaced(type) });

            // OrderByDescending is stable, so each half keeps the alphabetical order Items has.
            foreach (var item in Items.OrderByDescending(i => i.Obtainable))
                choices.Add(new ItemChoice
                {
                    Tag = Prefix + "item:" + item.Key,
                    Label = item.Display,
                    Obtainable = item.Obtainable
                });

            return choices;
        }

        public static string Describe(string tag)
        {
            var value = Value(tag);
            if (value == null) return tag;

            if (value.StartsWith("skill:")) return SkillLabel(value.Substring(6));
            if (value.StartsWith("type:")) return "Any " + Spaced(value.Substring(5));

            var prefab = value.Substring(5);
            var item = Items.FirstOrDefault(i => i.Prefabs.Contains(prefab));
            return item != null ? item.Display : prefab;
        }

        /// <summary>
        /// The concrete prefabs a tag covers, so "any pickaxe" and "Iron pickaxe" are correctly
        /// seen as overlapping rather than as two unrelated strings.
        /// </summary>
        public static HashSet<string> Expand(string tag)
        {
            var value = Value(tag);
            if (value == null) return null;

            if (value.StartsWith("skill:"))
            {
                var skill = value.Substring(6);
                return new HashSet<string>(Items.Where(i => i.Skill == skill).SelectMany(i => i.Prefabs));
            }

            if (value.StartsWith("type:"))
            {
                var type = value.Substring(5);
                return new HashSet<string>(Items.Where(i => i.Type == type).SelectMany(i => i.Prefabs));
            }

            var prefab = value.Substring(5);
            var item = Items.FirstOrDefault(i => i.Prefabs.Contains(prefab));
            return new HashSet<string>(item != null ? item.Prefabs : new List<string> { prefab });
        }

        public static bool IsHeldTag(string tag) => tag != null && tag.StartsWith(Prefix, StringComparison.Ordinal);

        private static string Value(string tag) => IsHeldTag(tag) ? tag.Substring(Prefix.Length) : null;

        /// <summary>
        /// Skill names are plural, which reads badly as "any blood magic". Name the
        /// thing you are holding instead.
        /// </summary>
        private static string SkillLabel(string skill)
        {
            switch (skill)
            {
                case "Swords": return "Any sword";
                case "Knives": return "Any knife";
                case "Clubs": return "Any club";
                case "Polearms": return "Any polearm";
                case "Spears": return "Any spear";
                case "Axes": return "Any axe";
                case "Bows": return "Any bow";
                case "Crossbows": return "Any crossbow";
                case "Pickaxes": return "Any pickaxe";
                case "Blocking": return "Any shield";
                case "Unarmed": return "Any fist weapon";
                case "WoodCutting": return "Any woodcutting axe";
                case "ElementalMagic": return "Any elemental magic weapon";
                case "BloodMagic": return "Any blood magic weapon";
                default: return "Any " + Spaced(skill) + " weapon";
            }
        }

        private static string Spaced(string type)
        {
            var text = type.Replace("_", " ");
            for (var i = text.Length - 1; i > 0; i--)
                if (char.IsUpper(text[i]) && text[i - 1] != ' ')
                    text = text.Insert(i, " ");
            return text.ToLowerInvariant();
        }

        private static void Load()
        {
            _items = new List<HeldItem>();

            foreach (var line in TextStore.Read(CachePath))
            {
                if (TextStore.IsNoise(line)) continue;

                var parts = line.Split('|');
                if (parts.Length < 4) continue;

                _items.Add(new HeldItem
                {
                    Prefabs = parts[0].Split(',').Where(p => p.Length > 0).ToList(),
                    Display = parts[1],
                    Type = parts[2],
                    Skill = parts[3],
                    Obtainable = parts.Length < 5 || parts[4] != "0"
                });
            }
        }

        private static void Save() =>
            TextStore.Write(CachePath,
                new[] { "# Holdable items, rebuilt from the game whenever you load a world." },
                _items.Select(i =>
                    $"{string.Join(",", i.Prefabs.ToArray())}|{i.Display}|{i.Type}|{i.Skill}|{(i.Obtainable ? "1" : "0")}"));
    }
}
