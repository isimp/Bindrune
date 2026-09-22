using System;
using System.Linq;
using BepInEx.Configuration;
using Bindrune.Personal;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;

namespace Bindrune
{
    /// <summary>Writes a new combo back to whichever system owns the bind.</summary>
    public static class BindWriter
    {
        /// <summary>Returns null on success, or a message explaining why nothing was written.</summary>
        public static string Apply(BindEntry bind, KeyCombo combo, SaveTarget target = SaveTarget.Personal)
        {
            if (!bind.Editable) return bind.ReadOnlyReason ?? "this bind cannot be changed from here";

            try
            {
                string problem;
                var wrote = false;

                switch (bind.Source)
                {
                    case BindSource.Vanilla:
                        problem = ApplyVanilla(bind, combo);
                        wrote = problem == null;
                        break;
                    case BindSource.Jotunn: problem = ApplyJotunn(bind, combo, out wrote); break;
                    default: problem = ApplyConfig(bind, combo, out wrote); break;
                }

                // Record what the bind actually became, which is not the same as "no problem":
                // a dropped modifier still wrote a key, and an unsupported setting wrote nothing.
                if (wrote) Remember(bind, target);
                return problem;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: writing {bind.Id} failed: {ex}");
                return "writing the new key failed, see the log";
            }
        }

        /// <summary>The key this bind shipped with, or an unbound combo when it cannot be read.</summary>
        public static KeyCombo DefaultOf(BindEntry bind)
        {
            try
            {
                switch (bind.Source)
                {
                    case BindSource.Vanilla:
                        if (!(bind.Handle is ZInput.ButtonDef def)) return KeyCombo.None;
                        // The non-effective path is the binding before any override was applied.
                        var path = AccessTools.Method(typeof(ZInput.ButtonDef), "GetActionPath")
                            ?.Invoke(def, new object[] { false }) as string;
                        return new KeyCombo(KeyPaths.FromPath(path), null);

                    case BindSource.Jotunn:
                        var cfg = bind.Handle as ButtonConfig;
                        if (cfg?.ShortcutConfig != null) return FromDefault(cfg.ShortcutConfig);
                        return cfg?.Config != null ? FromDefault(cfg.Config) : KeyCombo.None;

                    default:
                        return bind.Handle is ConfigEntryBase entry ? FromDefault(entry) : KeyCombo.None;
                }
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: no default for {bind.Id}: {ex.Message}");
                return KeyCombo.None;
            }
        }

        private static KeyCombo FromDefault(ConfigEntryBase entry)
        {
            var value = entry.DefaultValue;
            if (value is KeyboardShortcut shortcut) return new KeyCombo(shortcut.MainKey, shortcut.Modifiers);
            if (value is KeyCode key) return new KeyCombo(key, null);
            return KeyCombo.None;
        }

        /// <summary>Puts a bind back to the key its owner shipped with.</summary>
        public static string Reset(BindEntry bind)
        {
            if (!bind.Editable) return bind.ReadOnlyReason ?? "this bind cannot be changed from here";

            try
            {
                if (bind.Source == BindSource.Vanilla)
                {
                    if (!(bind.Handle is ZInput.ButtonDef def)) return "this game bind is not writable";

                    def.ResetBinding();
                    var zinput = ZInput.instance;
                    if (zinput != null) AccessTools.Method(typeof(ZInput), "Save")?.Invoke(zinput, null);

                    bind.Combo = DefaultOf(bind);
                    PersonalKeys.Forget(bind.Id);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: resetting {bind.Id} failed: {ex}");
                return "resetting the key failed, see the log";
            }

            // Back to the mod's own key means there is nothing personal left to keep.
            var fallback = DefaultOf(bind);
            return Apply(bind, fallback, SaveTarget.Profile);
        }

        /// <summary>
        /// Records the bind as yours, or hands it back to the profile. The reconciler writes with
        /// SaveTarget.Unrecorded, so re-applying a key never rewrites the file it came from.
        /// </summary>
        private static void Remember(BindEntry bind, SaveTarget target)
        {
            if (target == SaveTarget.Unrecorded || !PersonalKeys.Eligible(bind)) return;

            PersonalKeys.RecordRebind(bind.Id, bind.Combo, target == SaveTarget.Personal);
        }

        private static string ApplyConfig(BindEntry bind, KeyCombo combo, out bool wrote)
        {
            wrote = false;
            if (!(bind.Handle is ConfigEntryBase entry)) return "this bind has no config entry behind it";

            if (entry.SettingType == typeof(KeyboardShortcut))
            {
                entry.BoxedValue = combo.Main == KeyCode.None
                    ? KeyboardShortcut.Empty
                    : new KeyboardShortcut(combo.Main, combo.Modifiers);
            }
            else if (entry.SettingType == typeof(KeyCode))
            {
                entry.BoxedValue = combo.Main;
                if (combo.Modifiers.Length > 0)
                {
                    Save(entry);
                    bind.Combo = new KeyCombo(combo.Main, null);
                    wrote = true;
                    return $"{bind.OwnerName} stores a single key here, so the modifiers were dropped";
                }
            }
            else
            {
                return "this setting is not a key type";
            }

            Save(entry);
            bind.Combo = combo;
            wrote = true;
            return null;
        }

        private static string ApplyJotunn(BindEntry bind, KeyCombo combo, out bool wrote)
        {
            wrote = false;
            if (!(bind.Handle is ButtonConfig cfg)) return "this button has no config behind it";

            if (cfg.ShortcutConfig != null)
            {
                cfg.ShortcutConfig.Value = combo.Main == KeyCode.None
                    ? KeyboardShortcut.Empty
                    : new KeyboardShortcut(combo.Main, combo.Modifiers);
                Save(cfg.ShortcutConfig);
            }
            else if (cfg.Config != null)
            {
                cfg.Config.Value = combo.Main;
                Save(cfg.Config);
                if (combo.Modifiers.Length > 0)
                {
                    bind.Combo = new KeyCombo(combo.Main, null);
                    wrote = true;
                    return $"{bind.OwnerName} stores a single key here, so the modifiers were dropped";
                }
            }
            else
            {
                return "this button has no config behind it";
            }

            bind.Combo = combo;
            wrote = true;
            return null;
        }

        private static string ApplyVanilla(BindEntry bind, KeyCombo combo)
        {
            if (!(bind.Handle is ZInput.ButtonDef def)) return "this game bind is not writable";
            if (combo.Modifiers.Length > 0)
                return "the game stores one key per bind and has no modifier support, so pick a single key";

            var path = KeyPaths.ToPath(combo.Main);
            if (path == null) return $"the game has no input path for {combo.Main}";

            def.Rebind(path);

            var zinput = ZInput.instance;
            if (zinput != null) AccessTools.Method(typeof(ZInput), "Save")?.Invoke(zinput, null);

            bind.Combo = new KeyCombo(combo.Main, null);
            return null;
        }

        private static void Save(ConfigEntryBase entry)
        {
            var file = entry.ConfigFile;
            if (file == null) return;

            // Mods that write their own config on change would fight a global save, so only
            // flush when the file is not already saving on every assignment.
            var previous = file.SaveOnConfigSet;
            file.SaveOnConfigSet = false;
            file.Save();
            file.SaveOnConfigSet = previous;
        }
    }
}
