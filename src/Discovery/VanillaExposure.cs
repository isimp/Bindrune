using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Valheim.SettingsGui;

namespace Bindrune.Discovery
{
    /// <summary>
    /// The binds Valheim actually shows the player, read from the settings screen's own key list.
    /// ZInput also holds raw aliases the UI uses internally (LShift, MouseLeft, Esc) and actions
    /// the game never surfaces, which are noise in a list of "your keybinds".
    /// </summary>
    public static class VanillaExposure
    {
        private static HashSet<string> _exposed;

        public static bool Ready => _exposed != null && _exposed.Count > 0;

        /// <summary>True only when the game's own control list was read and names this bind.</summary>
        public static bool IsExplicitlyExposed(string buttonName) => Ready && _exposed.Contains(buttonName);

        /// <summary>
        /// Looks for the game's control list. A failure is never remembered: the Menu simply may
        /// not exist yet, and caching that absence would keep us on the fallback for the whole
        /// session even once the real list is available.
        /// </summary>
        public static void Ensure()
        {
            if (_exposed != null) return;

            try
            {
                var menu = Resources.FindObjectsOfTypeAll<Menu>().FirstOrDefault();
                if (menu == null) return;

                var prefab = AccessTools.Field(typeof(Menu), "m_settingsPrefab")?.GetValue(menu) as GameObject;
                if (prefab == null) return;

                var settings = prefab.GetComponentInChildren<KeyboardMouseSettings>(true);
                if (settings == null) return;

                if (!(AccessTools.Field(typeof(KeyboardMouseSettings), "m_keys")?.GetValue(settings) is IEnumerable keys))
                    return;

                var found = new HashSet<string>();

                var nameField = AccessTools.Field(typeof(KeySetting), "m_keyName");
                foreach (var key in keys)
                {
                    if (key == null) continue;
                    if (nameField?.GetValue(key) is string name && name.Length > 0) found.Add(name);
                }

                if (found.Count == 0) return;

                _exposed = found;
                Plugin.Log.LogInfo($"Bindrune: the game lists {found.Count} rebindable controls.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not read the game's control list ({ex.Message}); showing every ZInput button.");
            }
        }
    }
}
