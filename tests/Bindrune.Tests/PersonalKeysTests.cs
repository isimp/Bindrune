using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Personal;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// A bind has two keys: yours and the profile's. Setting your own remembers the key it
    /// replaced as the profile's, so Whole profile gives the bind back to it and Mine returns to
    /// yours, the way the panel does it.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class PersonalKeysTests
    {
        /// <summary>A mod's keybind setting, bound the way a plugin binds it, on the key the profile gave it.</summary>
        private static (BindEntry Bind, ConfigEntry<KeyboardShortcut> Entry) ModBind(TestProfile profile, KeyCode profiles)
        {
            var config = new ConfigFile(Path.Combine(profile.ConfigDir, "isimp.Example.cfg"), true);
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

        [Fact]
        public void YourFirstKeyRemembersTheOneItReplacedAsTheProfiles()
        {
            using var profile = new TestProfile();
            var (bind, _) = ModBind(profile, KeyCode.L);

            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, null), SaveTarget.Personal));

            var entry = PersonalKeys.Get(bind.Id);
            Assert.Equal(new KeyCombo(KeyCode.Q, null), entry.Personal);
            Assert.Equal(new KeyCombo(KeyCode.L, null), entry.Profile);
        }

        [Fact]
        public void WholeProfileGivesTheBindBackToTheProfilesKeyAndMineReturnsToYours()
        {
            using var profile = new TestProfile();
            var (bind, setting) = ModBind(profile, KeyCode.L);
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, null), SaveTarget.Personal));

            PersonalKeys.UseProfile(bind);
            Assert.Equal(KeyCode.L, setting.Value.MainKey);

            PersonalKeys.UsePersonal(bind);
            Assert.Equal(KeyCode.Q, setting.Value.MainKey);
        }

        [Fact]
        public void WholeProfileOnABindTheProfileLeftWithoutAKeyLeavesItWithoutOne()
        {
            using var profile = new TestProfile();
            var (bind, setting) = ModBind(profile, KeyCode.None);
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.W, null), SaveTarget.Personal));

            PersonalKeys.UseProfile(bind);
            Assert.Equal(KeyCode.None, setting.Value.MainKey);

            PersonalKeys.UsePersonal(bind);
            Assert.Equal(KeyCode.W, setting.Value.MainKey);

            // Going back and forth never takes your key for the profile's.
            var entry = PersonalKeys.Get(bind.Id);
            Assert.Equal(new KeyCombo(KeyCode.W, null), entry.Personal);
            Assert.False(entry.Profile.IsBound);
        }

        [Fact]
        public void GoingBackAndForthKeepsBothKeysAsTheyWere()
        {
            using var profile = new TestProfile();
            var (bind, setting) = ModBind(profile, KeyCode.L);
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, null), SaveTarget.Personal));

            for (var i = 0; i < 3; i++)
            {
                PersonalKeys.UseProfile(bind);
                PersonalKeys.UsePersonal(bind);
            }

            var entry = PersonalKeys.Get(bind.Id);
            Assert.Equal(KeyCode.Q, setting.Value.MainKey);
            Assert.Equal(new KeyCombo(KeyCode.Q, null), entry.Personal);
            Assert.Equal(new KeyCombo(KeyCode.L, null), entry.Profile);
        }

        [Fact]
        public void ChangingYourKeyAgainKeepsTheProfilesKeyItHadRemembered()
        {
            using var profile = new TestProfile();
            var (bind, _) = ModBind(profile, KeyCode.L);

            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, null), SaveTarget.Personal));
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.W, null), SaveTarget.Personal));

            var entry = PersonalKeys.Get(bind.Id);
            Assert.Equal(new KeyCombo(KeyCode.W, null), entry.Personal);
            Assert.Equal(new KeyCombo(KeyCode.L, null), entry.Profile);
        }

        /// <summary>What a scan found, as BindRegistry.Refresh leaves it.</summary>
        private static void Scanned(params BindEntry[] binds) =>
            typeof(Bindrune.Discovery.BindRegistry).GetProperty("All")!.GetSetMethod(true)!
                .Invoke(null, new object[] { binds.ToList() });

        [Fact]
        public void AfterASyncYourKeyGoesBackAndWhatTheProfileSaysIsRememberedEvenNoKey()
        {
            using var profile = new TestProfile();
            var (bind, setting) = ModBind(profile, KeyCode.None);
            File.WriteAllLines(profile.KeysFile, new[]
            {
                KeyLines.StateVersion, "[keys]", $"{bind.Id}\tW\tW\t1",
            });
            bind.Combo = KeyCombo.None;

            try
            {
                Scanned(bind);
                PersonalKeys.Reconcile();

                Assert.Equal(KeyCode.W, setting.Value.MainKey);
                Assert.False(PersonalKeys.Get(bind.Id).Profile.IsBound);

                // A later scan, as when a world loads, leaves both as they are.
                PersonalKeys.Reconcile();
                TestProfile.NewGame();
                Assert.Contains($"{bind.Id}\tW\tnone\t1", File.ReadAllLines(profile.KeysFile));
            }
            finally
            {
                Scanned();
            }
        }

        [Fact]
        public void BothKeysAreThereInTheNextGame()
        {
            using var profile = new TestProfile();
            var (bind, _) = ModBind(profile, KeyCode.L);
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, new[] { KeyCode.LeftAlt }), SaveTarget.Personal));

            TestProfile.NewGame();

            var entry = PersonalKeys.Get(bind.Id);
            Assert.True(entry.Active);
            Assert.Equal(new KeyCombo(KeyCode.Q, new[] { KeyCode.LeftAlt }), entry.Personal);
            Assert.Equal(new KeyCombo(KeyCode.L, null), entry.Profile);
        }
    }
}
