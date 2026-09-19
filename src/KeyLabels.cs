using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bindrune
{
    /// <summary>
    /// What to call a key on screen: the label printed on it under the player's keyboard layout,
    /// rather than Unity's name for it.
    ///
    /// Unity names keys by their position on a US keyboard, so on a German one the key labelled
    /// Y is "Z" and the one labelled Ö is "Semicolon". The binds are right; only the names are
    /// foreign. The label comes from ZInput.KeyCodeToDisplayName, the same Input System display
    /// name Valheim's own Controls screen shows, rather than from a table of our own.
    ///
    /// Display only, by construction. KeyCombo and everything that stores, compares, captures or
    /// writes a key keeps Unity's names; only the places that draw text for a person call in
    /// here. A label is accepted only when it is a single visible character, which covers the
    /// letters, digits and punctuation that move between layouts. Anything else, including any
    /// failure, falls back to Unity's name, so the worst this can do is change nothing.
    /// </summary>
    public static class KeyLabels
    {
        private static readonly Dictionary<KeyCode, string> Cache = new Dictionary<KeyCode, string>();

        /// <summary>Drops what was worked out, so a changed keyboard layout shows on the next scan.</summary>
        public static void Forget() => Cache.Clear();

        /// <summary>A combo spelled with labels, in exactly the shape KeyCombo.ToString uses.</summary>
        public static string Of(KeyCombo combo) => Plugin.KeyboardLabels ? combo.Format(Of) : combo.ToString();

        public static string Of(KeyCode key)
        {
            if (!Plugin.KeyboardLabels) return key.ToString();
            if (Cache.TryGetValue(key, out var known)) return known;

            var label = Resolve(key, out var settled);

            // Before a keyboard exists every key would resolve to its plain name. Remembering
            // that would keep the wrong answer until the next scan, so only settled answers stay.
            if (settled) Cache[key] = label;
            return label;
        }

        /// <summary>The heading for a group of binds on one key.</summary>
        public static string Heading(KeyCombo combo) =>
            combo.Main != KeyCode.None ? Of(combo.Main) : combo.MainLabel;

        /// <summary>
        /// Whether a key answers to what someone typed or pressed into the search box. A single
        /// character is a label and nothing else: on a German keyboard "z" means the key marked Z,
        /// and letting it also mean Unity's KeyCode.Z, which is the key marked Y, would find two
        /// different keys for one letter. Anything longer is a name ("LeftAlt", "F5", "Semicolon"),
        /// which no single-character label can be confused with.
        /// </summary>
        public static bool Answers(KeyCode key, string query)
        {
            if (key == KeyCode.None || string.IsNullOrEmpty(query)) return false;
            if (string.Equals(Of(key), query, StringComparison.OrdinalIgnoreCase)) return true;
            return query.Length > 1 && string.Equals(key.ToString(), query, StringComparison.OrdinalIgnoreCase);
        }

        private static string Resolve(KeyCode key, out bool settled)
        {
            var name = key.ToString();
            settled = false;

            try
            {
                if (Keyboard.current == null) return name;
                settled = true;

                var shown = ZInput.KeyCodeToDisplayName(key)?.Trim();
                if (string.IsNullOrEmpty(shown) || shown.Length != 1) return name;

                var c = shown[0];
                if (char.IsWhiteSpace(c) || char.IsControl(c)) return name;

                // Upper case, as Unity's own letter names are, so a relabelled key sits among the
                // unchanged ones without looking like a different kind of thing.
                return char.ToUpperInvariant(c).ToString();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Bindrune: no label for {name}, keeping its name: {ex.Message}");
                settled = true;
                return name;
            }
        }
    }
}
