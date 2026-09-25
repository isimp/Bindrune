using System.Linq;
using Bindrune.Conflicts;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// A key and its modifiers compare the same however they were given, the stored form never
    /// depends on anything but Unity's key names, and every bind source gets ids of its own.
    /// </summary>
    public class KeyComboTests
    {
        [Fact]
        public void ModifiersAreOnlyModifiersEachOnceWhateverOrderTheyCameIn()
        {
            var combo = new KeyCombo(KeyCode.K, new[] { KeyCode.LeftAlt, KeyCode.A, KeyCode.LeftShift, KeyCode.LeftAlt });

            Assert.Equal(2, combo.Modifiers.Length);
            Assert.Contains(KeyCode.LeftAlt, combo.Modifiers);
            Assert.Contains(KeyCode.LeftShift, combo.Modifiers);
            Assert.Equal(combo, new KeyCombo(KeyCode.K, new[] { KeyCode.LeftShift, KeyCode.LeftAlt }));
            Assert.Equal(combo.GetHashCode(), new KeyCombo(KeyCode.K, new[] { KeyCode.LeftShift, KeyCode.LeftAlt }).GetHashCode());
        }

        [Fact]
        public void AKeyNeverModifiesItself()
        {
            var combo = new KeyCombo(KeyCode.LeftAlt, new[] { KeyCode.LeftAlt, KeyCode.LeftControl });

            Assert.Equal(new[] { KeyCode.LeftControl }, combo.Modifiers);
        }

        [Fact]
        public void DifferentModifiersOrNoneMakeADifferentCombo()
        {
            var plain = new KeyCombo(KeyCode.K, null);
            var alt = new KeyCombo(KeyCode.K, new[] { KeyCode.LeftAlt });
            var rightAlt = new KeyCombo(KeyCode.K, new[] { KeyCode.RightAlt });

            Assert.NotEqual(plain, alt);
            Assert.NotEqual(alt, rightAlt);
            Assert.Equal(plain.MainToken, alt.MainToken);
            Assert.False(plain.SameModifiers(alt));
        }

        [Fact]
        public void AKeyKnownOnlyByItsPathIsTheSameWhateverItsCase()
        {
            var a = new KeyCombo(KeyCode.None, null, "<Keyboard>/F13");
            var b = new KeyCombo(KeyCode.None, null, "<keyboard>/f13");

            Assert.True(a.IsBound);
            Assert.Equal(a, b);
            Assert.NotEqual(a.MainToken, new KeyCombo(KeyCode.F13, null).MainToken);
        }

        [Fact]
        public void NoKeyIsUnboundAndSaysSo()
        {
            Assert.False(KeyCombo.None.IsBound);
            Assert.Equal("<unbound>", KeyCombo.None.ToString());
            Assert.Equal("not bound", KeyCombo.None.MainLabel);

            // One made without the constructor, as a field that was never set is.
            var unset = default(KeyCombo);
            Assert.False(unset.IsBound);
            Assert.Equal(KeyCombo.None.MainToken, unset.MainToken);
        }

        [Fact]
        public void TheStoredFormIsUnitysKeyNamesModifiersFirst()
        {
            var combo = new KeyCombo(KeyCode.Z, new[] { KeyCode.LeftAlt, KeyCode.LeftShift });

            Assert.Equal("LeftShift + LeftAlt + Z", combo.ToString());
            Assert.Equal("Z", new KeyCombo(KeyCode.Z, null).ToString());
        }

        [Fact]
        public void OnlyControlAltShiftAndCommandAreModifiers()
        {
            foreach (var key in new[] { KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt,
                         KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftCommand, KeyCode.RightCommand })
                Assert.True(KeyCombo.IsModifier(key), key.ToString());

            foreach (var key in new[] { KeyCode.Space, KeyCode.Tab, KeyCode.CapsLock, KeyCode.Mouse0, KeyCode.A })
                Assert.False(KeyCombo.IsModifier(key), key.ToString());
        }

        [Fact]
        public void EveryBindSourceHasIdsOfItsOwn()
        {
            var ids = new[]
            {
                BindIds.Vanilla("Jump"), BindIds.Game("Jump"), BindIds.Jotunn("Jump"),
                BindIds.Gamepad("Jump", "Default"), BindIds.Config("isimp.Example", "Keys", "Jump"),
            };

            Assert.Equal(ids.Length, ids.Distinct().Count());
        }

        [Fact]
        public void AConfigIdTellsSectionsAndModsApart()
        {
            Assert.NotEqual(BindIds.Config("a", "Keys", "Open"), BindIds.Config("a", "Other", "Open"));
            Assert.NotEqual(BindIds.Config("a", "Keys", "Open"), BindIds.Config("b", "Keys", "Open"));
            Assert.NotEqual(BindIds.Gamepad("Jump", "Default"), BindIds.Gamepad("Jump", "Alternative1"));
        }

        [Fact]
        public void AClashBetweenTwoBindsIsTheSameClashWhicheverComesFirst()
        {
            var a = new BindEntry { Id = "cfg:a:Keys:Open" };
            var b = new BindEntry { Id = "vanilla:Jump" };

            Assert.Equal(new Conflict { A = a, B = b }.PairKey, new Conflict { A = b, B = a }.PairKey);
        }

        [Fact]
        public void NearbyKeysAreTheOnesAroundIt()
        {
            var around = KeyGrid.Around(KeyCode.G).Take(4).ToList();

            Assert.Contains(KeyCode.F, around);
            Assert.Contains(KeyCode.H, around);
            Assert.Equal(KeyGrid.Distance(KeyCode.G, KeyCode.H), KeyGrid.Distance(KeyCode.H, KeyCode.G));
            Assert.True(KeyGrid.Distance(KeyCode.G, KeyCode.H) < KeyGrid.Distance(KeyCode.G, KeyCode.P));
        }

        [Fact]
        public void KeysWithNoPlaceOnTheBoardHaveNoNeighbours()
        {
            foreach (var key in new[] { KeyCode.Escape, KeyCode.CapsLock, KeyCode.Keypad5, KeyCode.Mouse3 })
            {
                Assert.Empty(KeyGrid.Around(key));
                Assert.Null(KeyGrid.Distance(key, KeyCode.G));
            }

            Assert.DoesNotContain(KeyCode.Escape, KeyGrid.Around(KeyCode.F1));
        }
    }
}
