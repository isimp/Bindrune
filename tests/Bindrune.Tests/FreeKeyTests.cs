using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Discovery;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What Bindrune offers when a key you pressed for a bind clashes (docs/technical.md, Free
    /// keys): up to three others, the first the same key with a modifier added, swapped or taken
    /// away where the bind can hold one, the rest the nearest keys; only keys that run into
    /// nothing worse than a note, a shared one marked as shared; and only keys the bind can take.
    /// A key nothing uses at all is saved at once, without a preview.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class FreeKeyTests
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

        /// <summary>What a scan found, as BindRegistry.Refresh leaves it.</summary>
        private static void Scanned(params BindEntry[] binds) =>
            typeof(BindRegistry).GetProperty("All")!.GetSetMethod(true)!.Invoke(null, new object[] { binds.ToList() });

        private static Severity? Worst(BindEntry bind, KeyCombo combo)
        {
            var clashes = ConflictEngine.Preview(bind, combo, BindRegistry.All);
            return clashes.Count == 0 ? (Severity?)null : clashes.Min(c => c.Severity);
        }

        [Fact]
        public void AtMostThreeKeysAreOfferedAndEachRunsIntoNothingWorseThanANote()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K, KeyCode.LeftAlt);
            Scanned(mine, taken, Shortcut("Near", KeyCode.J, KeyCode.LeftAlt), SingleKey("Plain", KeyCode.L));

            var offered = FreeKeys.For(mine, taken.Combo, 3);

            Assert.InRange(offered.Count, 1, 3);
            Assert.All(offered, key => Assert.True(Worst(mine, key.Combo) == null || Worst(mine, key.Combo) == Severity.Note, key.Combo + " " + Worst(mine, key.Combo)));
            Assert.All(offered, key => Assert.False(key.Combo.Equals(taken.Combo)));
        }

        [Fact]
        public void TheFirstKeyOfferedIsTheSameKeyWithItsModifierChanged()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K, KeyCode.LeftAlt);
            Scanned(mine, taken);

            var first = FreeKeys.For(mine, taken.Combo, 3).First();

            Assert.Equal(KeyCode.K, first.Combo.Main);
            Assert.False(first.Combo.SameModifiers(taken.Combo));
        }

        [Fact]
        public void TheOthersAreTheNearestKeysKeepingTheModifierThatWasHeld()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K, KeyCode.LeftAlt);
            Scanned(mine, taken);

            var others = FreeKeys.For(mine, taken.Combo, 3).Where(k => k.Combo.Main != KeyCode.K).ToList();

            Assert.NotEmpty(others);
            Assert.All(others, key => Assert.InRange(KeyGrid.Distance(KeyCode.K, key.Combo.Main) ?? 99f, 0.1f, 1.6f));
            Assert.Contains(others, key => key.Combo.Modifiers.SequenceEqual(new[] { KeyCode.LeftAlt }));
        }

        [Fact]
        public void ABindThatHoldsOneKeyIsOfferedKeysWithoutModifiers()
        {
            using var profile = new TestProfile();
            var mine = SingleKey("Mine", KeyCode.P);
            var taken = SingleKey("Taken", KeyCode.K);
            Scanned(mine, taken);

            // Every key worth offering, not only the first three, so a modifier ranked low is seen too.
            var offered = FreeKeys.For(mine, taken.Combo, 50);

            Assert.NotEmpty(offered);
            Assert.All(offered, key => Assert.Empty(key.Combo.Modifiers));
        }

        [Fact]
        public void NoModifierGoesOnAKeyASingleKeyBindHolds()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var plain = SingleKey("Plain", KeyCode.K);
            Scanned(mine, plain);

            // K with any modifier still fires Plain, so it is no way out.
            Assert.DoesNotContain(FreeKeys.For(mine, new KeyCombo(KeyCode.K, null), 3), key => key.Combo.Main == KeyCode.K);
        }

        [Fact]
        public void AModifierNobodyHereUsesIsNotOffered()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K);
            Scanned(mine, taken, Shortcut("UsesAlt", KeyCode.F5, KeyCode.LeftAlt));

            var offered = FreeKeys.For(mine, taken.Combo, 50);

            Assert.Contains(offered, key => key.Combo.Modifiers.Contains(KeyCode.LeftAlt));
            Assert.DoesNotContain(offered, key => key.Combo.Modifiers.Contains(KeyCode.LeftControl) || key.Combo.Modifiers.Contains(KeyCode.LeftShift));
        }

        [Fact]
        public void WithNoModifierInUseAltIsTheOneOffered()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K);
            Scanned(mine, taken);

            var offered = FreeKeys.For(mine, taken.Combo, 50);

            Assert.Contains(offered, key => key.Combo.Modifiers.SequenceEqual(new[] { KeyCode.LeftAlt }));
            Assert.DoesNotContain(offered, key => key.Combo.Modifiers.Contains(KeyCode.LeftControl) || key.Combo.Modifiers.Contains(KeyCode.LeftShift));
        }

        [Fact]
        public void ABindThatCannotBeChangedHereIsOfferedNothing()
        {
            using var profile = new TestProfile();
            var locked = Shortcut("Locked", KeyCode.P);
            locked.Editable = false;
            var taken = Shortcut("Taken", KeyCode.K);
            Scanned(locked, taken);

            Assert.Empty(FreeKeys.For(locked, taken.Combo, 50));
        }

        [Fact]
        public void AKeySharedWithABindNeverLiveAtTheSameTimeIsOfferedMarkedAsShared()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            var taken = Shortcut("Taken", KeyCode.K);
            var apart = Shortcut("Apart", KeyCode.K, KeyCode.LeftAlt);
            Assert.Null(SituationStore.Toggle(mine.Id, "Map"));
            Assert.Null(SituationStore.Toggle(apart.Id, "Inventory"));
            Scanned(mine, taken, apart);

            var shared = FreeKeys.For(mine, taken.Combo, 3).FirstOrDefault(k => k.Combo.Equals(apart.Combo));

            Assert.NotNull(shared);
            Assert.Contains(shared.Shared, c => c.A.Id == apart.Id || c.B.Id == apart.Id);
        }

        [Fact]
        public void NothingIsOfferedForAModifierOrNoKey()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            Scanned(mine, Shortcut("UsesAlt", KeyCode.F5, KeyCode.LeftAlt));

            Assert.Empty(FreeKeys.For(mine, new KeyCombo(KeyCode.LeftAlt, null), 50));
            Assert.Empty(FreeKeys.For(mine, new KeyCombo(KeyCode.RightControl, null), 50));
            Assert.Empty(FreeKeys.For(mine, KeyCombo.None, 50));
        }

        [Fact]
        public void NothingIsOfferedForAGamepadButton()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            Scanned(mine, Shortcut("UsesAlt", KeyCode.F5, KeyCode.LeftAlt));

            Assert.Empty(FreeKeys.For(mine, new KeyCombo(KeyCode.JoystickButton0, null), 50));
        }

        [Fact]
        public void AKeyNothingUsesIsFreeButOneSharedWithANoteIsNot()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.P);
            Scanned(mine, Shortcut("Other", KeyCode.K, KeyCode.LeftAlt));

            Assert.True(FreeKeys.Unused(mine, new KeyCombo(KeyCode.L, null)));
            Assert.False(FreeKeys.Unused(mine, new KeyCombo(KeyCode.K, new[] { KeyCode.LeftControl })));
        }

        // ---------- what a bind takes ----------

        [Fact]
        public void AModsShortcutTakesAnyModifiers()
        {
            Assert.Null(BindWriter.Refusal(Shortcut("Mine", KeyCode.P), new KeyCombo(KeyCode.K, new[] { KeyCode.LeftAlt, KeyCode.LeftShift })));
        }

        [Fact]
        public void AKeyOnlyTheGamesNewerInputSeesCannotGoOnAModsBind()
        {
            Assert.Equal(BindWriter.UnseenByMods, BindWriter.Refusal(Shortcut("Mine", KeyCode.P), new KeyCombo(KeyCode.None, null, "<Keyboard>/OEM1")));
        }

        [Fact]
        public void ABindThatCannotBeChangedHereSaysWhy()
        {
            var text = Shortcut("Typed", KeyCode.P);
            text.Editable = false;
            text.ReadOnlyReason = "kept as text";

            Assert.Equal("kept as text", BindWriter.Refusal(text, new KeyCombo(KeyCode.K, null)));
        }

        [Fact]
        public void ClearingAModsBindIsAllowed()
        {
            Assert.Null(BindWriter.Refusal(Shortcut("Mine", KeyCode.P), KeyCombo.None));
        }
    }
}
