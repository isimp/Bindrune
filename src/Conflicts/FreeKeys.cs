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
    /// Keys to offer when the one you pressed clashes: the nearest keys on the keyboard, with a
    /// modifier where the bind can hold one and this setup already uses it. See Ranked.
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

        /// <summary>
        /// A candidate this costly is no longer near what was pressed, and suggesting it is no
        /// help: better to offer nothing than a key across the keyboard.
        /// </summary>
        private const float TooFar = 7.5f;

        /// <summary>The modifiers a suggestion can add, in the order the game makes them sensible.</summary>
        private static readonly KeyCode[] Offered = { KeyCode.LeftAlt, KeyCode.LeftControl, KeyCode.LeftShift };

        public static List<FreeKey> For(BindEntry bind, KeyCombo tried, int limit)
        {
            var found = new List<FreeKey>();
            if (tried.Main == KeyCode.None || KeyCombo.IsModifier(tried.Main) || tried.Main >= KeyCode.JoystickButton0)
                return found;

            foreach (var option in Ranked(bind, tried))
            {
                if (found.Count >= limit) break;
                if (Usable(bind, option)) found.Add(option);
            }

            return found;
        }

        /// <summary>
        /// Every key worth trying, cheapest first. A candidate costs how far its key is from the one
        /// pressed, plus what its modifier costs: nothing for none or for the one that was held, and
        /// for any other, one key width per place it ranks in what this setup already uses. So a
        /// modifier on a nearby key can beat a plain key halfway across the keyboard, and a
        /// modifier nobody here uses is never offered.
        /// </summary>
        private static IEnumerable<FreeKey> Ranked(BindEntry bind, KeyCombo tried)
        {
            // A bind that stores one key drops any modifier when written, so it is offered none,
            // and one held while pressing is not carried over to its neighbours either.
            var strict = bind.Modifiers == ModifierBehavior.Strict;
            var held = strict ? tried.Modifiers : new KeyCode[0];

            var choices = new List<ModifierChoice> { new ModifierChoice(held, 0f) };
            if (strict)
            {
                if (held.Length > 0) choices.Add(new ModifierChoice(new KeyCode[0], 0f));

                var rank = 1;
                foreach (var modifier in Preferred().Where(m => !held.Contains(m)))
                    choices.Add(new ModifierChoice(new[] { modifier }, rank++));
            }

            var name = KeyLabels.Of(tried.Main);

            return new[] { tried.Main }.Concat(KeyGrid.Around(tried.Main))
                .SelectMany(key => choices.Select(choice => new
                {
                    Combo = new KeyCombo(key, choice.Modifiers),
                    Distance = KeyGrid.Distance(tried.Main, key) ?? 0f,
                    choice.Cost
                }))
                .Where(c => !c.Combo.Equals(tried) && c.Distance + c.Cost < TooFar)
                .OrderBy(c => c.Distance + c.Cost)
                .ThenBy(c => c.Distance)
                .Select(c => new FreeKey { Combo = c.Combo, Why = Why(c.Combo, tried, name) });
        }

        /// <summary>
        /// The modifiers this setup's shortcuts already use, most used first, right and left
        /// counted as one. With none in use it falls back to Alt alone: in the world Ctrl crouches
        /// and Shift runs, so Alt is the only one that does nothing else when pressed.
        /// </summary>
        private static List<KeyCode> Preferred()
        {
            var uses = Offered.ToDictionary(m => m, m => 0);

            foreach (var bind in BindRegistry.All)
            {
                if (bind.Modifiers != ModifierBehavior.Strict || !bind.Combo.IsBound) continue;

                foreach (var modifier in bind.Combo.Modifiers)
                {
                    var left = Left(modifier);
                    if (uses.ContainsKey(left)) uses[left]++;
                }
            }

            // OrderByDescending is stable, so a tie keeps the order Offered gives.
            var used = Offered.Where(m => uses[m] > 0).OrderByDescending(m => uses[m]).ToList();
            return used.Count > 0 ? used : new List<KeyCode> { KeyCode.LeftAlt };
        }

        private static KeyCode Left(KeyCode modifier) =>
            modifier == KeyCode.RightAlt ? KeyCode.LeftAlt :
            modifier == KeyCode.RightControl ? KeyCode.LeftControl :
            modifier == KeyCode.RightShift ? KeyCode.LeftShift :
            modifier;

        private static string Why(KeyCombo combo, KeyCombo tried, string name)
        {
            var with = combo.Modifiers.Length == 0
                ? "on its own"
                : "with " + string.Join(" + ", combo.Modifiers.Select(Short).ToArray());

            if (combo.Main == tried.Main) return "same key, " + with;
            return combo.Modifiers.SequenceEqual(tried.Modifiers) ? "near " + name : $"near {name}, {with}";
        }

        private class ModifierChoice
        {
            public readonly KeyCode[] Modifiers;
            public readonly float Cost;

            public ModifierChoice(KeyCode[] modifiers, float cost)
            {
                Modifiers = modifiers;
                Cost = cost;
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
            // The game's raw aliases, such as MouseForward or Tab, are not in here: nothing in its
            // code reads them by name, and the controls that do use those keys are binds already.
            var read = ReadByGame.FirstOrDefault(g => g.Combo.Main == combo.Main &&
                (bind.Modifiers != ModifierBehavior.Strict || g.Combo.Modifiers.All(combo.Modifiers.Contains)));
            return read?.What;
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
            modifier == KeyCode.LeftAlt ? "Alt" :
            modifier == KeyCode.LeftControl ? "Ctrl" :
            modifier == KeyCode.LeftShift ? "Shift" :
            KeyLabels.Of(modifier);
    }
}
