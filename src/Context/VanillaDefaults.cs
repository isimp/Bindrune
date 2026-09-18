using System.Collections.Generic;

namespace Bindrune.Context
{
    /// <summary>
    /// What the game's own binds are for, hand-checked, so a fresh install is useful before you
    /// have marked anything. Read live by KnownSituations rather than copied into your own
    /// situations: it stays clear whose answer it is, and anything you set overrides it.
    ///
    /// Doubles as the list of controls a player is meant to see, which is how VanillaScanner
    /// tells a real bind from the raw key aliases the UI reads.
    ///
    /// Only binds whose situation is unambiguous are listed. Console, Esc and the raw mouse
    /// aliases are deliberately absent: a wrong default is worse than none, because it would
    /// silence real conflicts.
    /// </summary>
    public static class VanillaDefaults
    {
        private static readonly string[] World = { "World" };
        private static readonly string[] Build = { "Build placement" };
        private static readonly string[] Map = { "Map" };
        private static readonly string[] Chat = { "Chat" };
        private static readonly string[] Inventory = { "Inventory" };
        private static readonly string[] Movement = { "World", "Vehicle" };

        public static readonly Dictionary<string, string[]> ByName = new Dictionary<string, string[]>
        {
            { "Forward", Movement },
            { "Backward", Movement },
            { "Left", Movement },
            { "Right", Movement },
            { "Jump", World },
            { "Crouch", World },
            { "Run", World },
            { "AutoRun", World },
            { "Sit", World },
            { "Hide", World },
            { "ToggleWalk", World },
            { "AutoPickup", World },
            { "Attack", World },
            { "SecondaryAttack", World },
            { "Block", World },
            { "OpenEmote", World },
            { "OpenRadial", World },

            // Interacting opens chests and stations, so Use is live in those too.
            { "Use", new[] { "World", "Container", "Crafting" } },

            { "BuildMenu", new[] { "Build menu" } },
            { "AltPlace", Build },
            { "Remove", Build },
            { "TabLeft", Build },
            { "TabRight", Build },

            { "Inventory", Inventory },

            { "Map", Map },
            { "MapZoomIn", Map },
            { "MapZoomOut", Map },

            { "Chat", Chat },
            { "ChatUp", Chat },
            { "ChatDown", Chat },
            { "ScrollChatUp", Chat },
            { "ScrollChatDown", Chat },

            { "Hotbar1", World }, { "Hotbar2", World }, { "Hotbar3", World }, { "Hotbar4", World },
            { "Hotbar5", World }, { "Hotbar6", World }, { "Hotbar7", World }, { "Hotbar8", World }
        };
    }
}
