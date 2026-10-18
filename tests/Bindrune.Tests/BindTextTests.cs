using System.Collections.Generic;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.Personal;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// What a bind's detail says in words: whom a key is shared with, where a bind's situations
    /// come from, how it reads modifiers and where it was found, the profile's key beside yours,
    /// and the game's other key for the same control.
    /// </summary>
    [Collection(ProfileCollection.Name)]
    public class BindTextTests
    {
        private static BindEntry Bind(string id, string owner, string label, BindSource source = BindSource.ModTyped,
            ModifierBehavior modifiers = ModifierBehavior.Strict, bool editable = true) => new BindEntry
        {
            Id = id,
            OwnerName = owner,
            Label = label,
            Source = source,
            Modifiers = modifiers,
            Combo = new KeyCombo(KeyCode.K, null),
            Editable = editable,
        };

        [Fact]
        public void AKeySharedWithOneOtherBindNamesItAndItsMod()
        {
            var mine = Bind("a", "Mine", "Open");
            var theirs = Bind("b", "Theirs", "Dig");

            Assert.Equal("Shared with Theirs's \"Dig\", which should not interfere.",
                BindText.SharedWith(new List<Conflict> { new Conflict { A = mine, B = theirs } }, mine));
            Assert.Equal("Shared with Theirs's \"Dig\", which should not interfere.",
                BindText.SharedWith(new List<Conflict> { new Conflict { A = theirs, B = mine } }, mine));
        }

        [Fact]
        public void AKeySharedWithSeveralBindsCountsThem()
        {
            var mine = Bind("a", "Mine", "Open");
            var shared = new List<Conflict>
            {
                new Conflict { A = mine, B = Bind("b", "One", "Dig") },
                new Conflict { A = mine, B = Bind("c", "Two", "Fly") },
            };

            Assert.Equal("Shared with 2 binds, none of which should interfere.", BindText.SharedWith(shared, mine));
        }

        [Fact]
        public void EachSourceOfSituationsSaysWhetherYouCanChangeThem()
        {
            Assert.Equal("Digger describes this bind itself, so these cannot be changed here.", BindText.Provenance(SituationSource.Mod, "Digger"));
            Assert.Equal("Taken from Digger's own key hints. Change anything to make it yours instead.", BindText.Provenance(SituationSource.KeyHints, "Digger"));
            Assert.Equal("From the shared list that travels with this profile. Change anything to make it yours instead.", BindText.Provenance(SituationSource.Pack, "Digger"));
            Assert.Equal("What the game does with its own bind. Change anything to make it yours instead.", BindText.Provenance(SituationSource.Game, "Digger"));
        }

        [Fact]
        public void SituationsYouSetOrNoneNeedNoWordOnWhereTheyCameFrom()
        {
            Assert.Null(BindText.Provenance(SituationSource.None, "Digger"));
            Assert.Null(BindText.Provenance(SituationSource.You, "Digger"));
        }

        [Fact]
        public void HowABindReadsModifiersIsToldForEachKind()
        {
            Assert.StartsWith("A gamepad button,", BindText.Behaviour(Bind("a", "Valheim", "Jump", BindSource.Gamepad, ModifierBehavior.SingleKey)));
            Assert.StartsWith("Fires while this modifier is held,", BindText.Behaviour(Bind("a", "Valheim", "Hotbar1", BindSource.Vanilla, ModifierBehavior.Required)));
            Assert.StartsWith("Needs exactly these modifiers,", BindText.Behaviour(Bind("a", "Mod", "Open", BindSource.ModTyped, ModifierBehavior.Strict)));
            Assert.StartsWith("Stored as text,", BindText.Behaviour(Bind("a", "Mod", "Open", BindSource.ModText, ModifierBehavior.Unknown)));
            Assert.StartsWith("A single key, read without looking at modifiers,", BindText.Behaviour(Bind("a", "Valheim", "Jump", BindSource.Vanilla, ModifierBehavior.SingleKey)));
            Assert.StartsWith("A single key, read without looking at modifiers,", BindText.Behaviour(Bind("a", "Mod", "Fly", BindSource.Jotunn, ModifierBehavior.SingleKey)));
            Assert.StartsWith("A single key, with no modifier to match,", BindText.Behaviour(Bind("a", "Mod", "Open", BindSource.ModTyped, ModifierBehavior.SingleKey)));
        }

        [Fact]
        public void WhereABindWasFoundIsSaidInWords()
        {
            Assert.EndsWith("\nFound as: one of the game's own controls.", BindText.Behaviour(Bind("a", "Valheim", "Jump", BindSource.Vanilla, ModifierBehavior.SingleKey)));
            Assert.Equal("one of the game's gamepad buttons", BindText.SourceLabel(BindSource.Gamepad));
            Assert.Equal("a mod button registered through Jotunn", BindText.SourceLabel(BindSource.Jotunn));
            Assert.Equal("a keybind setting in the mod's config", BindText.SourceLabel(BindSource.ModTyped));
            Assert.Equal("a text setting in the mod's config that reads as a key", BindText.SourceLabel(BindSource.ModText));
        }

        [Fact]
        public void YourKeyShowsTheProfilesBesideItWhereTheyDiffer()
        {
            using var profile = new TestProfile();
            var (bind, _) = profile.ModBind(KeyCode.L);

            Assert.Equal("", BindText.ProfileKeyNote(bind, yours: true));

            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.Q, null), SaveTarget.Personal));
            Assert.Equal("profile: L", BindText.ProfileKeyNote(bind, yours: true));
            Assert.Equal("", BindText.ProfileKeyNote(bind, yours: false));

            // Yours set back to the profile's key: nothing differs, so nothing is shown.
            Assert.Null(BindWriter.Apply(bind, new KeyCombo(KeyCode.L, null), SaveTarget.Personal));
            Assert.Equal("", BindText.ProfileKeyNote(bind, yours: true));
        }

        [Fact]
        public void TheGamesSecondKeyForAControlIsFoundWhereItCanBeChanged()
        {
            using var profile = new TestProfile();
            var jump = Bind(BindIds.Vanilla("Jump"), "Valheim", "Jump", BindSource.Vanilla, ModifierBehavior.SingleKey);
            var jumpAlt = Bind(BindIds.Vanilla("JumpAlt"), "Valheim", "JumpAlt", BindSource.Vanilla, ModifierBehavior.SingleKey);
            var mod = Bind("config:isimp.Example:Keys:Jump", "Example", "Jump");
            TestProfile.Scanned(jump, jumpAlt, mod);

            Assert.Same(jumpAlt, BindRegistry.AlternateOf(jump));
            Assert.Null(BindRegistry.AlternateOf(mod));

            jumpAlt.Editable = false;
            Assert.Null(BindRegistry.AlternateOf(jump));
        }
    }
}
