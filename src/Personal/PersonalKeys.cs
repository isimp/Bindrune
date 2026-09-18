using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using Bindrune.Discovery;

namespace Bindrune.Personal
{
    /// <summary>Which of a bind's two keys a write belongs to.</summary>
    public enum SaveTarget
    {
        /// <summary>Yours: kept when the profile syncs.</summary>
        Personal,
        /// <summary>The profile's: shared with everyone on it, and overwritten by a sync.</summary>
        Profile,
        /// <summary>
        /// Neither. The key is written to the mod, but nothing is recorded against it, because
        /// this write is not a new choice: the reconciler putting a remembered key back, or a
        /// switch between the two sides, both of which are already written down.
        /// </summary>
        Unrecorded
    }

    /// <summary>
    /// A bind can hold two keys at once: the one you chose, and the one the shared profile says.
    /// Both are remembered so switching between them restores the other rather than overwriting it.
    /// </summary>
    public class PersonalEntry
    {
        public KeyCombo Personal;
        public KeyCombo Profile;

        /// <summary>True while your key is the one actually written to the mod's config.</summary>
        public bool Active;
    }

    /// <summary>
    /// Keys you set that must survive a profile sync.
    ///
    /// Gale rewrites and deletes files under BepInEx/config on every pull, which is where mod
    /// keybinds live, so a subscriber's own rebinds are wiped each launch. This file sits in the
    /// BepInEx root with an extension outside Gale's allowlist - both are required for it to be
    /// skipped by the same predicate Gale uses for export and for deletion. LogOutput.log lives
    /// here for the same machine-local reason.
    /// </summary>
    public static class PersonalKeys
    {
        private const string Version = "# bindrune personal keys v2";

        private static Dictionary<string, PersonalEntry> _entries;

        /// <summary>
        /// Keys put back since the game started, by bind id so repeated reconciles do not list
        /// the same one twice. Session-only, because it reports what happened this session.
        /// </summary>
        private static readonly Dictionary<string, string> Restored = new Dictionary<string, string>();

        public static int RestoredCount => Restored.Count;

        public static IEnumerable<string> RestoredKeys => Restored.Values;

        private static string FilePath => Path.Combine(Paths.BepInExRootPath, "bindrune.keys");

        private static Dictionary<string, PersonalEntry> Entries
        {
            get
            {
                if (_entries == null) Load();
                return _entries;
            }
        }

        /// <summary>How many binds are currently using your key rather than the profile's.</summary>
        public static int Count => Entries.Values.Count(e => e.Active);

        /// <summary>True when your key is the live one for this bind.</summary>
        public static bool IsPersonal(string bindId) =>
            Entries.TryGetValue(bindId, out var entry) && entry.Active;

        public static PersonalEntry Get(string bindId) =>
            Entries.TryGetValue(bindId, out var entry) ? entry : null;

        /// <summary>Only binds whose value lives in the synced profile can be taken away by a sync.</summary>
        public static bool Eligible(BindEntry bind) =>
            bind != null && bind.Editable &&
            (bind.Source == BindSource.ModTyped || bind.Source == BindSource.Jotunn);

        /// <summary>Records a rebind against whichever side is live, leaving the other side untouched.</summary>
        public static void RecordRebind(string bindId, KeyCombo combo, bool personal)
        {
            var entry = Entry(bindId);

            if (personal)
            {
                entry.Personal = combo;
                entry.Active = true;
            }
            else
            {
                entry.Profile = combo;
                entry.Active = false;

                // With no key of yours to keep, there is nothing left worth remembering.
                if (!entry.Personal.IsBound)
                {
                    Entries.Remove(bindId);
                    Save();
                    return;
                }
            }

            Save();
        }

        /// <summary>
        /// Makes your key the live one, putting it back if you had set one before. The key that
        /// was live until now is kept as the profile's, so switching back returns to it.
        /// </summary>
        public static void UsePersonal(BindEntry bind)
        {
            var entry = Entry(bind.Id);

            // While the profile side is live, the config holds the profile's key by definition,
            // so take it now: a sync may have changed it since we last looked.
            if (!entry.Active) entry.Profile = bind.Combo;

            if (entry.Personal.IsBound) BindWriter.Apply(bind, entry.Personal, SaveTarget.Unrecorded);
            else entry.Personal = bind.Combo;

            entry.Active = true;
            Save();
        }

        /// <summary>Hands the bind back to the profile, keeping your key aside for later.</summary>
        public static void UseProfile(BindEntry bind)
        {
            var entry = Get(bind.Id);
            if (entry == null) return;

            if (entry.Active && entry.Personal.IsBound) entry.Personal = bind.Combo;
            if (entry.Profile.IsBound) BindWriter.Apply(bind, entry.Profile, SaveTarget.Unrecorded);

            entry.Active = false;
            Save();
        }

