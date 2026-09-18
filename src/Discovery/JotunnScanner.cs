using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;

namespace Bindrune.Discovery
{
    /// <summary>
    /// Reads buttons that mods registered through Jotunn's InputManager. These are richer than a
    /// raw config entry (they carry a display hint and know their own backing config), so when a
    /// bind shows up here we prefer this view and skip the plain config entry behind it.
    /// </summary>
    public static class JotunnScanner
    {
        public static List<BindEntry> Scan(HashSet<ConfigEntryBase> backingEntries, HashSet<string> claimedButtonNames)
        {
            var result = new List<BindEntry>();

            var instance = InputManager.Instance;
            if (instance == null) return result;

            if (!(AccessTools.Field(typeof(InputManager), "Buttons")?.GetValue(instance) is IDictionary buttons))
            {
                Plugin.Log.LogWarning("JotunnScanner: InputManager.Buttons not readable; Jotunn binds unavailable.");
                return result;
            }

            foreach (DictionaryEntry e in buttons)
            {
                var cfg = e.Value as ButtonConfig;
                if (cfg == null) continue;

                // Jotunn registers buttons into ZInput under "<button name>!<owning mod guid>",
                // and that same name is what the vanilla scan would otherwise report as a game bind.
                var dictKey = e.Key?.ToString() ?? cfg.Name;
                var bang = dictKey.IndexOf('!');
                var buttonName = bang > 0 ? dictKey.Substring(0, bang) : dictKey;
                var guid = bang > 0 ? dictKey.Substring(bang + 1) : "";
                claimedButtonNames.Add(dictKey);
                if (!string.IsNullOrEmpty(cfg.Name)) claimedButtonNames.Add(cfg.Name);

                var strict = false;
                KeyCombo combo;
                try
                {
                    var shortcut = cfg.Shortcut;
                    if (shortcut.MainKey != KeyCode.None)
                    {
                        strict = true;
                        combo = new KeyCombo(shortcut.MainKey, shortcut.Modifiers);
                    }
                    else if (cfg.Key != KeyCode.None)
                    {
                        combo = new KeyCombo(cfg.Key, null);
                    }
                    else if (!string.IsNullOrEmpty(cfg.Axis))
                    {
                        combo = new KeyCombo(KeyCode.None, null, "axis:" + cfg.Axis);
                    }
                    else
                    {
                        combo = KeyCombo.None;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"JotunnScanner: skipping {dictKey}: {ex.Message}");
                    continue;
                }

                var configBacked = cfg.ShortcutConfig != null || cfg.Config != null;
                if (cfg.ShortcutConfig != null) backingEntries.Add(cfg.ShortcutConfig);
                if (cfg.Config != null) backingEntries.Add(cfg.Config);

                // An axis is a stick, a trigger or the wheel, and the mod reads that axis by name.
                // Such a button can still carry a key config, and writing a key into it would look
                // like a rebind while the mod went on reading the axis - so say no instead.
                var onAxis = combo.RawPath != null;
                var editable = configBacked && !onAxis;

                var readOnlyReason = onAxis
                    ? "this button is driven by an axis - a stick, a trigger or the wheel - which cannot be set to a key here"
                    : configBacked ? null : "the mod hardcoded this button, it has no config setting";

                result.Add(new BindEntry
                {
                    Id = BindIds.Jotunn(dictKey),
                    OwnerName = Plugin.ResolveModName(guid),
                    OwnerGuid = guid,
                    Label = string.IsNullOrEmpty(cfg.Hint) ? buttonName : cfg.Hint,
                    Section = "Jotunn button",
                    Description = buttonName,
                    Source = BindSource.Jotunn,
                    Modifiers = strict ? ModifierBehavior.Strict : ModifierBehavior.SingleKey,
                    Combo = combo,
                    Editable = editable,
                    ReadOnlyReason = readOnlyReason,
                    Handle = cfg
                });
            }

            return result;
        }
    }
}
