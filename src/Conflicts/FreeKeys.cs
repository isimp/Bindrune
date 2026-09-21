using System.Collections.Generic;
using System.Linq;
using Bindrune.Discovery;
using UnityEngine;

namespace Bindrune.Conflicts
{
    /// <summary>A key worth trying instead, why it was picked, and what it would still share.</summary>
    public class FreeKey
    {
        public KeyCombo Combo;
        public string Why;

        /// <summary>Binds on the same key that should not interfere. Notes only, never worse.</summary>
        public List<Conflict> Shared = new List<Conflict>();
    }

    /// <summary>
    /// Keys to offer when the one you pressed clashes, the closest to it first.
    ///
    /// Free means nothing it would run into is worse than a note: a bind that is never live at
    /// the same time, or one whose modifiers keep the two apart. Those are the keys it takes
    /// knowing when each bind applies to find, so they are offered, and marked as shared.
    ///
    /// Only as far as Bindrune can see: a mod that reads a key in its own code, with no setting
    /// behind it, cannot be checked.
    /// </summary>
    public static class FreeKeys
    {
        /// <summary>
        /// Keys the game reads straight from the keyboard, with no bind behind them, for anyone in
        /// the world: taken whatever the bind list says. Found in Valheim 1.0.7's code. Keys it
        /// reads only in debug mode, in menus or inside a dialog are left out.
        /// </summary>
        private static readonly GameKey[] ReadByGame =
        {
            new GameKey(KeyCode.F2, null, "the network panel"),
            new GameKey(KeyCode.F9, null, "switching gamepad layout"),
            new GameKey(KeyCode.F11, null, "screenshots"),
            new GameKey(KeyCode.F1, KeyCode.LeftControl, "capturing the mouse"),
            new GameKey(KeyCode.F3, KeyCode.LeftControl, "hiding the HUD")
        };

        private class GameKey
        {
            public readonly KeyCombo Combo;
            public readonly string What;

            public GameKey(KeyCode key, KeyCode? modifier, string what)
            {
                Combo = new KeyCombo(key, modifier.HasValue ? new[] { modifier.Value } : null);
                What = what;
            }
        }

        /// <summary>The modifiers offered on the key you pressed, in the order they are tried.</summary>
        private static readonly KeyCode[] Offered = { KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.LeftShift };

        public static List<FreeKey> For(BindEntry bind, KeyCombo tried, int limit)
        {
            var found = new List<FreeKey>();
            if (tried.Main == KeyCode.None || KeyCombo.IsModifier(tried.Main) || tried.Main >= KeyCode.JoystickButton0)
                return found;

            // A bind that stores one key drops any modifier when written, so none is offered and
            // the one held while pressing is not carried over to the neighbours either.
            var strict = bind.Modifiers == ModifierBehavior.Strict;
            var held = strict ? tried.Modifiers : new KeyCode[0];
            var name = KeyLabels.Of(tried.Main);

            // Only the first of these goes ahead of the neighbours: a short list that is all
            // "G with something" would leave out every other key.
            var sameKey = strict ? SameKey(tried).Where(o => Usable(bind, o)).ToList() : new List<FreeKey>();
            if (sameKey.Count > 0) found.Add(sameKey[0]);

            foreach (var key in KeyGrid.Around(tried.Main))
            {
                if (found.Count >= limit) return found;

                var option = new FreeKey { Combo = new KeyCombo(key, held), Why = "near " + name };
                if (Usable(bind, option)) found.Add(option);
            }

            found.AddRange(sameKey.Skip(1).Take(limit - found.Count));
            return found;
        }

        /// <summary>The key you pressed with a modifier added, swapped or taken away.</summary>
        private static IEnumerable<FreeKey> SameKey(KeyCombo tried)
        {
            if (tried.Modifiers.Length > 0)
                yield return new FreeKey { Combo = new KeyCombo(tried.Main, null), Why = "same key, on its own" };

            foreach (var modifier in Offered)
            {
                var combo = new KeyCombo(tried.Main, new[] { modifier });
                if (!combo.Equals(tried)) yield return new FreeKey { Combo = combo, Why = "same key, with " + Short(modifier) };
            }
        }

        /// <summary>
        /// Whether nothing Bindrune can see uses this key: no bind at all, not even one that
        /// would never interfere, and no read by the game outside its binds.
        /// </summary>
        public static bool Unused(BindEntry bind, KeyCombo combo) =>
            GameUse(bind, combo) == null && ConflictEngine.Preview(bind, combo, BindRegistry.All).Count == 0;

        /// <summary>
        /// What the game does with this key without any bind behind it, or null when it does not
        /// read it that way. The clash check leaves these out, because they are not binds, so
        /// this is the only thing standing between them and a key that looks free.
        /// </summary>
        public static string GameUse(BindEntry bind, KeyCombo combo)
        {
            // A single key fires whatever modifiers are held, so it meets the game's read on any
            // combination; an exact shortcut only does when it holds the modifier the game waits for.
            var read = ReadByGame.FirstOrDefault(g => g.Combo.Main == combo.Main &&
                (bind.Modifiers != ModifierBehavior.Strict || g.Combo.Modifiers.All(combo.Modifiers.Contains)));
            if (read != null) return read.What;

            // The game's own plumbing: raw keys its UI reads, left out of clashes because they
            // mirror a real control, but still keys the game is listening to.
            var plumbing = BindRegistry.All.FirstOrDefault(b => b.Internal && b.Combo.MainToken == combo.MainToken);
            return plumbing != null ? $"its own \"{plumbing.Label}\" input" : null;
        }

        private static bool Usable(BindEntry bind, FreeKey option)
        {
            var combo = option.Combo;

            // What it already has is not an alternative.
            if (combo.Equals(bind.Combo)) return false;

            // The game can only be given a key it has an input path for.
            if (bind.Source == BindSource.Vanilla && KeyPaths.ToPath(combo.Main) == null) return false;

            if (GameUse(bind, combo) != null) return false;

            var clashes = ConflictEngine.Preview(bind, combo, BindRegistry.All);
            if (clashes.Any(c => c.Severity != Severity.Note)) return false;

            option.Shared = clashes;
            return true;
        }

        private static string Short(KeyCode modifier) =>
            modifier == KeyCode.LeftAlt ? "Alt" : modifier == KeyCode.LeftControl ? "Ctrl" : "Shift";
    }
}
