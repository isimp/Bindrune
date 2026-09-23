using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Bindrune.UI
{
    /// <summary>
    /// The key printed on each slot of the game's hotbar.
    ///
    /// The game prints the slot's number there, which is its key only while the key is the
    /// digit. A slot whose digit was moved is labelled with what reaches it now, and every other
    /// slot keeps the game's own number, so nothing changes for anyone who has not moved a key.
    ///
    /// Only the game's own bar is touched. Mods such as ExtraSlots and BetterArchery add bars of
    /// their own built from the same component, whose slots are not the hotbar's and whose labels
    /// are theirs, so the one labelled here is found the way those mods find it: the HotKeyBar
    /// child of the HUD.
    ///
    /// The game writes these labels when it builds the bar, which it does again on respawn, and
    /// rewrites them when you switch between keyboard and gamepad. This runs after each bar's
    /// update, which is every frame, and puts its labels back when either has happened. It can
    /// tell from a few reference checks, so a frame with nothing to do costs next to nothing.
    /// </summary>
    internal static class HotbarLabels
    {
        private static FieldInfo _elementsField;
        private static FieldInfo _goField;

        private static Hud _hud;
        private static HotkeyBar _vanilla;
        private static IList _elements;

        private static object _first;
        private static int _count;
        private static int _version = -1;
        private static bool _gamepad;

        private static readonly List<TMP_Text> Labels = new List<TMP_Text>();
        private static readonly List<string> Texts = new List<string>();

        /// <summary>How each label looked before it was fitted to a longer key, to put back later.</summary>
        private static readonly Dictionary<TMP_Text, Look> Originals = new Dictionary<TMP_Text, Look>();

        private static bool _failed;

        private struct Look
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
            public bool AutoSize;
            public float Min;
            public float Max;
            public float Size;
            public TextWrappingModes Wrapping;
            public TextOverflowModes Overflow;
            public Vector4 Margin;
            public TextAlignmentOptions Alignment;
        }

        public static void Patch(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(HotkeyBar), "UpdateIcons");
                _elementsField = AccessTools.Field(typeof(HotkeyBar), "m_elements");
                var element = AccessTools.Inner(typeof(HotkeyBar), "ElementData");
                _goField = element == null ? null : AccessTools.Field(element, "m_go");

                if (target == null || _elementsField == null || _goField == null)
                {
                    Plugin.WarnOnce("Bindrune: the hotbar's labels were not found, so a moved hotbar key " +
                                    "still shows its old number on the bar.");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(HotbarLabels), nameof(AfterUpdate))));
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not label the hotbar: {ex.Message}", ex);
            }
        }

        private static void AfterUpdate(HotkeyBar __instance)
        {
            // The game's numbers are right while nothing is moved and nothing of ours is on the bar.
            if (_failed || (!FixedKeys.BarChanged && Labels.Count == 0)) return;

            try
            {
                if (!ReferenceEquals(__instance, Vanilla())) return;

                if (_elements == null) _elements = _elementsField.GetValue(__instance) as IList;
                if (_elements == null || _elements.Count == 0) return;

                // A rebuilt bar has new labels in the game's own look, so there is nothing to put back.
                if (!ReferenceEquals(_elements[0], _first)) Originals.Clear();

                var gamepad = ZInput.IsGamepadActive();
                if (ReferenceEquals(_elements[0], _first) && _elements.Count == _count && gamepad == _gamepad &&
                    FixedKeys.Version == _version && Intact())
                    return;

                Draw(gamepad);
            }
            catch (Exception ex)
            {
                // This runs inside every bar's update every frame, so a failure stops it for good
                // rather than repeating, and the bar carries on with the game's own numbers.
                _failed = true;
                Plugin.WarnOnce($"Bindrune: stopped labelling the hotbar: {ex.Message}", ex);
            }
        }

        /// <summary>The game's own hotbar, looked up again only when the HUD is a new one.</summary>
        private static HotkeyBar Vanilla()
        {
            var hud = Hud.instance;
            if (ReferenceEquals(hud, _hud)) return _vanilla;

            _hud = hud;
            _elements = null;
            _first = null;

            var bar = hud == null || hud.m_rootObject == null ? null : hud.m_rootObject.transform.Find("HotKeyBar");
            _vanilla = bar == null ? null : bar.GetComponent<HotkeyBar>();
            return _vanilla;
        }

        private static void Draw(bool gamepad)
        {
            Labels.Clear();
            Texts.Clear();

            var zinput = ZInput.instance;
            for (var i = 0; i < _elements.Count; i++)
            {
                var go = _goField.GetValue(_elements[i]) as GameObject;
                var binding = go == null ? null : go.transform.Find("binding");
                var label = binding == null ? null : binding.GetComponent<TMP_Text>();

                // On a gamepad the game prints no keys on the bar, and neither does this.
                var text = gamepad ? "" : FixedKeys.SlotLabel(zinput, i);
                if (label != null)
                {
                    // The whole bar in one style while anything is moved, so a moved key and an
                    // untouched digit side by side do not look like two different things.
                    if (FixedKeys.BarChanged) Fit(label);
                    else PutBack(label);

                    if (label.text != text) label.text = text;
                }

                Labels.Add(label);
                Texts.Add(text);
            }

            _first = _elements[0];
            _count = _elements.Count;
            _gamepad = gamepad;
            _version = FixedKeys.Version;

            // Every slot reads the game's own number again, so there is nothing left to keep up.
            if (!FixedKeys.BarChanged)
            {
                Labels.Clear();
                Texts.Clear();
            }
        }

        /// <summary>
        /// Gives a label room for a key longer than a digit, laid out as ExtraSlots lays out the
        /// keys on its own bars so the two read alike side by side: the whole slot to use, a
        /// small margin at the sides, the top-left corner, and a size between 10 and 14 that
        /// shrinks to fit. The game's own colour is kept.
        /// </summary>
        private static void Fit(TMP_Text label)
        {
            var rect = label.rectTransform;

            if (!Originals.ContainsKey(label))
            {
                Originals[label] = new Look
                {
                    AnchorMin = rect.anchorMin,
                    AnchorMax = rect.anchorMax,
                    OffsetMin = rect.offsetMin,
                    OffsetMax = rect.offsetMax,
                    AutoSize = label.enableAutoSizing,
                    Min = label.fontSizeMin,
                    Max = label.fontSizeMax,
                    Size = label.fontSize,
                    Wrapping = label.textWrappingMode,
                    Overflow = label.overflowMode,
                    Margin = label.margin,
                    Alignment = label.alignment
                };
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 14f;
            label.overflowMode = TextOverflowModes.Overflow;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.margin = new Vector4(3f, 0f, 3f, 0f);
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
            label.verticalAlignment = VerticalAlignmentOptions.Top;
        }

        private static void PutBack(TMP_Text label)
        {
            if (!Originals.TryGetValue(label, out var look)) return;

            var rect = label.rectTransform;
            rect.anchorMin = look.AnchorMin;
            rect.anchorMax = look.AnchorMax;
            rect.offsetMin = look.OffsetMin;
            rect.offsetMax = look.OffsetMax;

            label.enableAutoSizing = look.AutoSize;
            label.fontSizeMin = look.Min;
            label.fontSizeMax = look.Max;
            label.fontSize = look.Size;
            label.textWrappingMode = look.Wrapping;
            label.overflowMode = look.Overflow;
            label.margin = look.Margin;
            label.alignment = look.Alignment;
            Originals.Remove(label);
        }

        /// <summary>
        /// Whether the labels still read what was written. The switch between keyboard and
        /// gamepad rewrites them without rebuilding the bar, so this is the only way to see it.
        /// </summary>
        private static bool Intact()
        {
            for (var i = 0; i < Labels.Count; i++)
            {
                if (ReferenceEquals(Labels[i], null)) continue;
                if (!string.Equals(Labels[i].text, Texts[i], StringComparison.Ordinal)) return false;
            }

            return true;
        }
    }
}
