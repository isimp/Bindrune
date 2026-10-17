using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace Bindrune.SelfTest
{
    /// <summary>
    /// How a mod tells Bindrune when its bind is live: an object of this name on the setting's
    /// tags, read by name, so the mod needs no reference to Bindrune. The self-test's own
    /// settings carry it as any mod would.
    /// </summary>
    internal sealed class BindruneAttributes
    {
        public string[] Situations;
        public string[] HeldItems;
    }

    /// <summary>
    /// The settings the self-test judges and rebinds: its own, bound in its own config file for
    /// the run and removed after. They sit on F13 to F15, keys few keyboards have, so they clash
    /// with each other and seldom with anything of yours; the checks look at the pairs alone,
    /// whatever else shares the keys. Each pair stands for one rule of how clashes are told.
    /// </summary>
    internal static class TestBinds
    {
        public const string Section = "Self-test";

        private sealed class Spec
        {
            public string Key;
            public object Default;
            public BindruneAttributes Tag;
        }

        private static KeyboardShortcut Shortcut(KeyCode key, KeyCode modifier) => new KeyboardShortcut(key, modifier);

        private static BindruneAttributes Where(string situation) => new BindruneAttributes { Situations = new[] { situation } };

        private static readonly Spec[] Specs =
        {
            // The same key and modifier, saying nothing of when: a hard clash.
            new Spec { Key = "HardA", Default = Shortcut(KeyCode.F13, KeyCode.LeftAlt) },
            new Spec { Key = "HardB", Default = Shortcut(KeyCode.F13, KeyCode.LeftAlt) },
            // A single key with no modifier field beside the same key with one: soft.
            new Spec { Key = "SoftKey", Default = KeyCode.F14 },
            new Spec { Key = "SoftCombo", Default = Shortcut(KeyCode.F14, KeyCode.LeftAlt) },
            // The same key with different modifiers, both checked exactly: a note.
            new Spec { Key = "NoteA", Default = Shortcut(KeyCode.F15, KeyCode.LeftAlt) },
            new Spec { Key = "NoteB", Default = Shortcut(KeyCode.F15, KeyCode.LeftControl) },
            // The same combo, live in places that never meet: ruled out.
            new Spec { Key = "OnMap", Default = Shortcut(KeyCode.F13, KeyCode.LeftControl), Tag = Where("Map") },
            new Spec { Key = "InBuildMenu", Default = Shortcut(KeyCode.F13, KeyCode.LeftControl), Tag = Where("Build menu") },
            // The same combo, both live on the map: a hard clash both confirm.
            new Spec { Key = "MapC", Default = Shortcut(KeyCode.F14, KeyCode.LeftControl), Tag = Where("Map") },
            new Spec { Key = "MapD", Default = Shortcut(KeyCode.F14, KeyCode.LeftControl), Tag = Where("Map") },
            // One says where, the other what is in hand: nothing is proven either way.
            new Spec { Key = "PlaceX", Default = Shortcut(KeyCode.F15, KeyCode.LeftShift), Tag = Where("Map") },
            new Spec { Key = "ItemY", Default = Shortcut(KeyCode.F15, KeyCode.LeftShift), Tag = new BindruneAttributes { HeldItems = new[] { "skill:Pickaxes" } } },
            // A hotkey kept as free text, which is shown and never changed from Bindrune.
            new Spec { Key = "TextKey", Default = "F15" },
            // A modifier on its own, which mods share on purpose: never a clash.
            new Spec { Key = "ModifierA", Default = KeyCode.RightShift },
            new Spec { Key = "ModifierB", Default = KeyCode.RightShift },
            // A pair you describe in the panel.
            new Spec { Key = "YouA", Default = Shortcut(KeyCode.F13, KeyCode.RightAlt) },
            new Spec { Key = "YouB", Default = Shortcut(KeyCode.F13, KeyCode.RightAlt) },
            // The bind your own key and the profile's are tried on.
            new Spec { Key = "Yours", Default = Shortcut(KeyCode.F13, KeyCode.RightControl) },
            // The bind pinned to the hints.
            new Spec { Key = "HintMe", Default = Shortcut(KeyCode.F14, KeyCode.RightAlt) },
        };

        private static readonly List<ConfigEntryBase> Bound = new List<ConfigEntryBase>();

        /// <summary>A test setting's bind id, as Bindrune builds it.</summary>
        public static string Id(string key) => BindIds.Config(SelfTestPlugin.Guid, Section, key);

        /// <summary>Every test setting's key.</summary>
        public static IEnumerable<string> Keys()
        {
            foreach (var spec in Specs) yield return spec.Key;
        }

        /// <summary>Binds every test setting at its default, whatever an earlier run left in the file.</summary>
        public static void Bind(ConfigFile config)
        {
            Bound.Clear();
            foreach (var spec in Specs)
            {
                var description = new ConfigDescription("A setting of the Bindrune self-test, there only while it runs.", null,
                    spec.Tag == null ? System.Array.Empty<object>() : new object[] { spec.Tag });

                ConfigEntryBase entry;
                switch (spec.Default)
                {
                    case KeyboardShortcut shortcut: entry = config.Bind(Section, spec.Key, shortcut, description); break;
                    case KeyCode key: entry = config.Bind(Section, spec.Key, key, description); break;
                    default: entry = config.Bind(Section, spec.Key, (string)spec.Default, description); break;
                }

                entry.BoxedValue = entry.DefaultValue;
                Bound.Add(entry);
            }
            config.Save();
        }

        /// <summary>Removes the test settings from the self-test's config, file included.</summary>
        public static void Unbind(ConfigFile config)
        {
            foreach (var entry in Bound) config.Remove(entry.Definition);
            Bound.Clear();
            config.Save();
        }
    }
}
