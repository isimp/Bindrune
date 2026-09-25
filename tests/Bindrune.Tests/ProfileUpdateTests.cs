using System;
using System.IO;
using System.Linq;
using BepInEx;
using Bindrune.Personal;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// Your own keys survive Update existing profile in Thunderstore Mod Manager and r2modman,
    /// which deletes the profile folder and puts the updated one in its place under the same name,
    /// once you agree to a spare copy outside the profile. Nothing is written outside a profile
    /// before that, nor outside one that does not need it.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class ProfileUpdateTests : IDisposable
    {
        private const string Key = "cfg:isimp.Example:Keys:Open\tK\tH\t1";
        private const string Other = "cfg:isimp.Example:Keys:Close\tJ\tG\t1";

        /// <summary>The mod manager's folder for the game, holding profiles/.</summary>
        private readonly string _game = Path.Combine(Path.GetTempPath(), "bindrune-update-" + Guid.NewGuid().ToString("N"));

        private string ProfileFolder(string name) => Path.Combine(_game, "profiles", name);

        private string KeysFile => Path.Combine(Paths.BepInExRootPath, "bindrune.keys");

        public void Dispose()
        {
            PersonalStore.Reset();
            try
            {
                Directory.Delete(_game, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>A profile laid out as Thunderstore Mod Manager and r2modman lay one out, or without mods.yml as another manager might.</summary>
        private void Open(string name = "Pack", bool modsYml = true)
        {
            var bepInEx = Path.Combine(ProfileFolder(name), "BepInEx");
            Directory.CreateDirectory(Path.Combine(bepInEx, "config"));
            if (modsYml) File.WriteAllText(Path.Combine(ProfileFolder(name), "mods.yml"), "[]");

            SetPath(nameof(Paths.BepInExRootPath), bepInEx);
            SetPath(nameof(Paths.ConfigPath), Path.Combine(bepInEx, "config"));
            PersonalStore.Reset();
        }

        private static void SetPath(string name, string value) =>
            typeof(Paths).GetProperty(name)!.GetSetMethod(true)!.Invoke(null, new object[] { value });

        /// <summary>A new game, as the plugin starts it.</summary>
        private static bool Launch()
        {
            PersonalStore.Reset();
            return SpareCopy.AtLaunch();
        }

        private static void SetKeys(params string[] lines) => Assert.True(PersonalStore.Replace(PersonalStore.Keys, lines));

        private static string[] Keys() => PersonalStore.Lines(PersonalStore.Keys).ToArray();

        /// <summary>A player's profile after a game: one key of their own, and asked, they want a spare copy unless told otherwise.</summary>
        private void Played(string name = "Pack", bool modsYml = true, bool? spare = true)
        {
            Open(name, modsYml);
            Launch();
            SetKeys(Key);
            if (spare != null) Assert.Null(SpareCopy.Answer(spare.Value));
        }

        /// <summary>Update existing profile: the folder is deleted, and the updated pack takes its name.</summary>
        private void UpdateExistingProfile(string name = "Pack")
        {
            Directory.Delete(ProfileFolder(name), true);
            Open(name);
        }

        private bool AnythingOutsideTheProfiles() =>
            Directory.GetFileSystemEntries(_game).Any(e => !string.Equals(Path.GetFileName(e), "profiles", StringComparison.OrdinalIgnoreCase));

        [Fact]
        public void YourKeysAreBackAtTheFirstLaunchAfterAnUpdate()
        {
            Played();
            UpdateExistingProfile();

            Assert.True(Launch());

            Assert.Equal(new[] { Key }, Keys());
            Assert.True(SpareCopy.RestoredThisLaunch);
            Assert.False(Launch());
            Assert.False(SpareCopy.RestoredThisLaunch);
        }

        [Fact]
        public void AKeySetInASessionThatCrashedSurvivesAnUpdate()
        {
            Played();
            Launch();
            SetKeys(Key, Other);

            // No close: the game crashed.
            UpdateExistingProfile();
            Launch();

            Assert.Equal(new[] { Key, Other }, Keys());
        }

        [Fact]
        public void MutedClashesAndHintsComeBackToo()
        {
            Played();
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { "a\tb" }));
            Assert.True(PersonalStore.Replace(PersonalStore.Hints, new[] { "cfg:isimp.Example:Keys:Open" }));

            UpdateExistingProfile();
            Launch();

            Assert.Equal(new[] { "a\tb" }, PersonalStore.Lines(PersonalStore.Muted));
            Assert.Equal(new[] { "cfg:isimp.Example:Keys:Open" }, PersonalStore.Lines(PersonalStore.Hints));
        }

        [Fact]
        public void AProfileWithItsKeysFileIsNeverOverwrittenFromTheSpareCopy()
        {
            Played();
            SetKeys(Other);
            var mine = File.ReadAllText(KeysFile);
            PersonalStore.Reset();

            // The spare copy holds the same now, so make it older than the profile's by hand.
            var spare = Path.Combine(SpareCopy.Folder!, "bindrune.keys");
            File.WriteAllText(spare, File.ReadAllText(spare).Replace("Close", "Open"));

            Assert.False(Launch());
            Assert.Equal(mine, File.ReadAllText(KeysFile));
        }

        [Fact]
        public void AProfileThatLostItsKeysFileNeverWritesOverTheSpareCopy()
        {
            Played();
            var spare = Path.Combine(SpareCopy.Folder!, "bindrune.keys");
            var spared = File.ReadAllText(spare);

            File.Delete(KeysFile);
            SpareCopy.Follow();
            Launch();

            Assert.Equal(spared, File.ReadAllText(spare));
        }

        // ---------- asked first ----------

        [Fact]
        public void NothingIsWrittenOutsideTheProfileUntilYouSayYes()
        {
            Played(spare: null);
            Launch();
            SetKeys(Key, Other);

            Assert.True(SpareCopy.Asks);
            Assert.False(AnythingOutsideTheProfiles());
        }

        [Fact]
        public void SayingYesMakesTheSpareCopyAtOnce()
        {
            Played(spare: null);

            Assert.Null(SpareCopy.Answer(true));

            Assert.False(SpareCopy.Asks);
            Assert.True(AnythingOutsideTheProfiles());
        }

        [Fact]
        public void SayingNoWritesNothingOutsideAndIsNotAskedAgain()
        {
            Played(spare: false);
            Launch();
            SetKeys(Key, Other);

            Assert.False(SpareCopy.Asks);
            Assert.False(AnythingOutsideTheProfiles());
        }

        [Fact]
        public void TurningItOffLaterRemovesTheSpareCopy()
        {
            Played();
            Assert.True(AnythingOutsideTheProfiles());

            Assert.Null(SpareCopy.Answer(false));
            Launch();
            SetKeys(Other);

            Assert.False(AnythingOutsideTheProfiles());
        }

        [Fact]
        public void TheQuestionIsOnlyForAManagerProfileThatKeepsSomething()
        {
            Open("Empty");
            Launch();
            Assert.False(SpareCopy.Asks);

            Played("Gale", modsYml: false, spare: null);
            Assert.False(SpareCopy.Asks);

            Open("HintsOnly");
            Launch();
            Assert.True(PersonalStore.Replace(PersonalStore.Hints, new[] { "cfg:isimp.Example:Keys:Open" }));
            Assert.True(SpareCopy.Asks);
        }

        [Fact]
        public void AProfileWithoutModsYmlGetsNothingWrittenOutsideIt()
        {
            Played(modsYml: false);

            Assert.Null(SpareCopy.Folder);
            Assert.False(AnythingOutsideTheProfiles());
        }

        [Fact]
        public void YourAnswerComesBackWithTheSpareCopyAfterAnUpdate()
        {
            Played();
            UpdateExistingProfile();

            Launch();

            Assert.False(SpareCopy.Asks);
            Assert.Equal(true, SpareCopy.Wanted);
        }

        [Fact]
        public void TheAnswerIsInNoFileASyncOrAnExportTakes()
        {
            Played();

            Assert.Equal(Paths.BepInExRootPath, Path.GetDirectoryName(SpareCopy.AnswerPath));
            foreach (var ending in new[] { ".cfg", ".txt", ".json", ".yml", ".yaml", ".ini" })
                Assert.False(SpareCopy.AnswerPath.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void BindruneAndKeepsakeKeepTheirSpareCopiesApart()
        {
            Played();

            Assert.Equal(Path.Combine(_game, "Bindrune", "Pack"), SpareCopy.Folder);
        }
    }

    /// <summary>The tests that share BepInEx's paths and Bindrune's stores run one after another.</summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class ProfileCollection
    {
        public const string Name = "profile";
    }
}