        /// <summary>Drops both keys, used when a bind goes back to what the mod shipped with.</summary>
        public static void Forget(string bindId)
        {
            if (Entries.Remove(bindId)) Save();
        }

        /// <summary>
        /// Puts every active key back. Runs at startup and on every rescan, because mods bind
        /// their configs at different times and there is no single moment when all of them exist.
        /// A no-op when nothing has changed. Returns how many recorded binds were not loaded yet,
        /// so callers know whether it is worth looking again.
        /// </summary>
        public static int Reconcile()
        {
            if (Entries.Count == 0) return 0;

            int reapplied = 0, missing = 0;
            var dirty = false;

            foreach (var pair in Entries.ToList())
            {
                var entry = pair.Value;
                if (!entry.Active || !entry.Personal.IsBound) continue;

                var bind = BindRegistry.All.FirstOrDefault(b => b.Id == pair.Key);
                if (bind == null)
                {
                    missing++;
                    continue;
                }

                if (!Eligible(bind) || bind.Combo.Equals(entry.Personal)) continue;

                // Whatever replaced your key is what the profile now says, so remember it before
                // overwriting: that is the key you get back by switching to the profile side.
                entry.Profile = bind.Combo;
                dirty = true;

                var problem = BindWriter.Apply(bind, entry.Personal, SaveTarget.Unrecorded);
                if (problem == null)
                {
                    reapplied++;
                    Restored[pair.Key] = $"{bind.OwnerName} / {bind.Label} - back to {entry.Personal}, the profile had {entry.Profile}";
                    Plugin.Log.LogInfo($"Bindrune: restored your key {entry.Personal} for {bind.OwnerName} / {bind.Label} (the profile said {entry.Profile}).");
                }
                else
                {
                    Plugin.Log.LogWarning($"Bindrune: could not restore {bind.OwnerName} / {bind.Label}: {problem}");
                }
            }

            if (dirty) Save();

            if (reapplied > 0 || missing > 0)
                Plugin.Log.LogInfo($"Bindrune: personal keys - {Count} active, {reapplied} reapplied, {missing} for binds not loaded.");

            return missing;
        }

        /// <summary>Recorded binds that no mod currently provides: uninstalled, disabled, or renamed.</summary>
        public static IEnumerable<string> Orphans() =>
            Entries.Where(e => e.Value.Active && BindRegistry.All.All(b => b.Id != e.Key)).Select(e => e.Key);

        private static PersonalEntry Entry(string bindId)
        {
            if (!Entries.TryGetValue(bindId, out var entry))
                Entries[bindId] = entry = new PersonalEntry { Personal = KeyCombo.None, Profile = KeyCombo.None };

            return entry;
        }

        private static void Load()
        {
            _entries = new Dictionary<string, PersonalEntry>();

            foreach (var line in TextStore.Read(FilePath))
            {
                if (TextStore.IsNoise(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 2) continue;

                _entries[parts[0]] = new PersonalEntry
                {
                    Personal = Parse(parts[1]),
                    Profile = parts.Length > 2 ? Parse(parts[2]) : KeyCombo.None,
                    // A line without the flag, for example one written by hand, counts as active.
                    Active = parts.Length < 4 || parts[3] == "1"
                };
            }

            if (_entries.Count > 0) Plugin.Log.LogInfo($"Bindrune: {Count} personal keys loaded.");
        }

        private static KeyCombo Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text == "none") return KeyCombo.None;

            try
            {
                var shortcut = KeyboardShortcut.Deserialize(text);
                return new KeyCombo(shortcut.MainKey, shortcut.Modifiers);
            }
            catch
            {
                return KeyCombo.None;
            }
        }

        private static string Serialize(KeyCombo combo) =>
            combo.IsBound ? new KeyboardShortcut(combo.Main, combo.Modifiers).Serialize() : "none";

        private static void Save()
        {
            // Atomic: this is the one file holding keys the user set by hand and cannot get back
            // from anywhere else, so a crash mid-write must not be able to truncate it.
            TextStore.Write(FilePath,
                new[]
                {
                    Version,
                    "# bind id, your key, the profile's key, whether yours is live.",
                    "# Kept out of BepInEx/config on purpose so profile syncs cannot delete it."
                },
                Entries.OrderBy(e => e.Key).Select(e =>
                    $"{e.Key}\t{Serialize(e.Value.Personal)}\t{Serialize(e.Value.Profile)}\t{(e.Value.Active ? "1" : "0")}"),
                atomic: true);
        }
    }
}
