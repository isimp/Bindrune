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
    ///
    /// A key with no KeyCode is the exception, in OfPath: it has no Unity name to fall back to,
    /// only its input path, so a label of any length is accepted there.
    /// </summary>
    public static class KeyLabels
    {
        private static readonly Dictionary<KeyCode, string> Cache = new Dictionary<KeyCode, string>();

        private static readonly Dictionary<string, string> PathCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Drops what was worked out, so a changed keyboard layout shows on the next scan.</summary>
        public static void Forget()
        {
            Cache.Clear();
            PathCache.Clear();
        }

        /// <summary>A combo spelled with labels, in exactly the shape KeyCombo.ToString uses.</summary>
        public static string Of(KeyCombo combo) => Plugin.KeyboardLabels ? combo.Format(Of, OfPath) : combo.ToString();

        public static string Of(KeyCode key)
        {
            if (!Plugin.KeyboardLabels) return key.ToString();
            if (Cache.TryGetValue(key, out var known)) return known;

            var label = Resolve(key.ToString(), () => ZInput.KeyCodeToDisplayName(key), false, out var settled);

            // Before a keyboard exists every key would resolve to its plain name. Remembering
            // that would keep the wrong answer until the next scan, so only settled answers stay.
            if (settled) Cache[key] = label;
            return label;
        }

        /// <summary>
        /// The label for a key the game knows only by its input path: one Unity's older input has
        /// no KeyCode for, such as the key beside left Shift on ISO keyboards. A label of any length
        /// is accepted, since the fallback is the path itself rather than a Unity key name.
        /// </summary>
        public static string OfPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Plugin.KeyboardLabels) return path;
            if (PathCache.TryGetValue(path, out var known)) return known;

            var label = Resolve(path, () => InputSystem.FindControl(path)?.displayName, true, out var settled);
            if (settled) PathCache[path] = label;
            return label;
        }

        /// <summary>The heading for a group of binds on one key.</summary>
        public static string Heading(KeyCombo combo) =>
            combo.Main != KeyCode.None ? Of(combo.Main)
            : string.IsNullOrEmpty(combo.RawPath) ? combo.MainLabel
            : OfPath(combo.RawPath);

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

        /// <summary>The same question for a key known only by its path, whose longer name is the path.</summary>
        public static bool Answers(string path, string query)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(query)) return false;
            if (string.Equals(OfPath(path), query, StringComparison.OrdinalIgnoreCase)) return true;
            return query.Length > 1 && string.Equals(path, query, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="anyLength">
        /// Whether a label longer than one character is accepted, which it is only for a key whose
        /// fallback is an input path.
        /// </param>
        private static string Resolve(string name, Func<string> displayName, bool anyLength, out bool settled)
        {
            settled = false;

            try
            {
                if (Keyboard.current == null) return name;
                settled = true;

                var shown = displayName()?.Trim();
                if (string.IsNullOrEmpty(shown)) return name;

                foreach (var c in shown)
                    if (char.IsControl(c)) return name;

                // Upper case, as Unity's own letter names are, so a relabelled key sits among the
                // unchanged ones without looking like a different kind of thing.
                if (shown.Length == 1)
                    return char.IsWhiteSpace(shown[0]) ? name : char.ToUpperInvariant(shown[0]).ToString();

                return anyLength ? shown : name;
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: no label for {name}, keeping its name: {ex.Message}");
                settled = true;
                return name;
            }
        }
    }
}
