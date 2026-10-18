using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.Personal;
using UnityEngine;

namespace Bindrune.Tests
{
    /// <summary>
    /// A BepInEx profile in a temporary folder, with Bindrune knowing nothing yet, as a new game
    /// starts it.
    /// </summary>
    public sealed class TestProfile : IDisposable
    {
        public readonly string Root;
        public string ConfigDir => Path.Combine(Root, "config");
        public string KeysFile => Path.Combine(Root, "bindrune.keys");
        public string SituationsFile => Path.Combine(ConfigDir, "Bindrune", "situations.txt");
        public string SharedListFile => Path.Combine(ConfigDir, "Bindrune", "known.txt");
        public string CacheDir => Path.Combine(Root, "cache");

        public TestProfile()
        {
            Root = Path.Combine(Path.GetTempPath(), "bindrune-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ConfigDir);
            SetPath(nameof(Paths.BepInExRootPath), Root);
            SetPath(nameof(Paths.ConfigPath), ConfigDir);
            SetPath(nameof(Paths.CachePath), CacheDir);
            NewGame();
        }

        /// <summary>The items a hand can hold, as the game's item list gave them when a world last loaded.</summary>
        public void HoldableItems(params string[] lines)
        {
            Directory.CreateDirectory(Path.Combine(CacheDir, "Bindrune"));
            File.WriteAllLines(Path.Combine(CacheDir, "Bindrune", "items.txt"), lines);
            EquippedItems.Reset();
        }

        /// <summary>A mod's keybind setting, bound the way a plugin binds it, on the key the profile gave it, and the bind the panel lists for it.</summary>
        internal (BindEntry Bind, ConfigEntry<KeyboardShortcut> Entry) ModBind(KeyCode profiles)
        {
            var config = new ConfigFile(Path.Combine(ConfigDir, "isimp.Example.cfg"), true);
            var entry = config.Bind("Keys", "Open", new KeyboardShortcut(KeyCode.H), "Opens it.");
            entry.Value = new KeyboardShortcut(profiles);

            var bind = new BindEntry
            {
                Id = BindIds.Config("isimp.Example", "Keys", "Open"),
                OwnerName = "Example",
                Label = "Open",
                Source = BindSource.ModTyped,
                Modifiers = ModifierBehavior.Strict,
                Combo = new KeyCombo(profiles, null),
                Editable = true,
                Handle = entry,
            };
            return (bind, entry);
        }

        /// <summary>What a scan found, as BindRegistry.Refresh leaves it.</summary>
        internal static void Scanned(params BindEntry[] binds) =>
            typeof(BindRegistry).GetProperty("All")!.GetSetMethod(true)!.Invoke(null, new object[] { binds.ToList() });

        /// <summary>BepInEx sets its paths once as it starts, with no public way in.</summary>
        private static void SetPath(string name, string value) =>
            typeof(Paths).GetProperty(name)!.GetSetMethod(true)!.Invoke(null, new object[] { value });

        /// <summary>Everything read from disk is forgotten, as when the game starts again.</summary>
        public static void NewGame()
        {
            PersonalStore.Reset();
            SituationStore.Reset();
            KnownSituations.Reset();
            EquippedItems.Reset();
        }

        /// <summary>Holds a file open so nothing else can read or write it, as a scanner or an editor may, until disposed.</summary>
        public static IDisposable Lock(string path) => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        public void Dispose()
        {
            NewGame();
            try
            {
                Directory.Delete(Root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
