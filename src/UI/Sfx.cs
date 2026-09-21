using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace Bindrune.UI
{
    /// <summary>
    /// The game's own interface sounds, played the way its buttons play them: an instance of the
    /// sound's prefab, which removes itself when done, at the player's own sound volume. Every
    /// button already clicks through the ButtonSfx Jotunn gives it; these are for what happens
    /// without a button.
    ///
    /// Found by name through Jotunn, which reaches the game's asset bundles, and kept once found.
    /// A sound whose bundle is not loaded yet is simply not played and looked for again next
    /// time: a missing sound is never worth more than silence.
    /// </summary>
    public static class Sfx
    {
        public const string PanelOpen = "sfx_gui_inventory_open";
        public const string PanelClose = "sfx_gui_inventory_close";

        /// <summary>Dropping an item into a slot, which is what setting a key amounts to.</summary>
        public const string KeySet = "sfx_gui_moveitem";

        /// <summary>A quiet tick: the hints can have nothing to show, so this may be the only sign.</summary>
        public const string HintsToggled = "sfx_gui_select";

        private static readonly Dictionary<string, GameObject> Found = new Dictionary<string, GameObject>();

        public static void Play(string name)
        {
            try
            {
                if (!Found.TryGetValue(name, out var prefab) || prefab == null)
                {
                    prefab = PrefabManager.Cache.GetPrefab<GameObject>(name);
                    if (prefab == null) return;

                    Found[name] = prefab;
                }

                UnityEngine.Object.Instantiate(prefab);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Bindrune: could not play {name}: {ex.Message}");
            }
        }
    }
}
