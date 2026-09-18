using System.Linq;
using Bindrune.Conflicts;
using Bindrune.Discovery;
using Bindrune.Hints;
using Bindrune.Personal;
using Jotunn.Managers;
using UnityEngine;

namespace Bindrune.UI
{
    /// <summary>
    /// The two pages the right-hand column shows when it is not showing a bind: the help page
    /// behind "?", which explains the marks and what Bindrune keeps for you, and the on-screen
    /// hints page behind "Hints", which is that whole feature's settings and its list.
    /// </summary>
    public static partial class BindrunePanel
    {
        /// <summary>
        /// What was put back since you started, under a heading of its own so it reads as part of
        /// "your keys" rather than as a stray line after whatever came before it.
        ///
        /// There is nothing to dismiss: the record is session-only, so starting the game clears
        /// it.
        /// </summary>
        private static void LegendRestored(float width)
        {
            if (PersonalKeys.RestoredCount == 0) return;

            Wrapped("Keys put back", _detail, width, 17, new Color(0.6f, 0.9f, 0.6f), true);
            Spacer(6f);
            Wrapped($"Something rewrote {PersonalKeys.RestoredCount} of your keys since you started the game - a " +
                    "profile sync, or the mod itself - and Bindrune put yours back in. Nothing to do; this clears " +
                    "itself next time you start the game.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(6f);

            foreach (var line in PersonalKeys.RestoredKeys.Take(8))
                Wrapped(line, _detail, width, 11, new Color(1f, 1f, 1f, 0.55f));

            if (PersonalKeys.RestoredCount > 8)
                Wrapped($"...and {PersonalKeys.RestoredCount - 8} more.", _detail, width, 11, new Color(1f, 1f, 1f, 0.45f));

            Spacer(14f);
        }

        private static void ShowLegend()
        {
            var width = DetailWidth - 40f;

            Wrapped("What the marks mean", _detail, width, 20, GUIManager.Instance.ValheimOrange, true);
            Spacer(10f);

            Wrapped("!!   hard", _detail, width, 16, ColorFor(Severity.Hard), true);
            Wrapped("Both binds act on exactly the same key and modifiers. One press triggers both, every time.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(12f);

            Wrapped("!   soft", _detail, width, 16, ColorFor(Severity.Soft), true);
            Wrapped("One side is a single key with no modifiers to match, so it fires anyway when you press the " +
                    "other one's combination: Alt+E for a mod still triggers a plain E bind.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(12f);

            Wrapped("-   note", _detail, width, 16, ColorFor(Severity.Note), true);
            Wrapped("Worth knowing, but they should not interfere: either both sides check modifiers exactly and those " +
                    "modifiers differ, or they are two binds the game itself ships on one key.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(14f);

            Wrapped("confirmed", _detail, width, 16, Color.white, true);
            Wrapped("Added to a mark when both binds are known to be live in the same situation - the same place, or " +
                    "the same thing in your hands. Without that, a clash is only what the two keys say. Filling in " +
                    "\"Applies when\" on a bind is what lets Bindrune rule clashes out, or confirm them.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(14f);

            Spacer(6f);
            Wrapped("Your keys", _detail, width, 17, GUIManager.Instance.ValheimOrange, true);
            Spacer(6f);
            Wrapped("A * beside a key means you set it and Bindrune keeps it for you. Mod keybinds live in files that a " +
                    "profile sync overwrites, so yours are stored separately and put back afterwards. Each bind can be " +
                    "switched between just for you and the whole profile.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(14f);

            LegendRestored(width);
            LegendOrphans(width);

            Wrapped("Nothing is ever blocked: Bindrune only tells you what will happen, so you can keep a clash if you want it. " +
                    "Mute one you have decided is fine and it stops counting. Alt, Ctrl and Shift on their own are never " +
                    "reported, because mods share them on purpose to qualify clicks and other keys.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));
            Spacer(10f);

            // Said plainly rather than left for someone to discover: a list that looks complete
            // and is not is worse than one that admits where it ends.
            Wrapped("What is not here: a mod that writes its key into its own code instead of a setting has nothing " +
                    "for Bindrune to read, so it does not appear in this list and cannot be part of a conflict.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));
            Spacer(10f);

            Wrapped("Gamepad buttons are listed only while a controller is plugged in, and then to be read rather " +
                    "than changed: Valheim has no per-button gamepad rebinding at all, only a choice of whole layouts " +
                    "under Settings > Gamepad. They sit on another device, so they are left out of conflicts.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));
            Spacer(16f);

            Wrapped("Select a bind on the left to see its own conflicts and change its key.",
                _detail, width, 14, new Color(1f, 1f, 1f, 0.75f));

            LegendAdvanced(width);
        }

        /// <summary>
        /// The on-screen hints, on a page of their own: the feature's settings and its list.
        /// </summary>
        private static void ShowHintsPage()
        {
            var width = DetailWidth - 40f;

            Wrapped("On-screen hints", _detail, width, 20, GUIManager.Instance.ValheimOrange, true);
            Spacer(10f);

            Wrapped($"{Plugin.HintsKeyText} shows and hides a list of keys on screen, and it stays as you left it. " +
                    "Binds go on it one at a time, from \"On screen\" on any bind, and each one only appears while its " +
                    "situations say it applies - the right item in hand, the map open, whatever you marked.",
                _detail, width, 13, new Color(1f, 1f, 1f, 0.85f));
            Spacer(6f);
            Wrapped("While this panel is open the list turns brown and can be dragged anywhere you like; it stays " +
                    "where you leave it. The rest of the time it ignores the mouse entirely, so it can never swallow " +
                    "a click meant for the game.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.6f));
            Spacer(8f);

            // The toggle key works from here too, but a button is what you find without knowing
            // the key - and you need them on screen before you can drag them anywhere.
            var showNow = FixedButton(Plugin.HintsVisible ? "* Hints are on" : "Turn hints on", _detail, width, 30f, () =>
            {
                HintOverlay.Toggle();
                Refresh(rescan: false);
            });
            TintButton(showNow, Plugin.HintsVisible);
            Spacer(6f);

            // Without the mod's name a hint is just a key and a word, and two mods can easily
            // name an action the same thing. With it, the list is wider - hence the choice.
            var names = FixedButton(Plugin.HintModNames ? "* Showing mod names" : "Show mod names", _detail, width, 28f, () =>
            {
                Plugin.HintModNames = !Plugin.HintModNames;
                HintOverlay.Invalidate();
                Refresh(rescan: false);
            });
            TintButton(names, Plugin.HintModNames);
            Spacer(6f);

            var order = HorizontalRow(_detail, 32f);
            var halfWidth = (width - 12f) / 2f;

            foreach (HintOrder which in System.Enum.GetValues(typeof(HintOrder)))
            {
                var chosen = Plugin.HintOrder == which;
                var label = which == HintOrder.KeyFirst ? "Key first" : "Action first";
                var button = FixedButton(chosen ? "* " + label : label, order, halfWidth, 28f, () =>
                {
                    Plugin.HintOrder = which;
                    HintOverlay.Invalidate();
                    Refresh(rescan: false);
                });
                TintButton(button, chosen);
            }

            Spacer(8f);

            // Both are worth setting while looking at the box rather than from a config screen:
            // which way it should grow depends entirely on where you just dragged it.
            var arrange = HorizontalRow(_detail, 32f);
            var third = (width - 16f) / 3f;

            foreach (HintAlign align in System.Enum.GetValues(typeof(HintAlign)))
            {
                var chosen = Plugin.HintAlign == align;
                var button = FixedButton(chosen ? "* " + align : align.ToString(), arrange, third, 28f, () =>
                {
                    Plugin.HintAlign = align;
                    HintOverlay.Invalidate();
                    Refresh(rescan: false);
                });
                TintButton(button, chosen);
            }

            Spacer(6f);

            var growth = HorizontalRow(_detail, 32f);
            var half = (width - 12f) / 2f;

            foreach (HintGrowth direction in System.Enum.GetValues(typeof(HintGrowth)))
            {
                var chosen = Plugin.HintGrowth == direction;
                var label = direction == HintGrowth.Up ? "Grows up" : "Grows down";
                var button = FixedButton(chosen ? "* " + label : label, growth, half, 28f, () =>
                {
                    Plugin.HintGrowth = direction;
                    HintOverlay.Invalidate();
                    Refresh(rescan: false);
                });
                TintButton(button, chosen);
            }

            Wrapped("Alignment is where the text sits in the box. Growth is which edge stays put as hints are added: " +
                    "up for a box near the bottom of the screen, down for one near the top.",
                _detail, width, 12, new Color(1f, 1f, 1f, 0.55f));
            Spacer(10f);

            var hinted = BindRegistry.All.Where(b => HintChoice.Shows(b.Id)).ToList();
            if (hinted.Count == 0)
            {
                Wrapped("Nothing is set to show yet.", _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));
            }
            else
            {
                foreach (var bind in hinted.Take(10))
                {
                    var row = HorizontalRow(_detail, 26f);
                    FixedLabel($"{bind.Combo}   {bind.OwnerName} / {bind.Label}", row, width - 106f, 22f, 12,
                        new Color(1f, 1f, 1f, 0.7f));

                    FixedButton("Remove", row, 96f, 24f, () =>
                    {
                        HintChoice.Toggle(bind.Id);
                        Refresh(rescan: false);
                    });
                }

                if (hinted.Count > 10)
                    Wrapped($"...and {hinted.Count - 10} more.", _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));
            }

            Spacer(14f);
        }

        /// <summary>
        /// Keys belonging to binds nothing provides any more: a mod uninstalled, switched off, or
        /// one that renamed a setting. Better said out loud than silently kept forever.
        /// </summary>
        private static void LegendOrphans(float width)
        {
            var orphans = PersonalKeys.Orphans().ToList();
            if (orphans.Count > 0)
            {
                Wrapped($"{orphans.Count} of your keys belong to binds no mod provides right now", _detail, width, 15, new Color(1f, 0.8f, 0.4f), true);
                Wrapped("Their mods may be uninstalled or switched off, in which case leaving them means your key " +
                        "comes back if you reinstall. If a mod renamed the setting instead, the old entry can be forgotten.",
                    _detail, width, 12, new Color(1f, 1f, 1f, 0.5f));

                foreach (var id in orphans.Take(6))
                {
                    var row = HorizontalRow(_detail, 26f);
                    FixedLabel(id, row, width - 106f, 22f, 11, new Color(1f, 1f, 1f, 0.55f));

                    FixedButton("Forget", row, 96f, 24f, () =>
                    {
                        PersonalKeys.Forget(id);
                        Refresh(rescan: false);
                    });
                }

                Spacer(14f);
            }
        }

        /// <summary>Last, since it is a debugging switch.</summary>
        private static void LegendAdvanced(float width)
        {
            Spacer(24f);
            Wrapped("Advanced", _detail, width, 13, new Color(1f, 1f, 1f, 0.4f), true);
            Spacer(4f);

            var internalButton = FixedButton(_internalShown ? "Showing internal game binds" : "Show internal game binds",
                _detail, width, 28f, () =>
                {
                    _internalShown = !_internalShown;
                    Refresh(rescan: false);
                });
            TintButton(internalButton, _internalShown);

            Wrapped("Raw key aliases the game reads internally, like LShift and MouseLeft. They are not controls you " +
                    "can change, and they are left out of conflicts.",
                _detail, width, 11, new Color(1f, 1f, 1f, 0.4f));
        }
    }
}
