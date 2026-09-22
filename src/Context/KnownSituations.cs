using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace Bindrune.Context
{
    /// <summary>
    /// Whose answer a bind's situations are, in the order they win. Distinct from BindSource,
    /// which is where the bind itself was found: a vanilla bind can still be described by you.
    /// </summary>
    public enum SituationSource
    {
        None,
        /// <summary>The mod declared them to Bindrune, which makes them final.</summary>
        Mod,
        /// <summary>You set them in the panel.</summary>
        You,
        /// <summary>Inferred from the mod's own Jotunn key hints.</summary>
        KeyHints,
        /// <summary>Listed in the shared list, known.txt.</summary>
        Pack,
        /// <summary>What the game itself does with one of its own binds.</summary>
        Game
    }

    /// <summary>
    /// Situations that did not come from you: declared by the mod itself, or listed in the shared
    /// list for the many mods that will never declare anything.
    ///
    /// Mods declare by attaching an object to their ConfigDescription tags, the same trick many
    /// mods already use for ConfigurationManager. It is read by reflection, so a mod
    /// needs no reference to Bindrune and the tag is simply inert when Bindrune is absent:
    ///
    ///     internal class BindruneAttributes
    ///     {
    ///         public string[] Situations;  // "Build placement", "Inventory" - see SituationTags
    ///         public string[] HeldItems;   // "skill:Pickaxes", "item:ood_remote"
    ///     }
    ///
    ///     Config.Bind("General", "DigKey", KeyCode.G,
    ///         new ConfigDescription("Dig", null,
    ///             new BindruneAttributes { HeldItems = new[] { "skill:Pickaxes" } }));
    /// </summary>
    public static class KnownSituations
    {
        private const string AttributeTypeName = "BindruneAttributes";

        private static readonly Dictionary<string, HashSet<string>> FromMods = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<string>> FromHints = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> _fromPack;

        private static string PackPath => TextStore.Ours(Paths.ConfigPath, "known.txt");

        /// <summary>Wiped at the start of every scan: a mod that stopped declaring should stop counting.</summary>
        public static void BeginScan()
        {
            FromMods.Clear();
            FromHints.Clear();
        }

        /// <summary>Records what a mod's key hints imply about when a bind is live.</summary>
        public static void RecordFromHints(string bindId, IEnumerable<string> situations)
        {
            if (!FromHints.TryGetValue(bindId, out var set))
                FromHints[bindId] = set = new HashSet<string>();

            foreach (var situation in situations) set.Add(situation);
        }

        /// <summary>
        /// Situations for a bind and where they came from. A mod that declares outright is final.
        /// Otherwise yours win, then what its key hints imply, then the shared list.
        /// </summary>
        public static HashSet<string> For(BindEntry bind, out SituationSource source)
        {
            var bindId = bind.Id;

            if (FromMods.TryGetValue(bindId, out var declared))
            {
                source = SituationSource.Mod;
                return declared;
            }

            var yours = SituationStore.For(bindId);
            if (yours.Count > 0)
            {
                source = SituationSource.You;
                return yours;
            }

            if (FromHints.TryGetValue(bindId, out var hinted) && hinted.Count > 0)
            {
                source = SituationSource.KeyHints;
                return hinted;
            }

            if (Pack.TryGetValue(bindId, out var packed) && packed.Count > 0)
            {
                source = SituationSource.Pack;
                return packed;
            }

            // The game's own binds describe themselves, through a curated table and through the
            // code that reads them. Answered here rather than copied into your own situations, so it
            // stays clear whose answer it is, and keeps working for binds a game update adds.
            if (bind.Source == BindSource.Vanilla)
            {
                var ours = GameDefaults(bind.Label);
                if (ours.Count > 0)
                {
                    source = SituationSource.Game;
                    return ours;
                }
            }

            source = SituationSource.None;
            return new HashSet<string>();
        }

        private static HashSet<string> GameDefaults(string buttonName)
        {
            if (VanillaDefaults.ByName.TryGetValue(buttonName, out var curated))
                return new HashSet<string>(curated);

            var derived = Discovery.ContextIndex.For(buttonName);
            if (derived == null) return new HashSet<string>();

            return new HashSet<string>(derived.Select(SituationTags.FromContext).Where(t => t != null));
        }

        /// <summary>Reads a BindruneAttributes object off a config entry, if the mod attached one.</summary>
        public static void ReadFrom(string bindId, ConfigEntryBase entry)
        {
            var tags = entry?.Description?.Tags;
            if (tags == null) return;

            foreach (var tag in tags)
            {
                if (tag == null || tag.GetType().Name != AttributeTypeName) continue;

                var situations = new HashSet<string>();
                foreach (var where in Strings(tag, "Situations")) situations.Add(where);
                foreach (var held in Strings(tag, "HeldItems")) situations.Add(EquippedItems.Prefix + held);

                if (situations.Count > 0) FromMods[bindId] = situations;
                return;
            }
        }

        /// <summary>Reads a string array off a field or a property, so either shape works mod-side.</summary>
        private static IEnumerable<string> Strings(object tag, string member)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            object value = null;

            try
            {
                var field = tag.GetType().GetField(member, flags);
                if (field != null) value = field.GetValue(tag);
                else
                {
                    var property = tag.GetType().GetProperty(member, flags);
                    if (property != null) value = property.GetValue(tag, null);
                }
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not read {member} from a mod's tag: {ex.Message}");
            }

            if (!(value is IEnumerable<string> strings)) return Enumerable.Empty<string>();
            return strings.Where(s => !string.IsNullOrEmpty(s));
        }

        private static Dictionary<string, HashSet<string>> Pack
        {
            get
            {
                if (_fromPack == null) LoadPack();
                return _fromPack;
            }
        }

        /// <summary>
        /// A shared list for mods that declare nothing. It lives in BepInEx/config on purpose:
        /// this is knowledge worth travelling with a profile, unlike your personal keys.
        /// </summary>
        private static void LoadPack()
        {
            _fromPack = new Dictionary<string, HashSet<string>>();

            if (!File.Exists(PackPath))
            {
                WriteTemplate();
                return;
            }

            foreach (var line in TextStore.Read(PackPath))
            {
                if (TextStore.IsNoise(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 2) continue;

                var situations = new HashSet<string>(
                    parts[1].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));

                if (parts.Length > 2)
                    foreach (var held in parts[2].Split(',').Select(s => s.Trim()).Where(s => s.Length > 0))
                        situations.Add(EquippedItems.Prefix + held);

                if (situations.Count > 0) _fromPack[parts[0].Trim()] = situations;
            }

            if (_fromPack.Count > 0)
                Plugin.Log.LogDebug($"Bindrune: {_fromPack.Count} binds described by the shared list.");
        }

        private static void WriteTemplate()
        {
            TextStore.Write(PackPath,
                new[]
                {
                    "# Situations for mods that do not describe their own binds.",
                    "# One per line:   <bind id><TAB><situations><TAB><held items>",
                    "#",
                    "# The bind id is the one Bindrune shows in its panel, for example:",
                    "#   cfg:com.example.mod:General:DigKey",
                    "#",
                    "# Situations are the names from the panel, comma separated:",
                    "#   World, Build placement, Build menu, Inventory, Container, Crafting, Map, Chat, Vehicle, Custom",
                    "#",
                    "# Held items are skill:<Skill>, type:<ItemType> or item:<prefab>, comma separated:",
                    "#   skill:Pickaxes, item:ood_remote",
                    "#",
                    "# Anything you set yourself in the panel wins over this list.",
                    "# A mod that declares its own situations wins over both."
                },
                new string[0]);
        }
    }
}
