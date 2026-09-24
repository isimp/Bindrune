using BepInEx.Configuration;

namespace Bindrune.Personal
{
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
    /// One line of the keys section of bindrune.keys: the bind id, your key, the profile's key and
    /// 1 while yours is in use, tab separated, keys written the way BepInEx writes a
    /// KeyboardShortcut. Keepsake reads this section to take over your keys while Bindrune is not
    /// installed, so a change to it means a new StateVersion and a matching Keepsake. See
    /// tests/contract.
    /// </summary>
    public static class KeyLines
    {
        /// <summary>The first line of bindrune.keys.</summary>
        public const string StateVersion = "# bindrune state v3";

        public static bool TryParse(string line, out string id, out PersonalEntry entry)
        {
            id = null;
            entry = null;

            var parts = line.Split('\t');
            if (parts.Length < 2) return false;

            id = parts[0];
            entry = new PersonalEntry
            {
                Personal = ParseCombo(parts[1]),
                Profile = parts.Length > 2 ? ParseCombo(parts[2]) : KeyCombo.None,
                // A line without the flag, for example one written by hand, counts as active.
                Active = parts.Length < 4 || parts[3] == "1"
            };
            return true;
        }

        public static string Format(string id, PersonalEntry entry) =>
            $"{id}\t{FormatCombo(entry.Personal)}\t{FormatCombo(entry.Profile)}\t{(entry.Active ? "1" : "0")}";

        public static KeyCombo ParseCombo(string text)
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

        public static string FormatCombo(KeyCombo combo) =>
            combo.IsBound ? new KeyboardShortcut(combo.Main, combo.Modifiers).Serialize() : "none";
    }
}
