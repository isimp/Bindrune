using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Personal;
using UnityEngine;
using Xunit;

namespace Bindrune.Tests
{
    /// <summary>
    /// Taking over the keybinds kept in Keepsake, run on the shared sample: which lines match
    /// which binds, what each key becomes, and that only those lines leave the file.
    /// </summary>
    public class HandoverTests
    {
        private static string[] PinsSample() => File.ReadAllLines(Path.Combine("contract", "keepsake.pins"));

        private static Dictionary<string, (string Bind, Type SettingType)> Binds(params (string Section, string Key, Type Type)[] binds) =>
            binds.ToDictionary(
                b => KeepsakeContract.Name("isimp.Example.cfg", b.Section, b.Key),
                b => (b.Section + "/" + b.Key, b.Type));

        [Fact]
        public void KeybindsAreMatchedToTheirBinds()
        {
            var matches = KeepsakeContract.Match(KeepsakeContract.Parse(PinsSample()), Binds(
                ("Keys", "Open", typeof(KeyCode)),
                ("Keys", "Shortcut", typeof(KeyboardShortcut)),
                ("Section: with colon", "Toggle", typeof(KeyCode))));

            Assert.Equal(new[] { "Keys/Open", "Keys/Shortcut", "Section: with colon/Toggle" }, matches.Select(m => m.Bind));
        }

        [Fact]
        public void SettingsThatAreNotBindsAreLeftAlone()
        {
            var matches = KeepsakeContract.Match(KeepsakeContract.Parse(PinsSample()), Binds(("Keys", "Open", typeof(KeyCode))));
            Assert.Equal(new[] { "Keys/Open" }, matches.Select(m => m.Bind));
        }

        [Fact]
        public void TheKeptKeyAndTheProfilesKeyAreTakenOver()
        {
            var open = KeepsakeContract.Match(KeepsakeContract.Parse(PinsSample()), Binds(("Keys", "Open", typeof(KeyCode)))).Single();

            Assert.Equal(new KeyCombo(KeyCode.K, null), open.Yours);
            Assert.Equal(new KeyCombo(KeyCode.H, null), open.Profile);
        }

        [Fact]
        public void AShortcutKeepsItsModifiers()
        {
            var shortcut = KeepsakeContract.Match(KeepsakeContract.Parse(PinsSample()), Binds(("Keys", "Shortcut", typeof(KeyboardShortcut)))).Single();

            Assert.Equal(new KeyCombo(KeyCode.H, new[] { KeyCode.LeftAlt }), shortcut.Yours);
            Assert.Equal(KeyCombo.None, shortcut.Profile);
        }

        [Fact]
        public void ALineWithoutTheProfilesKeyHasNone()
        {
            var toggle = KeepsakeContract.Match(KeepsakeContract.Parse(PinsSample()), Binds(("Section: with colon", "Toggle", typeof(KeyCode)))).Single();
            Assert.Null(toggle.Profile);
        }

        [Fact]
        public void AValueTheSettingCannotHoldIsNotTakenOver()
        {
            var lines = new[] { KeepsakeContract.PinsVersion, "isimp.Example.cfg\tKeys\tOpen\tNotAKey\tH" };
            Assert.Empty(KeepsakeContract.Match(KeepsakeContract.Parse(lines), Binds(("Keys", "Open", typeof(KeyCode)))));
        }

        [Fact]
        public void AShortcutTheSettingCannotReadIsNotTakenOverAsNoKey()
        {
            var lines = new[] { KeepsakeContract.PinsVersion, "isimp.Example.cfg\tKeys\tShortcut\tNot + A + Key\tNone" };
            Assert.Empty(KeepsakeContract.Match(KeepsakeContract.Parse(lines), Binds(("Keys", "Shortcut", typeof(KeyboardShortcut)))));
        }

        [Fact]
        public void AShortcutWrittenAsNoneIsTakenOverAsNoKey()
        {
            Assert.True(KeepsakeContract.TryCombo("None", typeof(KeyboardShortcut), out var combo));
            Assert.Equal(KeyCombo.None, combo);
        }

        [Fact]
        public void FileNamesMatchWhateverTheirCaseAndSlashes() =>
            Assert.Equal(KeepsakeContract.Name("Sub\\Mod.cfg", "S", "K"), KeepsakeContract.Name("sub/mod.cfg", "S", "K"));

        [Fact]
        public void OnlyTheTakenLinesLeaveTheFile()
        {
            var sample = PinsSample();
            var taken = KeepsakeContract.Match(KeepsakeContract.Parse(sample), Binds(("Keys", "Open", typeof(KeyCode)))).Select(m => m.Line);

            var left = KeepsakeContract.Without(sample, taken);

            Assert.Equal(sample.Length - 1, left.Length);
            Assert.DoesNotContain(left, l => l.StartsWith("isimp.Example.cfg\tKeys\tOpen\t"));
            Assert.Equal(sample.Where(l => !l.StartsWith("isimp.Example.cfg\tKeys\tOpen\t")), left);
        }

        [Fact]
        public void PinsOfAnotherVersionAreNotTouched() =>
            Assert.Null(KeepsakeContract.Parse(new[] { "# keepsake pins v2", "isimp.Example.cfg\tKeys\tOpen\tK\tH" }));

        [Fact]
        public void PinsWithoutAVersionAreNotTouched() =>
            Assert.Null(KeepsakeContract.Parse(new[] { "isimp.Example.cfg\tKeys\tOpen\tK\tH" }));

        [Fact]
        public void AnAliasedKeyIsTheSameKey()
        {
            Assert.True(KeepsakeContract.TryCombo("RightCommand", typeof(KeyCode), out var command));
            Assert.True(KeepsakeContract.TryCombo("RightMeta", typeof(KeyCode), out var meta));
            Assert.Equal(meta, command);
        }
    }
}
