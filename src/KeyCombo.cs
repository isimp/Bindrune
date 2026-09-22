using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bindrune
{
    /// <summary>A main key plus its modifier keys, comparable across every bind source.</summary>
    public readonly struct KeyCombo : IEquatable<KeyCombo>
    {
        public static readonly KeyCombo None = new KeyCombo(KeyCode.None, null, null);

        public readonly KeyCode Main;
        public readonly KeyCode[] Modifiers;

        /// <summary>Set when the main key could not be mapped to a KeyCode (unmapped vanilla input path).</summary>
        public readonly string RawPath;

        // Built once, here. Every clash check and equality test compares it, and building it means
        // formatting an enum, which is slow and allocates on the game's runtime - a suggestion
        // pass compares it tens of thousands of times.
        private readonly string _token;

        public KeyCombo(KeyCode main, IEnumerable<KeyCode> modifiers, string rawPath = null)
        {
            Main = main;
            // A key never modifies itself: "LeftAlt" as a main key must not also list LeftAlt.
            Modifiers = modifiers == null
                ? Array.Empty<KeyCode>()
                : modifiers.Where(k => IsModifier(k) && k != main).Distinct().OrderBy(k => (int)k).ToArray();
            RawPath = rawPath;
            _token = Token(main, rawPath);
        }

        public bool IsBound => Main != KeyCode.None || !string.IsNullOrEmpty(RawPath);

        /// <summary>What to call the main key in a heading: its name, or the raw path when it has none.</summary>
        public string MainLabel =>
            Main != KeyCode.None ? Main.ToString() : (string.IsNullOrEmpty(RawPath) ? "not bound" : RawPath);

        /// <summary>Identity used to group binds that fight over the same physical key.</summary>
        public string MainToken => _token ?? Token(Main, RawPath);

        // The fallback above is for a default(KeyCombo), which never ran the constructor.
        private static string Token(KeyCode main, string rawPath) =>
            main != KeyCode.None ? "key:" + main : (string.IsNullOrEmpty(rawPath) ? "none" : "path:" + rawPath.ToLowerInvariant());

        public static bool IsModifier(KeyCode k) =>
            k == KeyCode.LeftControl || k == KeyCode.RightControl ||
            k == KeyCode.LeftAlt || k == KeyCode.RightAlt ||
            k == KeyCode.LeftShift || k == KeyCode.RightShift ||
            k == KeyCode.LeftCommand || k == KeyCode.RightCommand;

        public bool SameModifiers(KeyCombo other) => Modifiers.SequenceEqual(other.Modifiers);

        public bool Equals(KeyCombo other) => MainToken == other.MainToken && SameModifiers(other);
        public override bool Equals(object obj) => obj is KeyCombo other && Equals(other);
        public override int GetHashCode() => MainToken.GetHashCode() ^ Modifiers.Length;

        /// <summary>
        /// The raw form, Unity's own key names. This is what is stored, logged and compared, so it
        /// must never change with the player's keyboard layout. For what to show, see KeyLabels.
        /// </summary>
        public override string ToString() => Format(k => k.ToString());

        /// <summary>
        /// The one place a combo is spelled out, so the raw form and the labelled form can never
        /// disagree about order or separators, only about what each key is called.
        /// </summary>
        /// <param name="path">What to call a main key known only by its path; the path itself when null.</param>
        internal string Format(Func<KeyCode, string> name, Func<string, string> path = null)
        {
            if (!IsBound) return "<unbound>";
            var main = Main != KeyCode.None ? name(Main) : path != null ? path(RawPath) : RawPath;
            return Modifiers.Length == 0 ? main : string.Join(" + ", Modifiers.Select(name).ToArray()) + " + " + main;
        }
    }
}
