using System;
using System.Collections;
using System.Linq;
using Bindrune.Discovery;
using Bindrune.Hints;
using Bindrune.UI;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        /// <summary>Long enough for the list to catch up with a search, which waits a moment after the last key typed.</summary>
        private const float SearchSettles = 0.5f;

        private IEnumerator PanelSearch()
        {
            if (!BindrunePanel.IsOpen)
            {
                Skip("searching the panel", "the panel is not open");
                yield break;
            }

            var before = BindrunePanel.SearchText;
            try
            {
                BindrunePanel.SearchText = "";
                yield return Wait(SearchSettles);
                var everything = BindrunePanel.ShownIds.Count;
                Check(everything > 0, "with no search the list shows every bind", Count(everything));

                // A key's name searches keys alone, so mod names that contain it do not get in the way.
                BindrunePanel.SearchText = "F13";
                yield return Wait(SearchSettles);
                var onKey = BindrunePanel.ShownIds.Select(ById).Where(b => b != null).ToList();
                Check(onKey.Count > 0 && onKey.All(b => b.Combo.Main == KeyCode.F13 || b.Combo.Modifiers.Contains(KeyCode.F13)),
                    "searching for F13 lists only binds on F13", Count(onKey.Count));
                Check(TestBinds.Keys().Where(k => Bind(k)?.Combo.Main == KeyCode.F13).All(k => BindrunePanel.ShownIds.Contains(TestBinds.Id(k))),
                    "every test setting on F13 is among them");

                // Text that names no key searches what the rows show.
                BindrunePanel.SearchText = "Bindrune self-test";
                yield return Wait(SearchSettles);
                var mine = BindrunePanel.ShownIds.ToList();
                Check(mine.Count == TestBinds.Keys().Count() && mine.All(id => id.StartsWith(TestBinds.Id(""), StringComparison.Ordinal)),
                    "searching for a mod's name lists that mod's binds", Count(mine.Count) + " of " + Count(TestBinds.Keys().Count()));
            }
            finally
            {
                BindrunePanel.SearchText = before ?? "";
            }
            yield return Wait(SearchSettles);
        }

        private IEnumerator HintsOnScreen()
        {
            var id = TestBinds.Id("HintMe");
            if (Bind("HintMe") == null)
            {
                Skip("showing a hint", "the test setting was not found");
                yield break;
            }

            var wasOn = Plugin.HintsVisible;
            try
            {
                HintChoice.Toggle(id);
                Check(HintChoice.Shows(id), "a bind pinned to the hints is shown by them");
                if (!Plugin.HintsVisible) HintOverlay.Toggle();
                HintOverlay.Tick(force: true);
                yield return null;

                var shown = HintOverlay.Shown;
                Check(shown != null && shown.IndexOf("HintMe", StringComparison.Ordinal) >= 0, "the hints on screen show the pinned bind", shown?.Replace('\n', '|'));

                HintChoice.Toggle(id);
                HintOverlay.Tick(force: true);
                yield return null;
                shown = HintOverlay.Shown;
                Check(shown == null || shown.IndexOf("HintMe", StringComparison.Ordinal) < 0, "unpinned, it is gone from the hints");
            }
            finally
            {
                if (HintChoice.Shows(id)) HintChoice.Toggle(id);
                if (Plugin.HintsVisible != wasOn) HintOverlay.Toggle();
            }
        }

        private IEnumerator KeyNames()
        {
            Check(KeyLabels.Of(KeyCode.A) == "A", "a letter is named as the keyboard labels it", KeyLabels.Of(KeyCode.A));
            Check(KeyLabels.Of(KeyCode.F5) == "F5", "a function key keeps its name", KeyLabels.Of(KeyCode.F5));
            Check(KeyLabels.Of(KeyCode.LeftAlt) == "LeftAlt", "a modifier keeps its name", KeyLabels.Of(KeyCode.LeftAlt));

            // The key a German keyboard labels Y is stored as Z: shown as the game's own controls name it.
            var game = ZInput.KeyCodeToDisplayName(KeyCode.Z)?.Trim();
            var shown = KeyLabels.Of(KeyCode.Z);
            if (!Plugin.KeyboardLabels) Skip("keys named as your keyboard labels them", "KeyboardLayoutLabels is off");
            else if (string.IsNullOrEmpty(game) || game.Length != 1) Note("the key stored as Z", "the game names it \"" + game + "\", so Bindrune keeps the name Z");
            else
            {
                // A letter is shown in upper case, as Unity names its own letter keys.
                var expected = game.ToUpperInvariant();
                Check(shown == expected, "the key stored as Z is named as the game's own controls name it", shown + ", the game says " + game);
            }

            Note("this keyboard labels", "Z as " + shown + ", Y as " + KeyLabels.Of(KeyCode.Y) + ", Semicolon as " + KeyLabels.Of(KeyCode.Semicolon));
            Check(BindRegistry.All.Count > 0, "the binds are still listed after every part");
            yield break;
        }
    }
}
