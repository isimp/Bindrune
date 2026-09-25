using System;
using System.IO;
using BepInEx;
using Bindrune.Context;
using Bindrune.Personal;

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

        public TestProfile()
        {
            Root = Path.Combine(Path.GetTempPath(), "bindrune-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ConfigDir);
            SetPath(nameof(Paths.BepInExRootPath), Root);
            SetPath(nameof(Paths.ConfigPath), ConfigDir);
            NewGame();
        }

        /// <summary>BepInEx sets its paths once as it starts, with no public way in.</summary>
        private static void SetPath(string name, string value) =>
            typeof(Paths).GetProperty(name)!.GetSetMethod(true)!.Invoke(null, new object[] { value });

        /// <summary>Everything read from disk is forgotten, as when the game starts again.</summary>
        public static void NewGame()
        {
            PersonalStore.Reset();
            SituationStore.Reset();
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
