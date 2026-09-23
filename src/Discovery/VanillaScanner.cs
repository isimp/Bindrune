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

            foreach (DictionaryEntry e in buttons)
            {
                var def = e.Value as ZInput.ButtonDef;
                if (def == null) continue;
                // Mods register their own buttons into ZInput; those belong to the mod, not the game.
                if (claimedButtonNames.Contains(def.Name)) continue;

                string path = null;
                var read = false;
                if (getPath != null)
                {
                    try
                    {
                        path = getPath.Invoke(def, new object[] { true }) as string;
                        read = true;
                    }
                    catch (Exception ex)
                    {
                        // A button with no binding behind it at all, such as the one standing for
                        // the whole hotbar in the key hints. Nothing to read and nothing to set.
                        Plugin.WarnOnce($"VanillaScanner: path read failed for {def.Name}: {ex.Message}");
                    }
                }

                // A bind the game ships unset holds an empty path rather than a keyboard one. It
                // is still a keyboard bind, and one the player can fill in, so it belongs in the
                // list as unbound instead of being taken for another device's.
                var blank = read && string.IsNullOrEmpty(path);
                if (blank ? def.Source != ZInput.InputSource.KeyboardMouse : !KeyPaths.IsKeyboardOrMouse(path)) continue;

                var key = blank ? KeyCode.None : KeyPaths.FromPath(path);
                // The game parks unbound actions on a "None" control rather than clearing them.
                var unbound = blank || (key == KeyCode.None && path.EndsWith("/None", StringComparison.OrdinalIgnoreCase));
                var canRebind = true;
                try { canRebind = rebindable == null || (bool)rebindable.Invoke(def, null); }
                catch { /* assume rebindable */ }

                // A slot's own key the game fixes but Bindrune moves and keeps itself. See FixedKeys.
                var editable = canRebind || FixedKeys.IsDigit(def.Name);

                // A hotbar key with a modifier, which Bindrune built for the game; the game's own
                // binding reads None while it is in use.
                KeyCombo? composite = null;
                if (FixedKeys.IsHotbar(def.Name))
                {
                    try { composite = FixedKeys.Composite(def); }
                    catch (Exception ex) { Plugin.WarnOnce($"VanillaScanner: could not read the key Bindrune set for {def.Name}: {ex.Message}"); }
                }

                result.Add(new BindEntry
                {
                    Id = BindIds.Vanilla(def.Name),
                    OwnerName = "Valheim",
                    OwnerGuid = "vanilla",
                    Label = def.Name,
                    Section = "Game",
                    Source = BindSource.Vanilla,
                    // Vanilla bindings are single input paths with no modifier concept, so they
                    // fire regardless of which modifiers are held. A composite needs its modifier
                    // and ignores any others.
                    Modifiers = composite != null ? ModifierBehavior.Required : ModifierBehavior.SingleKey,
                    Combo = composite ?? new KeyCombo(key, null, key == KeyCode.None && !unbound ? path : null),
                    Editable = editable,
                    ReadOnlyReason = editable ? null : "the game marks this bind as fixed",
                    Handle = def,
                    // Plumbing, by two facts that agree: the game refuses to let the player
                    // rebind it, and it is not one of the controls we know are player facing.
                    // "LShift" and "MouseLeft" are how the UI reads a raw key, not extra binds,
                    // and the camera zoom pair is a row the settings screen draws for a bind no
                    // code reads. A bind the player can set is never plumbing, whatever its name.
                    Internal = !canRebind && !Context.VanillaDefaults.ByName.ContainsKey(def.Name)
                });
            }

            return result;
        }
    }
}
