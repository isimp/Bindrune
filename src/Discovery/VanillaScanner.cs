using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Bindrune.Discovery
{
    /// <summary>Reads the game's own keybinds out of ZInput.</summary>
    public static class VanillaScanner
    {
        public static List<BindEntry> Scan(HashSet<string> claimedButtonNames)
        {
            var result = new List<BindEntry>();
            var zinput = ZInput.instance;
            if (zinput == null) return result;

            if (!(AccessTools.Field(typeof(ZInput), "m_buttons")?.GetValue(zinput) is IDictionary buttons))
            {
                Plugin.Log.LogWarning("VanillaScanner: ZInput.m_buttons not readable; vanilla binds unavailable.");
                return result;
            }

            var getPath = AccessTools.Method(typeof(ZInput.ButtonDef), "GetActionPath");
            var rebindable = AccessTools.PropertyGetter(typeof(ZInput.ButtonDef), "Rebindable");
            VanillaExposure.Ensure();

            foreach (DictionaryEntry e in buttons)
            {
                var def = e.Value as ZInput.ButtonDef;
                if (def == null) continue;
                // Mods register their own buttons into ZInput; those belong to the mod, not the game.
                if (claimedButtonNames.Contains(def.Name)) continue;

                string path = null;
                try { path = getPath?.Invoke(def, new object[] { true }) as string; }
                catch (Exception ex) { Plugin.Log.LogDebug($"VanillaScanner: path read failed for {def.Name}: {ex.Message}"); }

                if (!KeyPaths.IsKeyboardOrMouse(path)) continue;

                var key = KeyPaths.FromPath(path);
                // The game parks unbound actions on a "None" control rather than clearing them.
                var unbound = key == KeyCode.None && path.EndsWith("/None", StringComparison.OrdinalIgnoreCase);
                var canRebind = true;
                try { canRebind = rebindable == null || (bool)rebindable.Invoke(def, null); }
                catch { /* assume rebindable */ }

                result.Add(new BindEntry
                {
                    Id = BindIds.Vanilla(def.Name),
                    OwnerName = "Valheim",
                    OwnerGuid = "vanilla",
                    Label = def.Name,
                    Section = "Game",
                    Source = BindSource.Vanilla,
                    // Vanilla bindings are single input paths with no modifier concept,
                    // so they fire regardless of which modifiers are held.
                    Modifiers = ModifierBehavior.SingleKey,
                    Combo = new KeyCombo(key, null, key == KeyCode.None && !unbound ? path : null),
                    Editable = canRebind,
                    ReadOnlyReason = canRebind ? null : "the game marks this bind as fixed",
                    Handle = def,
                    // Plumbing, by two facts that agree: the game refuses to let the player
                    // rebind it, and it is not one of the controls we know are player facing.
                    // "LShift" and "MouseLeft" are how the UI reads a raw key, not extra binds.
                    Internal = !canRebind
                               && !Context.VanillaDefaults.ByName.ContainsKey(def.Name)
                               && !VanillaExposure.IsExplicitlyExposed(def.Name)
                });
            }

            return result;
        }
    }
}
