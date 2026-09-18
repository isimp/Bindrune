using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Bindrune.Discovery
{
    /// <summary>
    /// The game's gamepad buttons, listed so you can read them, never editable.
    ///
    /// Valheim has no per-button gamepad rebinding, which is a fact about the game rather than a
    /// gap here: every gamepad button is registered with rebindable false, ZInput.Save only
    /// writes the rebindable ones, and the settings screen offers whole layouts instead of a key
    /// list. They are still listed so the panel shows what the controller does.
    ///
    /// They are read from ZInput.m_presentationButtons rather than m_buttons. The game fills that
    /// dictionary for every gamepad button as it registers them, while m_buttons only receives
    /// the ones belonging to the active layout, and it clears and refills it whenever the layout
    /// changes - so what is in it is always what the pad does right now.
    /// </summary>
    public static class GamepadButtons
    {
        public static List<BindEntry> Scan()
        {
            var result = new List<BindEntry>();

            var zinput = ZInput.instance;
            if (zinput == null) return result;

            // The game registers these whether or not a controller exists, so on a keyboard-only
            // setup they would be forty-odd rows nobody can use. ConnectedGamepadType is the
            // game's own "is a pad plugged in" - not GamepadActive, which only says the pad is
            // what you touched last, and so would be false at the moment you open the panel.
            if (!Connected())
            {
                Plugin.Log.LogDebug("Bindrune: no gamepad connected, so its buttons are not listed.");
                return result;
            }

            if (!(AccessTools.Field(typeof(ZInput), "m_presentationButtons")?.GetValue(zinput) is IDictionary buttons))
            {
                Plugin.Log.LogDebug("Bindrune: ZInput.m_presentationButtons not readable; gamepad buttons unavailable.");
                return result;
            }

            foreach (DictionaryEntry entry in buttons)
            {
                var name = Read(entry.Key, "Name");
                var path = Read(entry.Value, "Path");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

                var layout = Layout(entry.Key);

                result.Add(new BindEntry
                {
                    Id = BindIds.Gamepad(name, layout),
                    OwnerName = "Valheim",
                    OwnerGuid = "vanilla",
                    Label = name,
                    Section = string.IsNullOrEmpty(layout) ? "Gamepad" : "Gamepad - " + layout,
                    Description = path,
                    Source = BindSource.Gamepad,
                    Modifiers = ModifierBehavior.SingleKey,
                    Combo = new KeyCombo(KeyCode.None, null, Readable(path)),
                    Editable = false,
                    ReadOnlyReason = "the game has no per-button gamepad rebinding; Settings > Gamepad picks a whole layout instead",
                    // A pad button is on another device: it cannot be pressed by the same action
                    // as a key, and nothing here can change it. Comparing it would only produce
                    // noise nobody could act on.
                    Compared = false
                });
            }

            if (result.Count > 0) Plugin.Log.LogDebug($"Bindrune: {result.Count} gamepad buttons listed.");
            return result;
        }

        /// <summary>Whether a controller is plugged in at all.</summary>
        private static bool Connected()
        {
            try
            {
                return ZInput.ConnectedGamepadType != GamepadType.None;
            }
            catch (Exception ex)
            {
                // Better to list them than to hide them over a failed check.
                Plugin.Log.LogDebug($"Bindrune: could not tell whether a gamepad is connected ({ex.Message}); listing anyway.");
                return true;
            }
        }

        /// <summary>Reads a string property off one of ZInput's small record types.</summary>
        private static string Read(object source, string property)
        {
            try
            {
                return AccessTools.Property(source?.GetType(), property)?.GetValue(source, null) as string;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Bindrune: could not read {property} off a gamepad button: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Which set of controls a button belongs to. The game models these as record types with
        /// no members, so the type's own name is the only thing there is to go on.
        /// </summary>
        private static string Layout(object key)
        {
            try
            {
                var layout = AccessTools.Property(key?.GetType(), "Layout")?.GetValue(key, null);
                switch (layout?.GetType().Name)
                {
                    case "ControllerLayout": return "controller";
                    case "MouseLayout": return "mouse";
                    // AllLayouts means the button is the same whichever layout is picked, which
                    // is not worth a heading of its own.
                    default: return "";
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Bindrune: could not read a gamepad button's layout: {ex.Message}");
                return "";
            }
        }

        /// <summary>"&lt;Gamepad&gt;/buttonSouth" reads better as "Gamepad buttonSouth".</summary>
        private static string Readable(string path) =>
            path.Replace("<", "").Replace(">", "").Replace("/", " ");
    }
}
