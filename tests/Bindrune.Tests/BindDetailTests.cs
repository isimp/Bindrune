using System.Linq;
using Bindrune.Conflicts;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What the right-hand column shows for a selected bind: its key, the controls that change it
    /// or why it cannot be changed, when Default is offered, and every clash it is in, worst
    /// first, each with the bind on its other side and how it is tagged. Drawing it needs the
    /// game; what it shows is decided here.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class BindDetailTests
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

        private static BindDetail Of(BindEntry bind, bool pending = false, bool capturing = false, bool storedNamesShown = false) =>
            BindDetail.Of(bind, pending, capturing, storedNamesShown);

        [Fact]
        public void ABindShowsWhoseItIsAndItsKey()
        {
            using var profile = new TestProfile();
            var view = Of(Shortcut("Open", KeyCode.K, KeyCode.LeftAlt));

            Assert.Equal("Test", view.Owner);
            Assert.Equal("Open", view.Label);
            Assert.Equal("LeftAlt + K", view.KeyLine);
        }

        [Fact]
        public void AnUnboundBindSaysSo()
        {
            using var profile = new TestProfile();
            var bind = Shortcut("Open", KeyCode.None);

            Assert.Equal("not bound", Of(bind).KeyLine);
            Assert.Null(Of(bind, storedNamesShown: true).StoredAs);
        }

        [Fact]
        public void TheStoredNameIsLeftOutWhereItIsTheNameShown()
        {
            using var profile = new TestProfile();
            Assert.Null(Of(Shortcut("Open", KeyCode.K), storedNamesShown: true).StoredAs);
        }

        [Fact]
        public void ABindThatCannotBeChangedSaysWhyInsteadOfOfferingControls()
        {
            using var profile = new TestProfile();
            var bind = Shortcut("Open", KeyCode.K);
            bind.Editable = false;
            bind.ReadOnlyReason = "the game reads this key in its own code";

            var view = Of(bind, pending: true);
            Assert.Equal(BindDetail.Controls.Locked, view.Mode);
            Assert.Equal("Locked: the game reads this key in its own code", view.Locked);
        }

        [Fact]
        public void AKeyPressedForABindWaitsForAnAnswerInPlaceOfTheButtons()
        {
            using var profile = new TestProfile();
            Assert.Equal(BindDetail.Controls.Pending, Of(Shortcut("Open", KeyCode.K), pending: true).Mode);
            Assert.Equal(BindDetail.Controls.Edit, Of(Shortcut("Open", KeyCode.K)).Mode);
        }

        [Fact]
        public void WhileAKeyIsAwaitedTheButtonsSaySoAndDefaultStepsAside()
        {
            using var profile = new TestProfile();
            var (bind, _) = profile.ModBind(KeyCode.L);

            var waiting = Of(bind, capturing: true);
            Assert.Equal("press a key...", waiting.RebindLabel);
            Assert.Equal("Cancel", waiting.ClearLabel);
            Assert.True(waiting.TellsHowToCapture);
            Assert.False(waiting.OffersDefault);

            var idle = Of(bind);
            Assert.Equal("Rebind", idle.RebindLabel);
            Assert.Equal("Clear", idle.ClearLabel);
            Assert.False(idle.TellsHowToCapture);
        }

        [Fact]
        public void DefaultIsOfferedOnlyWhereTheModsOwnKeyDiffersFromTheOneSet()
        {
            using var profile = new TestProfile();
            var (bind, _) = profile.ModBind(KeyCode.L);

            // The mod binds H; the profile set L.
            Assert.True(Of(bind).OffersDefault);

            bind.Combo = new KeyCombo(KeyCode.H, null);
            Assert.False(Of(bind).OffersDefault);
        }

        [Fact]
        public void ClashesAreListedWorstFirstEachWithTheBindOnItsOtherSide()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.K, KeyCode.LeftAlt);
            var note = Shortcut("Note", KeyCode.K, KeyCode.LeftControl);
            var soft = SingleKey("Soft", KeyCode.K);
            var hard = Shortcut("Hard", KeyCode.K, KeyCode.LeftAlt);
            ConflictIndex.Rebuild(new[] { note, soft, mine, hard });

            var lines = Of(mine).Conflicts;
            Assert.Equal(new[] { "Hard", "Soft", "Note" }, lines.Select(l => l.Other.Label).ToArray());
            Assert.Equal(new[] { "hard", "soft", "note" }, lines.Select(l => l.Tag).ToArray());
            Assert.All(lines, l => Assert.False(l.Muted));
        }

        [Fact]
        public void AConfirmedClashSaysSoAndAMutedOneIsTaggedMuted()
        {
            using var profile = new TestProfile();
            var mine = Shortcut("Mine", KeyCode.K);
            var other = Shortcut("Other", KeyCode.K);
            ConflictIndex.Rebuild(new[] { mine, other });

            var conflict = ConflictIndex.For(mine.Id).Single();
            conflict.Confirmed = true;
            Assert.Equal("hard - confirmed", Of(mine).Conflicts.Single().Tag);

            MuteStore.Toggle(conflict);
            var line = Of(mine).Conflicts.Single();
            Assert.True(line.Muted);
            Assert.Equal("muted", line.Tag);
        }

        [Fact]
        public void ABindNothingComparesHasNoClashSectionAndOneWithNoneSaysSo()
        {
            using var profile = new TestProfile();
            var alone = Shortcut("Alone", KeyCode.K);
            ConflictIndex.Rebuild(new[] { alone });
            Assert.Empty(Of(alone).Conflicts);

            alone.Compared = false;
            Assert.Null(Of(alone).Conflicts);
        }
    }
}
