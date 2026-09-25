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
        /// <summary>Why a key only the Input System sees cannot go on a mod's bind.</summary>
        public const string UnseenByMods =
            "mods read keys through Unity's older input, which cannot see this key, so only the game's own controls can use it";

        /// <summary>
        /// Why a key cannot go on a bind at all, or null when it can. The one place this is
        /// decided, asked before a key is previewed, suggested or written, so a key that would be
        /// refused is refused the moment it is pressed rather than after it has been shown.
        /// </summary>
        public static string Refusal(BindEntry bind, KeyCombo combo)
        {
            if (!bind.Editable) return bind.ReadOnlyReason ?? "this bind cannot be changed from here";

            // A mod's setting holds a KeyCode, and a key known only by its path has none: written
            // anyway it would come out as no key at all, clearing the bind instead of setting it.
            if (bind.Source != BindSource.Vanilla) return combo.Main == KeyCode.None && combo.IsBound ? UnseenByMods : null;

            // The game's own format holds one key. A hotbar key can carry one modifier as well,
            // which Bindrune builds and keeps itself. See FixedKeys.
            var hotbar = bind.Handle is ZInput.ButtonDef def && FixedKeys.IsHotbar(def.Name);
            if (combo.Modifiers.Length > (hotbar ? 1 : 0))
                return hotbar
                    ? "a hotbar key can have one modifier at most"
                    : "the game stores one key per bind and has no modifier support, so pick a single key";

            if (combo.Modifiers.Length == 1 && KeyPaths.ToPath(combo.Modifiers[0]) == null)
                return $"the game has no input path for {combo.Modifiers[0]}";

            if (string.IsNullOrEmpty(combo.RawPath) && KeyPaths.ToPath(combo.Main) == null)
                return $"the game has no input path for {combo.Main}";

            return null;
        }

        /// <summary>Returns null on success, or a message explaining why nothing was written.</summary>
        public static string Apply(BindEntry bind, KeyCombo combo, SaveTarget target = SaveTarget.Personal)
        {
            var refusal = Refusal(bind, combo);
            if (refusal != null) return refusal;

            // The key in place until now, which a first key of yours replaces as the profile's.
            var before = bind.Combo;

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
                if (wrote) Remember(bind, target, before);
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

                    (FixedKeys.Live(def.Name) ?? def).ResetBinding();
                    if (FixedKeys.IsHotbar(def.Name)) FixedKeys.Forget(def.Name);
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
        /// <param name="before">The key the bind had before this write.</param>
        private static void Remember(BindEntry bind, SaveTarget target, KeyCombo before)
        {
            if (target == SaveTarget.Unrecorded || !PersonalKeys.Eligible(bind)) return;

            PersonalKeys.RecordRebind(bind.Id, bind.Combo, target == SaveTarget.Personal, before);
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

            // A key the older input has no KeyCode for arrives as its path already, in the form the
            // game stores its own rebinds in. Everything else is named by its KeyCode, including
            // the unbound combo, whose path is the None control the game parks cleared binds on.
            // Refusal has already made sure both paths exist.
            var path = !string.IsNullOrEmpty(combo.RawPath) ? combo.RawPath : KeyPaths.ToPath(combo.Main);
            var modifier = combo.Modifiers.Length == 1 ? KeyPaths.ToPath(combo.Modifiers[0]) : null;

            // A slot's own key and any key with a modifier would be dropped the next time the game
            // loads its controls, so Bindrune keeps those and puts them back. A plain key on an Alt
            // button is the game's to keep, which also takes it back from Bindrune. See FixedKeys.
            if (FixedKeys.IsDigit(def.Name) || (FixedKeys.IsAlt(def.Name) && modifier != null))
            {
                FixedKeys.Set(def.Name, modifier == null ? path : modifier + "+" + path);
            }
            else
            {
                if (FixedKeys.IsAlt(def.Name)) FixedKeys.Forget(def.Name);
                (FixedKeys.Live(def.Name) ?? def).Rebind(path);
            }

            var zinput = ZInput.instance;
            if (zinput != null) AccessTools.Method(typeof(ZInput), "Save")?.Invoke(zinput, null);

            // The bar and the game's prompts name the keys that reach each slot.
            if (FixedKeys.IsHotbar(def.Name)) FixedKeys.Relabel();

            // A cleared bind sits on the None control, which is a path like any other and would
            // otherwise read back as a key of that name.
            bind.Combo = !combo.IsBound ? KeyCombo.None
                : combo.Main != KeyCode.None ? new KeyCombo(combo.Main, combo.Modifiers)
                : new KeyCombo(KeyCode.None, combo.Modifiers, path);

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
