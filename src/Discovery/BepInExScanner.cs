using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace Bindrune.Discovery
{
    /// <summary>Reads keybinds out of every loaded plugin's BepInEx configuration.</summary>
    public static class BepInExScanner
    {
        private static readonly string[] NameHints = { "key", "hotkey", "shortcut", "bind", "button", "modifier" };

        public static List<BindEntry> Scan(HashSet<ConfigEntryBase> skip)
        {
            var result = new List<BindEntry>();

            foreach (var kv in Chainloader.PluginInfos)
            {
                var info = kv.Value;
                // Bindrune's own keys are included deliberately: the panel's hotkey can collide
                // with a mod just like anything else, and hiding it would be the one conflict
                // this tool could never report.
                var guid = info?.Metadata?.GUID;
                if (guid == null) continue;

                ConfigFile config;
                try { config = info.Instance?.Config; }
                catch { continue; }
                if (config == null) continue;

                foreach (var entry in Snapshot(config))
                {
                    if (entry.Value == null || skip.Contains(entry.Value)) continue;

                    var bind = Interpret(info, guid, entry.Key, entry.Value);
                    if (bind == null) continue;

                    // Mods can describe when their own bind is live; read it while we are here.
                    Context.KnownSituations.ReadFrom(bind.Id, entry.Value);
                    result.Add(bind);
                }
            }

            return result;
        }

        /// <summary>Copies the entry list so a mod writing its config mid-scan cannot break iteration.</summary>
        private static List<KeyValuePair<ConfigDefinition, ConfigEntryBase>> Snapshot(ConfigFile config)
        {
            try { return config.ToList(); }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"BepInExScanner: could not enumerate a config: {ex.Message}");
                return new List<KeyValuePair<ConfigDefinition, ConfigEntryBase>>();
            }
        }

        private static BindEntry Interpret(PluginInfo info, string guid, ConfigDefinition def, ConfigEntryBase entry)
        {
            var type = entry.SettingType;
            KeyCombo combo;
            BindSource source;
            ModifierBehavior behavior;
            var editable = true;
            string readOnlyReason = null;

            if (type == typeof(KeyboardShortcut))
            {
                var sc = (KeyboardShortcut)entry.BoxedValue;
                combo = new KeyCombo(sc.MainKey, sc.Modifiers);
                source = BindSource.ModTyped;
                // BepInEx only reports a KeyboardShortcut as pressed when the held modifiers
                // match exactly, so a modifier here genuinely protects the bind.
                behavior = ModifierBehavior.Strict;
            }
            else if (type == typeof(KeyCode))
            {
                combo = new KeyCombo((KeyCode)entry.BoxedValue, null);
                source = BindSource.ModTyped;
                behavior = ModifierBehavior.SingleKey;
            }
            else if (type == typeof(string) && LooksLikeBind(def, entry, out combo))
            {
                source = BindSource.ModText;
                behavior = ModifierBehavior.Unknown;
                editable = false;
                readOnlyReason = "this mod stores its hotkey as free text, so it is safer to change in the mod's own settings";
            }
            else
            {
                return null;
            }

            return new BindEntry
            {
                Id = BindIds.Config(guid, def.Section, def.Key),
                OwnerName = info.Metadata?.Name ?? guid,
                OwnerGuid = guid,
                Label = def.Key,
                Section = def.Section,
                Description = entry.Description?.Description,
                Source = source,
                Modifiers = behavior,
                Combo = combo,
                Editable = editable,
                ReadOnlyReason = readOnlyReason,
                Handle = entry
            };
        }

        private static bool LooksLikeBind(ConfigDefinition def, ConfigEntryBase entry, out KeyCombo combo)
        {
            combo = KeyCombo.None;

            var haystack = (def.Key + " " + def.Section).ToLowerInvariant();
            if (!NameHints.Any(h => haystack.Contains(h))) return false;

            return TryParseCombo(entry.BoxedValue as string, out combo);
        }

        /// <summary>Parses "LeftAlt + H" / "H" / "None" into a combo. Rejects anything that is not all KeyCodes.</summary>
        public static bool TryParseCombo(string text, out KeyCombo combo)
        {
            combo = KeyCombo.None;
            if (string.IsNullOrEmpty(text)) return false;

            var parts = text.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            if (parts.Count == 0 || parts.Count > 5) return false;

            var keys = new List<KeyCode>();
            foreach (var part in parts)
            {
                if (!Enum.TryParse<KeyCode>(part, true, out var kc)) return false;
                keys.Add(kc);
            }

            var main = keys.FirstOrDefault(k => !KeyCombo.IsModifier(k));
            if (main == KeyCode.None && keys.Any(KeyCombo.IsModifier)) main = keys[keys.Count - 1];

            combo = new KeyCombo(main, keys.Where(KeyCombo.IsModifier));
            return true;
        }
    }
}
