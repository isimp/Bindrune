using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bindrune
{
    /// <summary>
    /// Translates between Unity Input System binding paths (what vanilla ZInput stores,
    /// e.g. "&lt;Keyboard&gt;/leftAlt") and legacy KeyCodes (what mod configs store), so binds from
    /// both worlds can be compared. Built from ZInput's own lookup tables to stay correct
    /// across game updates instead of hardcoding a table that will rot.
    /// </summary>
    public static class KeyPaths
    {
        private static Dictionary<string, KeyCode> _suffixToKey;
        private static Dictionary<KeyCode, string> _keyToPath;

        public static void Build()
        {
            if (_suffixToKey != null) return;
            _suffixToKey = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase);
            _keyToPath = new Dictionary<KeyCode, string>();

            Absorb("s_keyCodeToKeyMap", null, "<Keyboard>", GameKeyPath());
            Absorb("s_keyCodeToMouseButtonMap", "button", "<Mouse>", null);
        }

        /// <summary>
        /// The game's own path for a key, which is not always the key's name: digits are "1"
        /// rather than "digit1", and the Windows, Command and Apple keys are all "Meta". A path
        /// built from the name instead matches no control, and a bind given one stops answering
        /// to any key at all. Null when the game no longer has the method, which leaves the paths
        /// built from the names.
        /// </summary>
        private static Func<object, string> GameKeyPath()
        {
            var method = AccessTools.Method(typeof(ZInput), "KeyToPath", new[] { typeof(Key) });
            if (method == null) return null;

            return key => method.Invoke(null, new[] { key }) as string;
        }

        /// <summary>The input path vanilla expects for a key, or null when it has no equivalent.</summary>
        public static string ToPath(KeyCode key)
        {
            Build();
            return _keyToPath.TryGetValue(key, out var path) ? path : null;
        }

        private static void Absorb(string fieldName, string suffixWord, string device, Func<object, string> gamePath)
        {
            try
            {
                var field = AccessTools.Field(typeof(ZInput), fieldName);
                if (!(field?.GetValue(null) is IDictionary map)) return;

                foreach (DictionaryEntry e in map)
                {
                    if (!(e.Key is KeyCode kc) || e.Value == null) continue;

                    // Kept even where the game spells it differently, so a path written from the
                    // name by an earlier version still reads as its key.
                    var name = e.Value.ToString();
                    _suffixToKey[name] = kc;

                    var path = gamePath?.Invoke(e.Value);
                    if (!string.IsNullOrEmpty(path))
                    {
                        var slash = path.LastIndexOf('/');
                        _suffixToKey[slash >= 0 ? path.Substring(slash + 1) : path] = kc;
                        if (!_keyToPath.ContainsKey(kc)) _keyToPath[kc] = path;
                        continue;
                    }

                    var control = name;
                    if (suffixWord != null && !name.EndsWith(suffixWord, StringComparison.OrdinalIgnoreCase))
                    {
                        control = name + char.ToUpperInvariant(suffixWord[0]) + suffixWord.Substring(1);
                        _suffixToKey[name + suffixWord] = kc;
                    }

                    // Input System control names are camelCase: Key.LeftAlt -> "<Keyboard>/leftAlt".
                    if (control.Length > 0 && !_keyToPath.ContainsKey(kc))
                        _keyToPath[kc] = device + "/" + char.ToLowerInvariant(control[0]) + control.Substring(1);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"KeyPaths: could not read ZInput.{fieldName}: {ex.Message}");
            }
        }

        /// <summary>Maps an input path to a KeyCode. Returns KeyCode.None when it is not a keyboard/mouse control.</summary>
        public static KeyCode FromPath(string path)
        {
            Build();
            if (string.IsNullOrEmpty(path)) return KeyCode.None;

            var slash = path.LastIndexOf('/');
            var suffix = slash >= 0 ? path.Substring(slash + 1) : path;
            return _suffixToKey.TryGetValue(suffix, out var kc) ? kc : KeyCode.None;
        }

        public static bool IsKeyboardOrMouse(string path) =>
            !string.IsNullOrEmpty(path) &&
            (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase));
    }
}
