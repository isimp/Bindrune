using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Conflicts;
using Bindrune.Context;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// How the panel tells two binds on one key apart (docs/technical.md, Clash tiers and Where a
    /// bind applies). A hard clash is one key and the same modifiers; a soft one is a bind that
    /// fires under the other's combo too; a note is worth knowing and should not interfere. A
    /// pair is ruled out only when one axis, where it is live or what is in hand, rules it out
    /// with both sides speaking on that axis, and confirmed only where both really meet. Every
    /// clash is read the way the panel reads it, from the index it rebuilds on each scan.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class ClashTests
    {
        private static BindEntry Shortcut(string key, KeyCode main, params KeyCode[] modifiers) => new BindEntry
        {
            Id = BindIds.Config("test.mod", "Keys", key), OwnerName = "Test", OwnerGuid = "test.mod", Label = key, Section = "Keys",
            Source = BindSource.ModTyped, Modifiers = ModifierBehavior.Strict, Combo = new KeyCombo(main, modifiers), Editable = true,
        };

        private static BindEntry SingleKey(string key, KeyCode main) => new BindEntry
        {
            Id = BindIds.Config("test.mod", "Keys", key), OwnerName = "Test", OwnerGuid = "test.mod", Label = key, Section = "Keys",
            Source = BindSource.ModTyped, Modifiers = ModifierBehavior.SingleKey, Combo = new KeyCombo(main, null), Editable = true,
        };

        private static BindEntry Text(string key, KeyCode main) => new BindEntry
        {
            Id = BindIds.Config("test.mod", "Keys", key), OwnerName = "Test", OwnerGuid = "test.mod", Label = key, Section = "Keys",
            Source = BindSource.ModText, Modifiers = ModifierBehavior.Unknown, Combo = new KeyCombo(main, null), Editable = false,
        };

        /// <summary>A key the game reads in its own code, as the scan lists it.</summary>
        private static BindEntry GameRead(string name, KeyCode main, KeyCode? modifier = null) => new BindEntry
        {
            Id = BindIds.Game(name), OwnerName = "Valheim", OwnerGuid = "vanilla", Label = name, Section = "Game",
            Source = BindSource.Vanilla, Modifiers = modifier.HasValue ? ModifierBehavior.Required : ModifierBehavior.SingleKey,
            Combo = new KeyCombo(main, modifier.HasValue ? new[] { modifier.Value } : null), Editable = false,
        };

        /// <summary>The clash the panel lists between two binds after a scan that found them both, or null.</summary>
        private static Conflict Between(BindEntry a, BindEntry b, params BindEntry[] others)
        {
            ConflictIndex.Rebuild(new[] { a, b }.Concat(others));
            return ConflictIndex.For(a.Id).SingleOrDefault(c => c.A.Id == b.Id || c.B.Id == b.Id);
        }

        /// <summary>Situations as you set them in the panel, one tag at a time.</summary>
        private static void YouSay(BindEntry bind, params string[] tags)
        {
            foreach (var tag in tags) Assert.Null(SituationStore.Toggle(bind.Id, tag));
        }

        private static string Holding(string held) => EquippedItems.Prefix + held;

        // ---------- the tiers ----------

        [Fact]
        public void TheSameKeyAndTheSameModifiersIsAHardClash()
        {
            using var profile = new TestProfile();

            var clash = Between(Shortcut("A", KeyCode.K, KeyCode.LeftAlt), Shortcut("B", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal(Severity.Hard, clash?.Severity);
        }

        [Fact]
        public void ASingleKeyWithNoModifierFieldFiresUnderAComboOnItsKeySoItIsSoft()
        {
            using var profile = new TestProfile();

            var clash = Between(SingleKey("Plain", KeyCode.K), Shortcut("WithAlt", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal(Severity.Soft, clash?.Severity);
        }

        [Fact]
        public void TheSameKeyWithModifiersBothCheckExactlyIsOnlyANote()
        {
            using var profile = new TestProfile();

            var clash = Between(Shortcut("WithAlt", KeyCode.K, KeyCode.LeftAlt), Shortcut("WithCtrl", KeyCode.K, KeyCode.LeftControl));

            Assert.Equal(Severity.Note, clash?.Severity);
        }

        [Fact]
        public void APlainShortcutAndOneWithAModifierCheckedExactlyAreANote()
        {
            using var profile = new TestProfile();

            var clash = Between(Shortcut("Plain", KeyCode.K), Shortcut("WithAlt", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal(Severity.Note, clash?.Severity);
        }

        [Fact]
        public void AltCtrlAndShiftOnTheirOwnAreNeverAClash()
        {
            using var profile = new TestProfile();

            foreach (var modifier in new[] { KeyCode.LeftAlt, KeyCode.RightControl, KeyCode.LeftShift })
                Assert.Null(Between(SingleKey("One", modifier), SingleKey("Two", modifier)));
        }

        [Fact]
        public void BindsOnDifferentKeysDoNotClash()
        {
            using var profile = new TestProfile();

            Assert.Null(Between(Shortcut("A", KeyCode.K, KeyCode.LeftAlt), Shortcut("B", KeyCode.L, KeyCode.LeftAlt)));
        }

        [Fact]
        public void AHotkeyKeptAsTextMayIgnoreModifiersSoItFiresUnderAComboOnItsKey()
        {
            using var profile = new TestProfile();

            var clash = Between(Text("Typed", KeyCode.K), Shortcut("WithAlt", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal(Severity.Soft, clash?.Severity);
            Assert.Contains("stored as text", clash.Reason);
        }

        [Fact]
        public void AKeyTheGameReadsInItsOwnCodeClashesWithAComboOnItsKey()
        {
            using var profile = new TestProfile();

            var clash = Between(GameRead("Screenshot", KeyCode.F11), Shortcut("Snap", KeyCode.F11, KeyCode.LeftAlt));

            Assert.Equal(Severity.Soft, clash?.Severity);
        }

        [Fact]
        public void CtrlF1FiresUnderAnyComboHoldingCtrlButNotUnderF1Alone()
        {
            using var profile = new TestProfile();
            var capture = GameRead("Mouse capture", KeyCode.F1, KeyCode.LeftControl);

            Assert.Equal(Severity.Soft, Between(capture, Shortcut("Both", KeyCode.F1, KeyCode.LeftControl, KeyCode.LeftAlt))?.Severity);
            Assert.Equal(Severity.Note, Between(capture, Shortcut("Bare", KeyCode.F1))?.Severity);
        }

        // ---------- where a bind applies ----------

        [Fact]
        public void TwoBindsLiveInPlacesThatNeverMeetAreRuledOut()
        {
            using var profile = new TestProfile();
            var map = Shortcut("OnMap", KeyCode.K, KeyCode.LeftAlt);
            var inventory = Shortcut("InInventory", KeyCode.K, KeyCode.LeftAlt);
            YouSay(map, "Map");
            YouSay(inventory, "Inventory");

            var clash = Between(map, inventory);

            Assert.Equal(Severity.Note, clash?.Severity);
            Assert.Contains("never live together", clash.Reason);
        }

        [Fact]
        public void TwoBindsLiveInTheSamePlaceAreAConfirmedClashNamedForWhereTheyMeet()
        {
            using var profile = new TestProfile();
            var a = Shortcut("A", KeyCode.K, KeyCode.LeftAlt);
            var b = Shortcut("B", KeyCode.K, KeyCode.LeftAlt);
            YouSay(a, "Map", "World");
            YouSay(b, "Map");

            var clash = Between(a, b);

            Assert.Equal(Severity.Hard, clash?.Severity);
            Assert.True(clash.Confirmed);
            Assert.Contains("Both apply to Map", clash.Reason);
        }

        [Fact]
        public void ABindThatSaysWhereAndOneThatSaysNothingAreNotConfirmed()
        {
            using var profile = new TestProfile();
            var said = Shortcut("Said", KeyCode.K, KeyCode.LeftAlt);
            YouSay(said, "Map");

            var clash = Between(said, Shortcut("Silent", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal(Severity.Hard, clash?.Severity);
            Assert.False(clash.Confirmed);
            Assert.Contains("setting that one's situations would settle this", clash.Reason);
        }

        [Fact]
        public void APlaceAndAnItemInHandRuleNothingOut()
        {
            using var profile = new TestProfile();
            profile.HoldableItems("PickaxeIron|Iron pickaxe|Tool|Pickaxes|1");
            var place = Shortcut("Place", KeyCode.K, KeyCode.LeftAlt);
            var tool = Shortcut("Tool", KeyCode.K, KeyCode.LeftAlt);
            YouSay(place, "Map");
            YouSay(tool, Holding("skill:Pickaxes"));

            var clash = Between(place, tool);

            Assert.Equal(Severity.Hard, clash?.Severity);
            Assert.False(clash.Confirmed);
            Assert.Contains("needs something in hand", clash.Reason);
        }

        [Fact]
        public void TwoBindsForItemsNeverHeldTogetherAreRuledOut()
        {
            using var profile = new TestProfile();
            profile.HoldableItems("PickaxeIron|Iron pickaxe|Tool|Pickaxes|1", "AxeIron|Iron axe|OneHandedWeapon|Axes|1");
            var dig = Shortcut("Dig", KeyCode.K, KeyCode.LeftAlt);
            var chop = Shortcut("Chop", KeyCode.K, KeyCode.LeftAlt);
            YouSay(dig, Holding("skill:Pickaxes"));
            YouSay(chop, Holding("skill:Axes"));

            Assert.Equal(Severity.Note, Between(dig, chop)?.Severity);
        }

        [Fact]
        public void AnyPickaxeAndTheIronPickaxeMeetWhenItIsInHand()
        {
            using var profile = new TestProfile();
            profile.HoldableItems("PickaxeIron|Iron pickaxe|Tool|Pickaxes|1", "PickaxeBronze|Bronze pickaxe|Tool|Pickaxes|1");
            var any = Shortcut("Any", KeyCode.K, KeyCode.LeftAlt);
            var iron = Shortcut("Iron", KeyCode.K, KeyCode.LeftAlt);
            YouSay(any, Holding("skill:Pickaxes"));
            YouSay(iron, Holding("item:PickaxeIron"));

            var clash = Between(any, iron);

            Assert.Equal(Severity.Hard, clash?.Severity);
            Assert.True(clash.Confirmed);
            Assert.Contains("Iron pickaxe", clash.Reason);
        }

        [Fact]
        public void APlaceRulesOutOnlyWhereBothSpeak()
        {
            using var profile = new TestProfile();
            var map = Shortcut("Map", KeyCode.K, KeyCode.LeftAlt);
            YouSay(map, "Map");

            // The other names nothing of where, so nothing is ruled out, however far "Map" is from where it is used.
            Assert.Equal(Severity.Hard, Between(map, Shortcut("Anywhere", KeyCode.K, KeyCode.LeftAlt))?.Severity);
        }

        // ---------- whose word counts ----------

        [Fact]
        public void AModThatSaysWhenItsBindIsLiveIsFinalOverWhatYouSet()
        {
            using var profile = new TestProfile();
            var config = new ConfigFile(Path.Combine(profile.ConfigDir, "test.mod.cfg"), false);
            var entry = config.Bind("Keys", "Dig", KeyCode.K, new ConfigDescription("Digs.", null, new BindruneAttributes { Situations = new[] { "Map" } }));
            var dig = SingleKey("Dig", KeyCode.K);
            KnownSituations.ReadFrom(dig.Id, entry);
            YouSay(dig, "Inventory");

            var situations = KnownSituations.For(dig, out var source);

            Assert.Equal(SituationSource.Mod, source);
            Assert.Equal(new[] { "Map" }, situations.ToArray());
        }

        [Fact]
        public void AModTellsWhatHasToBeInHandAsWell()
        {
            using var profile = new TestProfile();
            var config = new ConfigFile(Path.Combine(profile.ConfigDir, "test.mod.cfg"), false);
            var entry = config.Bind("Keys", "Dig", KeyCode.K, new ConfigDescription("Digs.", null, new BindruneAttributes { HeldItems = new[] { "skill:Pickaxes" } }));
            var dig = SingleKey("Dig", KeyCode.K);
            KnownSituations.ReadFrom(dig.Id, entry);

            Assert.Equal(new[] { Holding("skill:Pickaxes") }, KnownSituations.For(dig, out _).ToArray());
        }

        [Fact]
        public void WhatYouSetWinsOverTheSharedList()
        {
            using var profile = new TestProfile();
            var dig = SingleKey("Dig", KeyCode.K);
            Directory.CreateDirectory(Path.GetDirectoryName(profile.SharedListFile));
            File.WriteAllLines(profile.SharedListFile, new[] { dig.Id + "\tMap\t" });
            YouSay(dig, "Inventory");

            var situations = KnownSituations.For(dig, out var source);

            Assert.Equal(SituationSource.You, source);
            Assert.Equal(new[] { "Inventory" }, situations.ToArray());
        }

        [Fact]
        public void TheSharedListDescribesAModThatSaysNothing()
        {
            using var profile = new TestProfile();
            var dig = SingleKey("Dig", KeyCode.K);
            Directory.CreateDirectory(Path.GetDirectoryName(profile.SharedListFile));
            File.WriteAllLines(profile.SharedListFile, new[] { "# a comment", dig.Id + "\tBuild menu, Map\tskill:Pickaxes, item:ood_remote" });

            var situations = KnownSituations.For(dig, out var source);

            Assert.Equal(SituationSource.Pack, source);
            Assert.Equal(new[] { "Build menu", "Map", Holding("skill:Pickaxes"), Holding("item:ood_remote") }.OrderBy(s => s, System.StringComparer.Ordinal),
                situations.OrderBy(s => s, System.StringComparer.Ordinal));
        }

        [Fact]
        public void WithoutTheSharedListItIsWrittenAsAnEmptyTemplate()
        {
            using var profile = new TestProfile();

            KnownSituations.For(SingleKey("Dig", KeyCode.K), out var source);

            Assert.Equal(SituationSource.None, source);
            Assert.True(File.Exists(profile.SharedListFile));
            Assert.All(File.ReadAllLines(profile.SharedListFile), line => Assert.StartsWith("#", line));
        }

        // ---------- the preview ----------

        [Fact]
        public void APreviewNamesTheClashesTheScanFindsOnceTheKeyIsSet()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.L);
            var others = new[]
            {
                Shortcut("Hard", KeyCode.K, KeyCode.LeftAlt), SingleKey("Soft", KeyCode.K), Shortcut("Note", KeyCode.K, KeyCode.LeftControl),
            };
            var candidate = new KeyCombo(KeyCode.K, new[] { KeyCode.LeftAlt });

            var previewed = ConflictEngine.Preview(mine, candidate, others).ToDictionary(c => c.A.Id == mine.Id ? c.B.Id : c.A.Id, c => c.Severity);

            mine.Combo = candidate;
            ConflictIndex.Rebuild(others.Concat(new[] { mine }));
            var found = ConflictIndex.For(mine.Id).ToDictionary(c => c.A.Id == mine.Id ? c.B.Id : c.A.Id, c => c.Severity);

            Assert.Equal(3, previewed.Count);
            Assert.Equal(found.OrderBy(p => p.Key, System.StringComparer.Ordinal), previewed.OrderBy(p => p.Key, System.StringComparer.Ordinal));
        }

        [Fact]
        public void APreviewLeavesOutWhatIsNeverCompared()
        {
            using var profile = new TestProfile();
            var pad = Shortcut("Pad", KeyCode.K);
            pad.Compared = false;
            var plumbing = Shortcut("Plumbing", KeyCode.K);
            plumbing.Internal = true;

            Assert.Empty(ConflictEngine.Preview(Shortcut("Mine", KeyCode.L), new KeyCombo(KeyCode.K, null), new[] { pad, plumbing }));
        }

        /// <summary>A mod's own description of its bind, read by its name as Bindrune reads any mod's.</summary>
        private sealed class BindruneAttributes
        {
            public string[] Situations;
            public string[] HeldItems;
        }
    }
}
