using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Bindrune.Personal;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bindrune
{
    /// <summary>
    /// Hotbar keys Bindrune sets and keeps where the game would not.
    ///
    /// The hotbar digits are registered without the rebindable flag, and that flag is all that
    /// holds them in place. An override on one of those buttons works like on any other, but the
    /// settings screen refuses to start one, and ZInput.Save and Load skip the button, so a change
    /// is never written and is lost the next time the game loads its controls. Load also throws
    /// every keyboard button away and builds it again from its defaults, at startup and each time
    /// the Controls screen is left. So the keys set here are kept in bindrune.keys and put back
    /// after every Load, and after the game's own reset too: that screen does not list the digits,
    /// so its reset cannot be meant for them.
    ///
    /// Each slot's Alt button the game does let you rebind, and keeps. It cannot keep a key with a
    /// modifier, though, since its format holds one key, so an Alt key with a modifier is kept
    /// here as well, and a plain one is left to the game. Rebinding an Alt key in the game's own
    /// Controls screen hands it back to the game first.
    ///
    /// Nothing Bindrune keeps is written to the game's own settings, so without Bindrune the
    /// digits are the game's again.
    /// </summary>
    internal static class FixedKeys
    {
        /// <summary>
        /// The hotbar's own keys. The one thing in the game that reads them is the player's
        /// hotbar check, which reads each beside its Alt button, so moving one changes what that
        /// key does and nothing else. The other fixed buttons are read by the UI as the raw key,
        /// and moving those would break menus.
        /// </summary>
        private static readonly string[] Digits =
            { "Hotbar1", "Hotbar2", "Hotbar3", "Hotbar4", "Hotbar5", "Hotbar6", "Hotbar7", "Hotbar8" };

        private static readonly string[] Alts = Digits.Select(d => d + "Alt").ToArray();

        private static readonly string[] Hotbar = Digits.Concat(Alts).ToArray();

        private const string None = "none";

        /// <summary>The game's own word for the hotbar, from its radial menu, in whatever language it runs in.</summary>
        private const string HotbarWord = "$radial_hotbar";

        private static FieldInfo _buttonsField;
        private static MethodInfo _getPath;

        private static Dictionary<string, string> _moved;

        /// <summary>Lines naming buttons this version does not keep, written back as they were.</summary>
        private static List<string> _other;

        /// <summary>The buttons the last pass put a key on, so one dropped since goes back to the game's.</summary>
        private static readonly HashSet<string> Applied = new HashSet<string>();

        private static ZInput _restoredFor;

        /// <summary>Changes whenever what reaches a slot may have changed, so its label knows to redraw.</summary>
        public static int Version { get; private set; }

        /// <summary>Whether any slot's own key is not the game's digit, which is when the bar's labels change.</summary>
        public static bool BarChanged
        {
            get
            {
                var moved = Moved;
                foreach (var digit in Digits)
                    if (moved.ContainsKey(digit))
                        return true;

                return false;
            }
        }

        static FixedKeys() => PersonalStore.Reloaded += () =>
        {
            _moved = null;
            _restoredFor = null;
        };

        /// <summary>A slot's own key, which only Bindrune can move.</summary>
        public static bool IsDigit(string button) => Array.IndexOf(Digits, button) >= 0;

        /// <summary>A slot's Alt key, which the game keeps as long as it has no modifier.</summary>
        public static bool IsAlt(string button) => Array.IndexOf(Alts, button) >= 0;

        /// <summary>Either kind of hotbar key, which can carry one modifier here.</summary>
        public static bool IsHotbar(string button) => Array.IndexOf(Hotbar, button) >= 0;

        /// <summary>
        /// The game's button of that name as it is now. Every Load replaces them all, so a button
        /// held from before may no longer be the one the game reads.
        /// </summary>
        public static ZInput.ButtonDef Live(string name)
        {
            var zinput = ZInput.instance;
            return zinput == null ? null : Button(zinput, name);
        }

        public static void Patch(Harmony harmony)
        {
            try
            {
                var after = new HarmonyMethod(AccessTools.Method(typeof(FixedKeys), nameof(AfterControlsLoaded)));

                var load = AccessTools.Method(typeof(ZInput), "Load");
                if (load != null) harmony.Patch(load, postfix: after);
                else
                    Plugin.WarnOnce("Bindrune: the game's control loading was not found, so a moved hotbar key " +
                                    "comes back when the game starts but not after the Controls screen.");

                var reset = AccessTools.Method(typeof(ZInput), "ResetToDefault");
                if (reset != null) harmony.Patch(reset, postfix: after);
                else
                    Plugin.WarnOnce("Bindrune: the game's control reset was not found, so resetting the " +
                                    "controls also resets a moved hotbar key until the game next loads them.");

                var rebind = AccessTools.Method(typeof(ZInput), "StartBindKey");
                if (rebind != null)
                    harmony.Patch(rebind, new HarmonyMethod(AccessTools.Method(typeof(FixedKeys), nameof(BeforeGameRebind))));
                else
                    Plugin.WarnOnce("Bindrune: the game's own rebinding was not found, so an Alt hotbar key with a " +
                                    "modifier comes back after you rebind it in the Controls screen.");
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not follow the game reloading its controls: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// The game's own ZInput rather than ZInput.instance: the first Load runs inside the
        /// constructor, before the instance has been stored.
        /// </summary>
        private static void AfterControlsLoaded(ZInput __instance) => RestoreFor(__instance);

        /// <summary>
        /// The game's own screen is about to rebind a button, which it does by overriding every
        /// binding on it at once, so a composite kept here would be scrambled and then put back
        /// over your choice at the next Load. An Alt key is handed back to the game first.
        /// </summary>
        private static void BeforeGameRebind(string name)
        {
            try
            {
                if (IsAlt(name) && Moved.ContainsKey(name)) Forget(name);
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not hand {name} back to the game: {ex.Message}", ex);
            }
        }

        /// <summary>Once for the game's ZInput, in case it loaded before the patch above was in place.</summary>
        public static void EnsureRestored()
        {
            var zinput = ZInput.instance;
            if (zinput != null && !ReferenceEquals(zinput, _restoredFor)) RestoreFor(zinput);
        }

        /// <summary>Draws the slot labels and the prompt again, for when a key or what it is called has changed outside this class.</summary>
        public static void Relabel()
        {
            var zinput = ZInput.instance;
            if (zinput == null) return;

            Version++;

            try
            {
                ShowInPrompts(zinput);
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not rename the hotbar keys in the game's prompts: {ex.Message}", ex);
            }
        }

        /// <summary>Sets a hotbar key, with a modifier as "modifier+key" or to none, and keeps it.</summary>
        public static void Set(string button, string stored)
        {
            PersonalStore.Sync();

            Moved[button] = IsNone(stored) ? None : stored;
            Save();
            Apply();
        }

        /// <summary>Hands a button back to the key the game gives it.</summary>
        public static void Forget(string button)
        {
            PersonalStore.Sync();

            if (Moved.Remove(button)) Save();
            Apply();
        }

        /// <summary>
        /// What to print on a hotbar slot. The game's own number while its digit is where the game
        /// put it, the key it was moved to when it has one, and otherwise the key of its Alt
        /// button, the other thing that reaches the slot. Empty when no key reaches it at all.
        /// </summary>
        public static string SlotLabel(ZInput zinput, int index)
        {
            var number = (index + 1).ToString(CultureInfo.InvariantCulture);
            var digit = "Hotbar" + number;
            if (!Moved.ContainsKey(digit)) return number;

            var own = KeyOn(zinput, digit);
            return own.Length > 0 ? own : KeyOn(zinput, digit + "Alt");
        }

        /// <summary>The key a hotbar button answers to, as shown, or empty when it has none.</summary>
        private static string KeyOn(ZInput zinput, string name)
        {
            if (Moved.TryGetValue(name, out var stored)) return stored == None ? "" : LabelOf(stored);

            var path = PathOf(Button(zinput, name));
            return IsNone(path) ? "" : LabelOf(path);
        }

        private static Dictionary<string, string> Moved
        {
            get
            {
                if (_moved == null) Load();
                return _moved;
            }
        }

        private static void Apply()
        {
            var zinput = ZInput.instance;
            if (zinput != null) RestoreFor(zinput);
        }

        private static void RestoreFor(ZInput zinput)
        {
            _restoredFor = zinput;

            try
            {
                Restore(zinput);
            }
            catch (Exception ex)
            {
                // Never out of here: this also runs inside the game's own control loading.
                Plugin.WarnOnce($"Bindrune: could not put the moved hotbar keys back: {ex.Message}", ex);
            }
        }

        private static void Restore(ZInput zinput)
        {
            foreach (var name in Hotbar)
            {
                var button = Button(zinput, name);
                if (button == null) continue;

                if (Moved.TryGetValue(name, out var stored)) Put(button, stored);
                else if (Applied.Contains(name)) Put(button, null);
            }

            Applied.Clear();
            Applied.UnionWith(Moved.Keys);

            Version++;
            ShowInPrompts(zinput);
        }

        /// <summary>
        /// Gives a button the key it was moved to, or its own key back when stored is null.
        ///
        /// A key with a modifier is a composite of the two, which the game's own format has no
        /// room for, so the game's binding is parked on None and the composite added beside it.
        /// The game only ever reads a button's first binding for its name, and the button fires
        /// for any of them. ZInput's Rebind overrides every binding on the action at once, so a
        /// composite left in place would be flattened by it: every earlier composite is removed
        /// first, and a new one is only added after the Rebind.
        /// </summary>
        private static void Put(ZInput.ButtonDef button, string stored)
        {
            var action = button.ButtonAction;

            action.Disable();
            while (action.bindings.Count > 1) action.ChangeBinding(1).Erase();
            action.Enable();

            if (stored == null)
            {
                button.ResetBinding();
                return;
            }

            var parts = stored.Split('+');
            var main = parts[parts.Length - 1] == None ? KeyPaths.ToPath(KeyCode.None) : parts[parts.Length - 1];
            if (main == null) return;

            if (parts.Length == 1)
            {
                button.Rebind(main);
                return;
            }

            var none = KeyPaths.ToPath(KeyCode.None);
            if (none == null) return;
            button.Rebind(none);

            action.Disable();
            action.AddCompositeBinding("OneModifier").With("Modifier", parts[0]).With("Binding", main);
            action.Enable();
        }

        /// <summary>
        /// The key a composite Bindrune put on a button stands for, or null when it has none. The
        /// game's own binding reads None while a composite is in use, so this is what to show.
        /// </summary>
        public static KeyCombo? Composite(ZInput.ButtonDef button)
        {
            string modifier = null, main = null;

            foreach (var binding in button.ButtonAction.bindings)
            {
                if (!binding.isPartOfComposite) continue;
                if (string.Equals(binding.name, "modifier", StringComparison.OrdinalIgnoreCase)) modifier = binding.effectivePath;
                else if (string.Equals(binding.name, "binding", StringComparison.OrdinalIgnoreCase)) main = binding.effectivePath;
            }

            if (main == null) return null;

            var key = KeyPaths.FromPath(main);
            var held = KeyPaths.FromPath(modifier);
            return new KeyCombo(key, held != KeyCode.None ? new[] { held } : null, key == KeyCode.None ? main : null);
        }

        /// <summary>
        /// Keeps the "[1-8]" in the game's hover prompts, such as cooking or attaching an item to
        /// a stand, in step with the keys. It is the display text of the HotbarUse button, which
        /// the game sets once when it builds the button, so it is written again after every
        /// change. One or two runs, such as Alt + 1-8, read at a glance; beyond that a prompt
        /// becomes a list to decipher, so it names the hotbar instead, in the game's own word.
        /// </summary>
        private static void ShowInPrompts(ZInput zinput)
        {
            var hotbarUse = Button(zinput, "HotbarUse");
            if (hotbarUse == null) return;

            var parts = Summarise(Enumerable.Range(0, Digits.Length).Select(i => SlotLabel(zinput, i)));
            var text = parts.Count <= 2 ? string.Join("/", parts.ToArray()) : HotbarWord;
            if (hotbarUse.DisplayNameOverride == text) return;

            hotbarUse.DisplayNameOverride = text;
            ForgetTranslations();
        }

        /// <summary>The slots' keys in slot order: runs of three or more as a range, the rest one by one.</summary>
        private static List<string> Summarise(IEnumerable<string> labels)
        {
            var keys = labels.Where(l => l.Length > 0).ToList();
            var parts = new List<string>();

            for (var start = 0; start < keys.Count;)
            {
                var end = start;
                while (end + 1 < keys.Count && Follows(keys[end], keys[end + 1])) end++;

                if (end - start >= 2) parts.Add(Range(keys[start], keys[end]));
                else
                    for (var i = start; i <= end; i++)
                        parts.Add(keys[i]);

                start = end + 1;
            }

            return parts;
        }

        /// <summary>
        /// A run written once. After a modifier the modifier is said once, as Alt + 1-8, and a key
        /// name is repeated, as F1-F8, since F1-8 could be read as F1 to the digit 8.
        /// </summary>
        private static string Range(string first, string last)
        {
            Split(first, out var prefix, out _);
            Split(last, out _, out var number);
            return prefix.TrimEnd().EndsWith("+", StringComparison.Ordinal)
                ? first + "-" + number.ToString(CultureInfo.InvariantCulture)
                : first + "-" + last;
        }

        /// <summary>Whether one label is the other with its trailing number one higher, as F2 follows F1.</summary>
        private static bool Follows(string a, string b) =>
            Split(a, out var prefixA, out var numberA) && Split(b, out var prefixB, out var numberB) &&
            prefixA == prefixB && numberB == numberA + 1;

        private static bool Split(string label, out string prefix, out int number)
        {
            var digits = label.Length;
            while (digits > 0 && label[digits - 1] >= '0' && label[digits - 1] <= '9') digits--;

            prefix = label.Substring(0, digits);
            return int.TryParse(label.Substring(digits), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>
        /// Empties the game's cache of translated text, which keeps a whole prompt once it has
        /// been built, so a prompt already shown would otherwise go on naming the old keys. Read
        /// through the private field: Localization.instance sets the translations up when they
        /// are not yet, and when they are not there is nothing cached to empty.
        /// </summary>
        private static void ForgetTranslations()
        {
            var localization = AccessTools.Field(typeof(Localization), "m_instance")?.GetValue(null);
            var cache = localization == null ? null : AccessTools.Field(typeof(Localization), "m_cache")?.GetValue(localization);
            if (cache == null) return;

            AccessTools.Method(cache.GetType(), "EvictAll")?.Invoke(cache, null);
        }

        /// <summary>
        /// A stored key as it is shown: its modifier first when it has one, spelled short. The
        /// spaces around the plus do not break, so a line that wraps never splits a key in two.
        /// </summary>
        private static string LabelOf(string stored)
        {
            var parts = stored.Split('+');
            var main = KeyOf(parts[parts.Length - 1]);
            if (parts.Length == 1) return main;

            var modifier = KeyPaths.FromPath(parts[0]);
            return (modifier != KeyCode.None ? KeyLabels.Modifier(modifier) : KeyLabels.OfPath(parts[0])) + " + " + main;
        }

        private static string KeyOf(string path)
        {
            var key = KeyPaths.FromPath(path);
            return key != KeyCode.None ? KeyLabels.Of(key) : KeyLabels.OfPath(path);
        }

        /// <summary>The game parks a button with no key on its None control rather than leaving it empty.</summary>
        private static bool IsNone(string path) =>
            string.IsNullOrEmpty(path) || path == None ||
            (KeyPaths.FromPath(path) == KeyCode.None && path.EndsWith("/None", StringComparison.OrdinalIgnoreCase));

        private static ZInput.ButtonDef Button(ZInput zinput, string name)
        {
            if (_buttonsField == null) _buttonsField = AccessTools.Field(typeof(ZInput), "m_buttons");

            return _buttonsField?.GetValue(zinput) is IDictionary buttons && buttons.Contains(name)
                ? buttons[name] as ZInput.ButtonDef
                : null;
        }

        private static string PathOf(ZInput.ButtonDef button)
        {
            if (button == null) return null;
            if (_getPath == null) _getPath = AccessTools.Method(typeof(ZInput.ButtonDef), "GetActionPath");

            return _getPath?.Invoke(button, new object[] { true }) as string;
        }

        private static void Load()
        {
            _moved = new Dictionary<string, string>();
            _other = new List<string>();

            foreach (var line in PersonalStore.Lines(PersonalStore.Fixed))
            {
                var parts = line.Split('\t');
                if (parts.Length == 2 && IsHotbar(parts[0]) && parts[1].Length > 0) _moved[parts[0]] = parts[1];
                else _other.Add(line);
            }
        }

        private static void Save()
        {
            var moved = Moved;
            PersonalStore.Replace(PersonalStore.Fixed,
                _other.Concat(moved.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Key + "\t" + e.Value)));
        }
    }
}
