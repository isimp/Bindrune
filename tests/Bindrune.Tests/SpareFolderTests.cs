using System;
using System.IO;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// Which profiles get a spare copy and where it goes: a profile of Thunderstore Mod Manager or
    /// r2modman, told by the mods.yml in its folder inside a folder named profiles, gets one
    /// beside profiles under the mod's name and the profile's; any other profile gets none. Your
    /// answer on keeping one is read as this version wrote it, and turning it off removes the
    /// copy and, once empty, the folder that held it.
    /// </summary>
    public sealed class SpareFolderTests : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "spare-folder-tests-" + Guid.NewGuid().ToString("N"));

        /// <summary>A profile folder with its BepInEx folder, returning the BepInEx folder.</summary>
        private string Profile(string parents, string name, bool modsYml)
        {
            var profile = Path.Combine(_root, parents, name);
            Directory.CreateDirectory(Path.Combine(profile, "BepInEx"));
            if (modsYml) File.WriteAllText(Path.Combine(profile, "mods.yml"), "");
            return Path.Combine(profile, "BepInEx");
        }

        [Fact]
        public void AManagersProfileKeepsItsSpareCopyBesideProfilesUnderTheModsName()
        {
            var bepInEx = Profile(Path.Combine("Valheim", "profiles"), "Friends", modsYml: true);
            Assert.Equal(Path.Combine(_root, "Valheim", "Bindrune", "Friends"), SpareFolder.For(bepInEx));
        }

        [Fact]
        public void TheProfilesFolderIsKnownWhateverItsCase()
        {
            var bepInEx = Profile(Path.Combine("Valheim", "Profiles"), "Friends", modsYml: true);
            Assert.Equal(Path.Combine(_root, "Valheim", "Bindrune", "Friends"), SpareFolder.For(bepInEx));
        }

        [Fact]
        public void AProfileWithoutModsYmlGetsNoSpareCopy()
        {
            Assert.Null(SpareFolder.For(Profile(Path.Combine("Valheim", "profiles"), "Friends", modsYml: false)));
        }

        [Fact]
        public void AProfileOutsideAFolderNamedProfilesGetsNoSpareCopy()
        {
            Assert.Null(SpareFolder.For(Profile(Path.Combine("Valheim", "elsewhere"), "Friends", modsYml: true)));
        }

        [Theory]
        [InlineData("copy\tyes", true)]
        [InlineData("copy\tno", false)]
        [InlineData(" copy \t yes ", true)]
        public void YourAnswerIsReadAsWritten(string line, bool wanted)
        {
            Assert.Equal(wanted, SpareFolder.WantedIn(new[] { SpareFolder.AnswerVersion, "# what this file is for", line }));
        }

        [Fact]
        public void NoAnswerIsReadWhereThereIsNone()
        {
            Assert.Null(SpareFolder.WantedIn(new string[0]));
            Assert.Null(SpareFolder.WantedIn(new[] { SpareFolder.AnswerVersion }));
            Assert.Null(SpareFolder.WantedIn(new[] { SpareFolder.AnswerVersion, "copy\tmaybe" }));
            Assert.Null(SpareFolder.WantedIn(new[] { SpareFolder.AnswerVersion, "# copy\tyes" }));
        }

        [Fact]
        public void AnAnswerAnotherVersionWroteIsNotRead()
        {
            Assert.Null(SpareFolder.WantedIn(new[] { "# some other spare v2", "copy\tyes" }));
            Assert.Null(SpareFolder.WantedIn(new[] { "copy\tyes" }));
        }

        [Fact]
        public void TurningItOffRemovesTheCopyAndTheEmptyFolderThatHeldIt()
        {
            var spare = Path.Combine(_root, "Valheim", "Bindrune", "Friends");
            Directory.CreateDirectory(spare);
            File.WriteAllText(Path.Combine(spare, "kept.txt"), "x");

            SpareFolder.Remove(spare);

            Assert.False(Directory.Exists(spare));
            Assert.False(Directory.Exists(Path.GetDirectoryName(spare)));
        }

        [Fact]
        public void TurningItOffLeavesOtherProfilesCopies()
        {
            var spare = Path.Combine(_root, "Valheim", "Bindrune", "Friends");
            var other = Path.Combine(_root, "Valheim", "Bindrune", "Solo");
            Directory.CreateDirectory(spare);
            Directory.CreateDirectory(other);

            SpareFolder.Remove(spare);

            Assert.False(Directory.Exists(spare));
            Assert.True(Directory.Exists(other));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
