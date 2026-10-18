using System.Collections.Generic;
using Bindrune.Conflicts;
using Bindrune.Context;
using Bindrune.Personal;

namespace Bindrune
{
    /// <summary>
    /// What the panel says about a bind in words: whom its key is shared with, where its
    /// situations come from, how it reads modifiers and where it was found, and the profile's key
    /// beside yours. Kept apart from the drawing, which needs the game, so the tests reach it.
    /// </summary>
    internal static class BindText
    {
        /// <summary>The note under a free key that other binds use too, which the panel offers because they should not interfere.</summary>
        public static string SharedWith(List<Conflict> shared, BindEntry bind)
        {
            if (shared.Count > 1) return $"Shared with {shared.Count} binds, none of which should interfere.";

            var other = shared[0].A.Id == bind.Id ? shared[0].B : shared[0].A;
            return $"Shared with {other.OwnerName}'s \"{other.Label}\", which should not interfere.";
        }

        /// <summary>
        /// What the profile would put here instead, shown only when it actually differs - so the
        /// column stays empty for the many binds nobody has touched.
        /// </summary>
        public static string ProfileKeyNote(BindEntry bind, bool yours)
        {
            if (!yours) return "";

            var entry = PersonalKeys.Get(bind.Id);
            if (entry == null || !entry.Profile.IsBound || entry.Profile.Equals(bind.Combo)) return "";

            return "profile: " + KeyLabels.Of(entry.Profile);
        }

        /// <summary>
        /// Whose answer is on show, and whether clicking will change it. A mod that describes its
        /// own bind is final: its author knows when their code runs, and no amount of guessing
        /// from the outside beats that. Every other source is a starting point you can take over.
        /// </summary>
        public static string Provenance(SituationSource source, string ownerName)
        {
            switch (source)
            {
                case SituationSource.Mod:
                    return $"{ownerName} describes this bind itself, so these cannot be changed here.";
                case SituationSource.KeyHints:
                    return $"Taken from {ownerName}'s own key hints. Change anything to make it yours instead.";
                case SituationSource.Pack:
                    return "From the shared list that travels with this profile. Change anything to make it yours instead.";
                case SituationSource.Game:
                    return "What the game does with its own bind. Change anything to make it yours instead.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// What is known about this bind, kept to what its storage proves. A setting holding one
        /// key cannot require a modifier - but its owner may still look at Alt itself, and no
        /// amount of reading the config would show that, so the wording says so rather than
        /// stating a behaviour we have not seen.
        /// </summary>
        public static string Behaviour(BindEntry bind)
        {
            string modifiers;
            if (bind.Source == BindSource.Gamepad)
                modifiers = "A gamepad button, read from whichever controller layout you picked. Modifiers do not enter into it.";
            else if (bind.Modifiers == ModifierBehavior.Required)
                modifiers = "Fires while this modifier is held, whatever else is held with it.";
            else if (bind.Modifiers == ModifierBehavior.Strict)
                modifiers = "Needs exactly these modifiers, and will not fire while any other key is held (including movement keys).";
            else if (bind.Modifiers == ModifierBehavior.Unknown)
                modifiers = "Stored as text, so whether modifiers matter here cannot be told from the setting.";
            else if (bind.Source == BindSource.Vanilla || bind.Source == BindSource.Jotunn)
                modifiers = "A single key, read without looking at modifiers, so it fires while Alt, Ctrl or Shift are held too.";
            else
                modifiers = "A single key, with no modifier to match, so it fires while Alt, Ctrl or Shift are held - " +
                            "unless the mod checks them in its own code, which the setting does not show.";

            return $"{modifiers}\nFound as: {SourceLabel(bind.Source)}.";
        }

        /// <summary>Where the bind came from, said in words rather than in the enum's name.</summary>
        public static string SourceLabel(BindSource source)
        {
            switch (source)
            {
                case BindSource.Vanilla: return "one of the game's own controls";
                case BindSource.Gamepad: return "one of the game's gamepad buttons";
                case BindSource.Jotunn: return "a mod button registered through Jotunn";
                case BindSource.ModTyped: return "a keybind setting in the mod's config";
                default: return "a text setting in the mod's config that reads as a key";
            }
        }
    }
}
