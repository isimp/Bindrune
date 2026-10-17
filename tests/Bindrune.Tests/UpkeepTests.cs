using System;
using System.IO;
using BepInEx.Configuration;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// A setting an earlier version of Bindrune had and this one dropped would otherwise live on
    /// in isimp.Bindrune.cfg for good, since BepInEx writes back what it does not recognise, and
    /// ship to everyone a profile syncs to. At each start, once every setting of this version is
    /// bound, Bindrune takes those out of its own file. Nothing it still has is touched, no other
    /// mod's file is, the file is not written when there is nothing to take out, and a file that
    /// cannot be written never stops the start.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class UpkeepTests
    {
        private const string Earlier =
            "## Settings file was created by plugin Bindrune v0.3.0\n" +
            "## Plugin GUID: isimp.Bindrune\n\n" +
            "[General]\n\n" +
            "## Opens the Bindrune panel.\n" +
            "# Setting type: KeyboardShortcut\n" +
            "# Default value: Insert\n" +
            "OpenKey = F7\n\n" +
            "## A setting this version no longer has.\n" +
            "# Setting type: Boolean\n" +
            "# Default value: true\n" +
            "ShowTips = false\n\n" +
            "[Retired]\n\n" +
            "# Setting type: Int32\n" +
            "# Default value: 3\n" +
            "Rows = 7\n";

        /// <summary>Bindrune's own settings file as an earlier version left it, read as this version starts.</summary>
        private static (ConfigFile Config, string Path) Start(TestProfile profile, string text)
        {
            var path = Path.Combine(profile.ConfigDir, "isimp.Bindrune.cfg");
            File.WriteAllText(path, text);
            var config = new ConfigFile(path, false);
            config.Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.Insert), "Opens the Bindrune panel.");
            return (config, path);
        }

        [Fact]
        public void SettingsAnEarlierVersionHadAreGoneFromTheFileAfterTheNextStart()
        {
            using var profile = new TestProfile();
            var (config, path) = Start(profile, Earlier);

            Upkeep.Run(config);

            var text = File.ReadAllText(path);
            Assert.DoesNotContain("ShowTips", text);
            Assert.DoesNotContain("Rows", text);
            Assert.DoesNotContain("[Retired]", text);
        }

        [Fact]
        public void EverySettingThisVersionHasKeepsYourValue()
        {
            using var profile = new TestProfile();
            var (config, path) = Start(profile, Earlier);

            Upkeep.Run(config);

            Assert.Contains("OpenKey = F7", File.ReadAllText(path));
            var reread = new ConfigFile(path, false).Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.Insert), "Opens the Bindrune panel.");
            Assert.Equal(KeyCode.F7, reread.Value.MainKey);
        }

        [Fact]
        public void WithNothingLeftOverTheFileIsNotWritten()
        {
            using var profile = new TestProfile();
            var (config, path) = Start(profile, "[General]\n\nOpenKey = F7\n");
            var stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, stamp);

            Upkeep.Run(config);

            Assert.Equal(stamp, File.GetLastWriteTimeUtc(path));
        }

        [Fact]
        public void AnotherModsLeftoversAreLeftAlone()
        {
            using var profile = new TestProfile();
            var (config, _) = Start(profile, Earlier);
            var other = Path.Combine(profile.ConfigDir, "test.mod.cfg");
            File.WriteAllText(other, "[Old]\n\nGone = 1\n");
            var otherConfig = new ConfigFile(other, false);

            Upkeep.Run(config);

            Assert.Contains("Gone = 1", File.ReadAllText(other));
            Assert.NotNull(otherConfig);
        }

        [Fact]
        public void AFileThatCannotBeWrittenNeverStopsTheStart()
        {
            using var profile = new TestProfile();
            var (config, path) = Start(profile, Earlier);

            using (TestProfile.Lock(path))
            {
                var failure = Record.Exception(() => Upkeep.Run(config));
                Assert.Null(failure);
            }

            Assert.Contains("ShowTips = false", File.ReadAllText(path));
        }
    }
}
