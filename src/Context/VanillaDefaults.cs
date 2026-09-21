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
        private static readonly string[] Movement = { "World", "Vehicle" };

        private static readonly string[] Everywhere =
        {
            "World", "Vehicle", "Build placement", "Build menu", "Inventory", "Container", "Crafting", "Map", "Chat"
        };

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

            // A key that opens a screen is read while that screen is still closed, so it is live
            // where you open it from as well as in the screen itself. Tagging these with the
            // screen alone would pass any world bind on the same key as never live alongside them.
            { "BuildMenu", new[] { "Build placement", "Build menu" } },
            { "AltPlace", Build },
            { "Remove", Build },
            { "TabLeft", Build },
            { "TabRight", Build },

            // Opened from the world, closed from any of the screens the inventory panel shows.
            { "Inventory", new[] { "World", "Inventory", "Container", "Crafting" } },

            { "Map", new[] { "World", "Map" } },
            { "MapZoomIn", Map },
            { "MapZoomOut", Map },

            { "Chat", new[] { "World", "Chat" } },
            { "ChatUp", Chat },
            { "ChatDown", Chat },
            { "ScrollChatUp", Chat },
            { "ScrollChatDown", Chat },

            { "Hotbar1", World }, { "Hotbar2", World }, { "Hotbar3", World }, { "Hotbar4", World },
            { "Hotbar5", World }, { "Hotbar6", World }, { "Hotbar7", World }, { "Hotbar8", World },

            // The keys the game reads in its own code (GameKeys), polled whatever screen is open.
            { "Network panel", Everywhere }, { "Gamepad layout", Everywhere }, { "Screenshot", Everywhere },
            { "Mouse capture", Everywhere }, { "Hide HUD", Everywhere }
        };
    }
}
