using System;
using System.IO;
using System.Linq;
using Bindrune.Context;
using Bindrune.Hints;
using Bindrune.Personal;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What you set by hand lives in bindrune.keys, outside BepInEx/config where no sync reaches:
    /// kept whole across games, never written over when it cannot be read, and picked up when it
    /// is edited while the game runs. The situations you mark live in BepInEx/config/Bindrune, the
    /// same way. Hints you chose are shown until you take them off.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class PersonalStoreTests
    {
        private const string Key = "cfg:isimp.Example:Keys:Open\tK\tH\t1";

        [Fact]
        public void WhatYouSetIsThereInTheNextGame()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Keys, new[] { Key }));
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { "a|b\thard\t0" }));

            TestProfile.NewGame();

            Assert.Equal(new[] { Key }, PersonalStore.Lines(PersonalStore.Keys));
            Assert.Equal(new[] { "a|b\thard\t0" }, PersonalStore.Lines(PersonalStore.Muted));
        }

        [Fact]
        public void ChangingOneKindLeavesTheOthersAndOnesItDoesNotKnow()
        {
            using var profile = new TestProfile();
            File.WriteAllLines(profile.KeysFile, new[]
            {
                KeyLines.StateVersion, "[keys]", Key, "[later]", "something a newer version keeps",
            });

            Assert.True(PersonalStore.Replace(PersonalStore.Hints, new[] { "vanilla:Jump" }));

            var text = File.ReadAllText(profile.KeysFile);
            Assert.Contains(Key, text);
            Assert.Contains("[later]", text);
            Assert.Contains("something a newer version keeps", text);
            Assert.Equal(new[] { "vanilla:Jump" }, PersonalStore.Lines(PersonalStore.Hints));
        }

        [Fact]
        public void AFileFromBeforeSectionsReadsAsKeys()
        {
            using var profile = new TestProfile();
            File.WriteAllLines(profile.KeysFile, new[] { "# an older file", Key });

            Assert.Equal(new[] { Key }, PersonalStore.Lines(PersonalStore.Keys));
        }

        [Fact]
        public void WhatOlderVersionsKeptInConfigIsTakenInOnceAndTheirFilesGo()
        {
            using var profile = new TestProfile();
            var folder = Path.Combine(profile.ConfigDir, "Bindrune");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "muted.txt"), new[] { "a|b" });
            File.WriteAllLines(Path.Combine(folder, "hints.txt"), new[] { "vanilla:Jump" });

            Assert.Equal(new[] { "a|b" }, PersonalStore.Lines(PersonalStore.Muted));
            Assert.Equal(new[] { "vanilla:Jump" }, PersonalStore.Lines(PersonalStore.Hints));
            Assert.False(File.Exists(Path.Combine(folder, "muted.txt")));
            Assert.False(File.Exists(Path.Combine(folder, "hints.txt")));

            // A sync puts the owner's old file back later: it is not taken in again.
            File.WriteAllLines(Path.Combine(folder, "muted.txt"), new[] { "c|d" });
            TestProfile.NewGame();
            Assert.Equal(new[] { "a|b" }, PersonalStore.Lines(PersonalStore.Muted));
        }

        [Fact]
        public void AnOlderFileThatCannotBeReadIsLeftWhereItIs()
        {
            using var profile = new TestProfile();
            var folder = Path.Combine(profile.ConfigDir, "Bindrune");
            Directory.CreateDirectory(folder);
            var muted = Path.Combine(folder, "muted.txt");
            File.WriteAllLines(muted, new[] { "a|b" });

            using (TestProfile.Lock(muted))
                PersonalStore.Lines(PersonalStore.Keys);

            Assert.Equal(new[] { "a|b" }, File.ReadAllLines(muted));
        }

        [Fact]
        public void AnEditMadeWhileTheGameRunsIsReadBeforeTheNextChange()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Keys, new[] { Key }));

            var edited = File.ReadAllText(profile.KeysFile).Replace("[hints]", "[hints]\nvanilla:Jump");
            File.WriteAllText(profile.KeysFile, edited);
            File.SetLastWriteTimeUtc(profile.KeysFile, DateTime.UtcNow.AddMinutes(1));

            PersonalStore.Sync();
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { "a|b" }));

            Assert.Equal(new[] { "vanilla:Jump" }, PersonalStore.Lines(PersonalStore.Hints));
            Assert.Contains("vanilla:Jump", File.ReadAllText(profile.KeysFile));
        }

        [Fact]
        public void AKeysFileThatCannotBeReadIsNeverWrittenOver()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Keys, new[] { Key }));
            var before = File.ReadAllBytes(profile.KeysFile);
            TestProfile.NewGame();

            // Held open by a scanner as the game first looks, and let go a moment later.
            using (TestProfile.Lock(profile.KeysFile))
                PersonalStore.Lines(PersonalStore.Keys);
            Assert.Equal(before, File.ReadAllBytes(profile.KeysFile));

            // The next change adds to what the file holds, rather than writing the empty read over it.
            PersonalStore.Sync();
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { "a|b" }));
            Assert.Equal(new[] { Key }, PersonalStore.Lines(PersonalStore.Keys));
            Assert.Contains(Key, File.ReadAllText(profile.KeysFile));
        }

        [Fact]
        public void HintsAndMutesReadWhileTheFileWasHeldOpenAreNotWrittenOverItLater()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Hints, new[] { "vanilla:Jump" }));
            Assert.True(PersonalStore.Replace(PersonalStore.Muted, new[] { "a|b\thard\t0" }));
            TestProfile.NewGame();

            // The overlay and the clash list look while a scanner holds the file.
            using (TestProfile.Lock(profile.KeysFile))
            {
                Assert.False(HintChoice.Shows("vanilla:Jump"));
                Assert.False(Bindrune.Conflicts.MuteStore.IsMuted(new Bindrune.Conflicts.Conflict
                {
                    A = new BindEntry { Id = "a" }, B = new BindEntry { Id = "b" }, Severity = Bindrune.Conflicts.Severity.Hard,
                }));
            }

            // Let go, a hint is added: the one you had stays, and so does the mute.
            HintChoice.Toggle("vanilla:Crouch");

            TestProfile.NewGame();
            Assert.True(HintChoice.Shows("vanilla:Jump"));
            Assert.True(HintChoice.Shows("vanilla:Crouch"));
            Assert.Equal(new[] { "a|b\thard\t0" }, PersonalStore.Lines(PersonalStore.Muted));
        }

        [Fact]
        public void TheKeysFileIsWrittenWholeAndNothingIsLeftBeside()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Keys, new[] { Key }));

            Assert.Equal(KeyLines.StateVersion, File.ReadLines(profile.KeysFile).First());
            Assert.Equal(new[] { "bindrune.keys" }, Directory.GetFiles(profile.Root).Select(Path.GetFileName));
        }

        [Fact]
        public void YourKeysAreInNoPlaceASyncOrAnExportTakes()
        {
            using var profile = new TestProfile();
            Assert.True(PersonalStore.Replace(PersonalStore.Keys, new[] { Key }));

            Assert.Equal(profile.Root, Path.GetDirectoryName(profile.KeysFile));
            foreach (var ending in new[] { ".cfg", ".txt", ".json", ".yml", ".yaml", ".ini" })
                Assert.False(profile.KeysFile.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        // ---------- hints ----------

        [Fact]
        public void AHintYouChoseIsShownUntilYouTakeItOff()
        {
            using var profile = new TestProfile();

            HintChoice.Toggle("vanilla:Jump");
            TestProfile.NewGame();
            Assert.True(HintChoice.Shows("vanilla:Jump"));

            HintChoice.Toggle("vanilla:Jump");
            TestProfile.NewGame();
            Assert.False(HintChoice.Shows("vanilla:Jump"));
        }

        [Fact]
        public void HintsCanBeAddedAndTakenOffSeveralAtOnce()
        {
            using var profile = new TestProfile();

            HintChoice.Add(new[] { "a", "b", "c" });
            HintChoice.Remove(new[] { "a", "c" });

            Assert.Equal(1, HintChoice.Count);
            Assert.True(HintChoice.Shows("b"));
        }

        [Fact]
        public void TheOverlayHearsOfEveryChangeToTheHints()
        {
            using var profile = new TestProfile();
            var heard = 0;
            HintChoice.Changed = () => heard++;
            try
            {
                HintChoice.Toggle("a");
                HintChoice.Add(new[] { "b" });
                HintChoice.Add(new[] { "b" });
                HintChoice.Remove(new[] { "a" });
            }
            finally
            {
                HintChoice.Changed = null;
            }

            Assert.Equal(3, heard);
        }

        // ---------- situations ----------

        [Fact]
        public void ASituationYouMarkIsThereInTheNextGameAndMarkingItAgainTakesItOff()
        {
            using var profile = new TestProfile();

            SituationStore.Toggle("vanilla:Jump", "World");
            SituationStore.Toggle("vanilla:Jump", "Boat");
            TestProfile.NewGame();
            Assert.Equal(new[] { "Boat", "World" }, SituationStore.For("vanilla:Jump").OrderBy(s => s));

            SituationStore.Toggle("vanilla:Jump", "World");
            SituationStore.Toggle("vanilla:Jump", "Boat");
            TestProfile.NewGame();
            Assert.Empty(SituationStore.For("vanilla:Jump"));
            Assert.DoesNotContain("vanilla:Jump", File.ReadAllText(profile.SituationsFile));
        }

        [Fact]
        public void SituationsAreInTheSharedConfigFolderForTheProfileToHandOn()
        {
            using var profile = new TestProfile();
            SituationStore.Toggle("vanilla:Jump", "World");

            Assert.True(File.Exists(profile.SituationsFile));
        }

        [Fact]
        public void ASituationEditMadeWhileTheGameRunsIsKeptByTheNextChange()
        {
            using var profile = new TestProfile();
            SituationStore.Toggle("vanilla:Jump", "World");

            File.AppendAllLines(profile.SituationsFile, new[] { "vanilla:Crouch=Boat" });
            File.SetLastWriteTimeUtc(profile.SituationsFile, DateTime.UtcNow.AddMinutes(1));
            SituationStore.Toggle("vanilla:Use", "World");

            Assert.Contains("Boat", SituationStore.For("vanilla:Crouch"));
            Assert.Contains("vanilla:Crouch=Boat", File.ReadAllText(profile.SituationsFile));
        }

        [Fact]
        public void ASituationsFileThatCannotBeReadIsNeverWrittenOver()
        {
            using var profile = new TestProfile();
            SituationStore.Toggle("vanilla:Jump", "World");
            var before = File.ReadAllBytes(profile.SituationsFile);
            TestProfile.NewGame();

            // Held open as the panel first looks, and let go a moment later.
            using (TestProfile.Lock(profile.SituationsFile))
                SituationStore.For("vanilla:Jump");
            Assert.Equal(before, File.ReadAllBytes(profile.SituationsFile));

            SituationStore.Toggle("vanilla:Use", "Boat");

            Assert.Contains("World", SituationStore.For("vanilla:Jump"));
            Assert.Contains("vanilla:Jump=World", File.ReadAllText(profile.SituationsFile));
        }
    }
}
