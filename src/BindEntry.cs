using System;

namespace Bindrune
{
    /// <summary>
    /// Where a bind was found, which is also what decides how it can be written back. Ordered
    /// from most to least the owner told us: a ZInput button and a Jotunn button announce
    /// themselves, a typed config entry is unmistakable, and free text is our reading of it.
    /// </summary>
    public enum BindSource
    {
        /// <summary>Vanilla game button registered in ZInput.</summary>
        Vanilla,
        /// <summary>A vanilla gamepad button. Listed to be read: the game cannot rebind these.</summary>
        Gamepad,
        /// <summary>Registered through Jotunn's InputManager by a mod.</summary>
        Jotunn,
        /// <summary>A mod's BepInEx config entry typed KeyboardShortcut or KeyCode.</summary>
        ModTyped,
        /// <summary>A mod's string config entry whose text reads as a key combo.</summary>
        ModText
    }

    /// <summary>
    /// What is known about modifiers on this bind, which decides whether one actually protects it.
    /// Only ever what the storage proves, never a guess about the mod's own code: a setting that
    /// cannot express a modifier cannot require one, but nothing stops its owner from checking
    /// Alt separately, and nothing here can see that.
    /// </summary>
    public enum ModifierBehavior
    {
        /// <summary>A KeyboardShortcut: its owner only reports it pressed on an exact match.</summary>
        Strict,
        /// <summary>One key, with no modifier field to match against.</summary>
        SingleKey,
        /// <summary>Free text we parsed. How its owner reads it is not visible from here.</summary>
        Unknown
    }


    public class BindEntry
    {
        /// <summary>Stable identity across restarts and rescans. Built by BindIds; never parsed.</summary>
        public string Id;
        public string OwnerName;
        public string OwnerGuid;
        public string Label;
        public string Section;
        public string Description;

        public BindSource Source;
        public ModifierBehavior Modifiers;
        public KeyCombo Combo;

        /// <summary>
        /// A game button the player never sees: a raw key alias the UI reads, or an action the
        /// settings screen does not list. Hidden unless asked for, and kept out of conflicts.
        /// </summary>
        public bool Internal;

        /// <summary>
        /// Whether this bind takes part in conflict analysis. False for binds that are listed to
        /// be read but cannot meaningfully clash with what is around them - a gamepad button is
        /// on another device entirely, and nothing about it can be changed from here anyway.
        /// </summary>
        public bool Compared = true;

        public bool Editable;
        /// <summary>Why it is not editable, shown in the UI. Null when editable.</summary>
        public string ReadOnlyReason;

        /// <summary>
        /// The thing this bind was read from, so a rebind can write back to it: a ConfigEntryBase,
        /// a Jotunn ButtonConfig, or a ZInput.ButtonDef depending on Source.
        /// </summary>
        public object Handle;

        public override string ToString() => $"{OwnerName} / {Label} = {Combo}";
    }
}
