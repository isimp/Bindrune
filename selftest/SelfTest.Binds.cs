using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Discovery;
using Bindrune.UI;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        private static BindEntry Bind(string key) => BindRegistry.All.FirstOrDefault(b => b.Id == TestBinds.Id(key));

        private static BindEntry ById(string id) => BindRegistry.All.FirstOrDefault(b => b.Id == id);

        /// <summary>The clash between two test settings, as the panel lists it, or null.</summary>
        private static Conflict Pair(string a, string b)
        {
            var idA = TestBinds.Id(a);
            var idB = TestBinds.Id(b);
            return ConflictIndex.For(idA).FirstOrDefault(c => (c.A.Id == idA && c.B.Id == idB) || (c.A.Id == idB && c.B.Id == idA));
        }

        /// <summary>Closes the panel and opens it again, which reads every bind and judges every clash afresh, as for a player.</summary>
        private static IEnumerator Reopen()
        {
            BindrunePanel.Close(quietly: true);
            yield return null;
            BindrunePanel.Open();
            yield return null;
        }

        private IEnumerator Finding()
        {
            TestBinds.Bind(_config);

            var clock = Stopwatch.StartNew();
            BindrunePanel.Close(quietly: true);
            BindrunePanel.Open();
            Note("opening the panel, which reads every bind and judges every clash", Ms(clock.Elapsed) + " ms");
            yield return null;

            Check(BindrunePanel.IsOpen, "the panel opens");
            var all = BindRegistry.All;
            Note("binds found", Count(all.Count) + ", of them " + Count(all.Count(b => b.Source == BindSource.Vanilla)) + " the game's and " +
                                Count(ConflictIndex.All.Count) + " clashes");

            // The game's own controls.
            var rebindable = all.Count(b => b.Source == BindSource.Vanilla && b.Editable && !b.Internal);
            Check(rebindable >= 38, "every control the game lets you rebind is listed and can be changed here", Count(rebindable) + " found, the game has 38");
            var jump = ById(BindIds.Vanilla("Jump"));
            Check(jump != null && jump.Editable, "Jump is listed and can be changed", jump?.ToString());

            var digits = Enumerable.Range(1, 8).Select(i => ById(BindIds.Vanilla("Hotbar" + Count(i)))).ToList();
            Check(digits.All(d => d != null && d.Editable), "the eight hotbar keys are listed and can be changed",
                string.Join(", ", digits.Select(d => d == null ? "missing" : d.Combo.ToString()).ToArray()));

            // Keys the game reads in its own code.
            foreach (var name in new[] { "Gamepad layout", "Screenshot", "Mouse capture", "Hide HUD" })
            {
                var read = ById(BindIds.Game(name));
                Check(read != null && !read.Editable, name + " is listed as the game's own, locked", read?.ToString());
            }
            var capture = ById(BindIds.Game("Mouse capture"));
            Check(capture != null && capture.Modifiers == ModifierBehavior.Required, "Ctrl+F1 needs Ctrl held and fires whatever else is held");

            // Bindrune's own settings, as any mod's.
            var open = ById(BindIds.Config(Plugin.Guid, "General", "OpenKey"));
            Check(open != null && open.Source == BindSource.ModTyped && open.Modifiers == ModifierBehavior.Strict, "Bindrune's own open key is listed as a typed shortcut", open?.ToString());

            // The self-test's settings, each read for what its storage proves.
            var missing = TestBinds.Keys().Where(k => Bind(k) == null).ToList();
            Check(missing.Count == 0, "every test setting is found", missing.Count == 0 ? null : "missing " + string.Join(", ", missing.ToArray()));
            if (missing.Count > 0) yield break;

            var shortcut = Bind("HardA");
            Check(shortcut.Source == BindSource.ModTyped && shortcut.Modifiers == ModifierBehavior.Strict && shortcut.Editable,
                "a KeyboardShortcut setting is typed, checks its modifiers exactly, and can be changed");
            var single = Bind("SoftKey");
            Check(single.Source == BindSource.ModTyped && single.Modifiers == ModifierBehavior.SingleKey && single.Editable,
                "a KeyCode setting is typed, one key with no modifier, and can be changed");
            var text = Bind("TextKey");
            Check(text.Source == BindSource.ModText && text.Modifiers == ModifierBehavior.Unknown && !text.Editable && !string.IsNullOrEmpty(text.ReadOnlyReason),
                "a hotkey kept as text is shown, read only, with the reason", text.ReadOnlyReason);

            var map = KnownSituations.For(Bind("OnMap"), out var source);
            Check(source == SituationSource.Mod && map.Count == 1 && map.Contains("Map"), "a mod's own tag says where its bind is live", string.Join(", ", map.ToArray()));

            var others = all.Count(b => !b.Id.StartsWith(TestBinds.Id(""), StringComparison.Ordinal) && b.Combo.IsBound &&
                                        (b.Combo.Main == KeyCode.F13 || b.Combo.Main == KeyCode.F14 || b.Combo.Main == KeyCode.F15));
            Note("binds of others on F13 to F15", Count(others));
        }

        private IEnumerator Clashes()
        {
            var hard = Pair("HardA", "HardB");
            Check(hard != null && hard.Severity == Severity.Hard && !hard.Confirmed,
                "the same key and modifier is a hard clash, not confirmed while neither says when it is live", hard?.ToString());

            var soft = Pair("SoftKey", "SoftCombo");
            Check(soft != null && soft.Severity == Severity.Soft, "a single key fires under a combo on the same key: a soft clash", soft?.Reason);

            var note = Pair("NoteA", "NoteB");
            Check(note != null && note.Severity == Severity.Note, "the same key with modifiers both check exactly is a note", note?.Reason);

            var apart = Pair("OnMap", "InBuildMenu");
            Check(apart != null && apart.Severity == Severity.Note && apart.Reason.IndexOf("never live together", StringComparison.Ordinal) >= 0,
                "two binds live in places that never meet are ruled out", apart?.Reason);

            var both = Pair("MapC", "MapD");
            Check(both != null && both.Severity == Severity.Hard && both.Confirmed && both.Reason.IndexOf("Map", StringComparison.Ordinal) >= 0,
                "two binds both live on the map are a confirmed clash, named for where they meet", both?.Reason);

            var axes = Pair("PlaceX", "ItemY");
            Check(axes != null && axes.Severity == Severity.Hard && !axes.Confirmed && axes.Reason.IndexOf("in hand", StringComparison.Ordinal) >= 0,
                "a place and an item in hand prove nothing either way, and the clash says what is missing", axes?.Reason);

            var text = Pair("TextKey", "NoteA");
            Check(text != null && text.Severity == Severity.Soft && text.Reason.IndexOf("stored as text", StringComparison.Ordinal) >= 0,
                "a hotkey kept as text may ignore modifiers, so it is a soft clash with a combo on its key", text?.Reason);

            Check(Pair("ModifierA", "ModifierB") == null, "a modifier on its own is never a clash");

            Check(ConflictIndex.Worst(TestBinds.Id("HardA")) == Severity.Hard, "a bind's worst clash is the one the list marks it with");
            yield break;
        }

        private IEnumerator SituationsYouSet()
        {
            var before = Pair("YouA", "YouB");
            Check(before != null && before.Severity == Severity.Hard, "the pair starts as a hard clash", before?.ToString());

            var a = TestBinds.Id("YouA");
            var b = TestBinds.Id("YouB");
            try
            {
                Check(SituationStore.Toggle(a, "Map") == null && SituationStore.Toggle(b, "Inventory") == null, "situations set in the panel are saved");
                yield return Reopen();

                var after = Pair("YouA", "YouB");
                KnownSituations.For(Bind("YouA"), out var source);
                Check(source == SituationSource.You, "a situation you set is yours, read back from situations.txt");
                Check(after != null && after.Severity == Severity.Note && after.Reason.IndexOf("never live together", StringComparison.Ordinal) >= 0,
                    "the map and the inventory never meet, so the clash is ruled out", after?.Reason);
            }
            finally
            {
                if (SituationStore.For(a).Contains("Map")) SituationStore.Toggle(a, "Map");
                if (SituationStore.For(b).Contains("Inventory")) SituationStore.Toggle(b, "Inventory");
            }

            yield return Reopen();
            var back = Pair("YouA", "YouB");
            Check(back != null && back.Severity == Severity.Hard, "taken off again, the clash is back as it was", back?.ToString());
        }

        private IEnumerator Muting()
        {
            var hard = Pair("HardA", "HardB");
            if (hard == null)
            {
                Skip("muting a clash", "the hard pair was not found");
                yield break;
            }

            var id = TestBinds.Id("HardA");
            try
            {
                MuteStore.Toggle(hard);
                Check(MuteStore.IsMuted(hard), "a muted clash reads as muted");
                Check(!ConflictIndex.Live(id).Any(c => c.PairKey == hard.PairKey), "a muted clash is left out of the bind's live clashes");

                yield return Reopen();
                var again = Pair("HardA", "HardB");
                Check(again != null && MuteStore.IsMuted(again), "the mute is kept, and holds once the clashes are judged afresh");
            }
            finally
            {
                var now = Pair("HardA", "HardB");
                if (now != null && MuteStore.IsMuted(now)) MuteStore.Toggle(now);
            }

            var after = Pair("HardA", "HardB");
            Check(after != null && !MuteStore.IsMuted(after), "unmuted, the clash is live again");

            // A mute is about the clash waved through: one that gets worse is told again.
            var note = Pair("NoteA", "NoteB");
            var noteB = Bind("NoteB");
            if (note == null || noteB == null)
            {
                Skip("a muted clash that gets worse", "the note pair was not found");
                yield break;
            }

            var keyBefore = noteB.Combo;
            try
            {
                MuteStore.Toggle(note);
                Check(BindWriter.Apply(noteB, Bind("NoteA").Combo, Personal.SaveTarget.Unrecorded) == null, "the note pair is put on one combo");
                yield return Reopen();

                var worse = Pair("NoteA", "NoteB");
                Check(worse != null && worse.Severity == Severity.Hard && !MuteStore.IsMuted(worse),
                    "a muted note that becomes a hard clash is told again", worse?.ToString());
            }
            finally
            {
                var b = Bind("NoteB");
                if (b != null) BindWriter.Apply(b, keyBefore, Personal.SaveTarget.Unrecorded);

                // Judged afresh, as the panel would, so the mute is looked for on the note it was set on.
                ConflictIndex.Rebuild(BindRegistry.All.Where(x => !x.Internal && x.Compared));
                var now = Pair("NoteA", "NoteB");
                if (now != null && MuteStore.IsMuted(now)) MuteStore.Toggle(now);
            }
            yield return Reopen();
        }

        private IEnumerator FreeKeySuggestions()
        {
            var yours = Bind("Yours");
            var tried = Bind("HardA")?.Combo;
            if (yours == null || tried == null)
            {
                Skip("suggesting free keys", "the test settings were not found");
                yield break;
            }

            var clock = Stopwatch.StartNew();
            var offered = FreeKeys.For(yours, tried.Value, 3);
            Note("finding free keys", Ms(clock.Elapsed) + " ms for " + Count(offered.Count));

            Check(offered.Count >= 1 && offered.Count <= 3, "up to three keys are offered for a key that clashes",
                string.Join(", ", offered.Select(k => k.Combo.ToString()).ToArray()));

            foreach (var key in offered)
            {
                var clashes = ConflictEngine.Preview(yours, key.Combo, BindRegistry.All);
                var worst = clashes.Count == 0 ? (Severity?)null : clashes.Min(c => c.Severity);
                Check(worst == null || worst == Severity.Note, "an offered key runs into nothing worse than a note: " + key.Combo,
                    worst == null ? "free" : "shares a key, " + key.Why);
                Check(BindWriter.Refusal(yours, key.Combo) == null, "an offered key is one the bind can take: " + key.Combo);
            }
            yield break;
        }
    }
}
