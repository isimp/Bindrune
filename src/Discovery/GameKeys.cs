using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bindrune.Discovery
{
    /// <summary>
    /// Keys the game reads straight from the keyboard in its own code, with no button behind them
    /// that could be rebound. Listed as the game's binds so they clash like any other, and
    /// locked, since nothing can move them.
    ///
    /// Found in Valheim 1.0.7's code, and only the ones read for a player in the world: keys it
    /// reads only in debug mode, in menus or inside a dialog are left out. Each is polled every
    /// frame by the HUD or the camera, whatever screen is open, which is why VanillaDefaults
    /// tags them as live everywhere.
    /// </summary>
    public static class GameKeys
    {
        private class Read
        {
            public readonly string Name;
            public readonly KeyCombo Combo;
            public readonly string What;

            /// <summary>A mod setting that replaces this read when it exists, or null.</summary>
            public readonly string TakenOverBy;

            public Read(string name, KeyCode key, KeyCode? modifier, string what, string takenOverBy = null)
            {
                Name = name;
                Combo = new KeyCombo(key, modifier.HasValue ? new[] { modifier.Value } : null);
                What = what;
                TakenOverBy = takenOverBy;
            }
        }

        private static readonly Read[] Reads =
        {
            // Extra Slots patches the panel's F2 check to read its own setting instead, so with it
            // installed that setting is the key, and it is already listed as Extra Slots' bind.
            new Read("Network panel", KeyCode.F2, null, "Opens the network panel.",
                BindIds.Config("shudnal.ExtraSlots", "Mods compatibility", "Rebind Connect Panel")),
            new Read("Gamepad layout", KeyCode.F9, null, "Switches to the next gamepad layout."),
            new Read("Screenshot", KeyCode.F11, null, "Takes a screenshot."),
            new Read("Mouse capture", KeyCode.F1, KeyCode.LeftControl, "Frees or captures the mouse."),
            new Read("Hide HUD", KeyCode.F3, KeyCode.LeftControl, "Hides or shows the HUD.")
        };

        /// <param name="found">The binds found so far, to tell which reads a mod has taken over.</param>
        public static List<BindEntry> Scan(IEnumerable<BindEntry> found)
        {
            var ids = new HashSet<string>(found.Select(b => b.Id));

            return Reads
                .Where(r => r.TakenOverBy == null || !ids.Contains(r.TakenOverBy))
                .Select(r => new BindEntry
                {
                    Id = BindIds.Game(r.Name),
                    OwnerName = "Valheim",
                    OwnerGuid = "vanilla",
                    Label = r.Name,
                    Section = "Game",
                    Description = r.What + " The game reads this key in its own code, not through a control.",
                    Source = BindSource.Vanilla,
                    // The game checks that Ctrl is down and nothing else, so a Ctrl read does not
                    // fire on F3 alone, while a plain read fires whatever is held with it.
                    Modifiers = r.Combo.Modifiers.Length > 0 ? ModifierBehavior.Strict : ModifierBehavior.SingleKey,
                    Combo = r.Combo,
                    Editable = false,
                    ReadOnlyReason = "the game reads this key in its own code, so it cannot be rebound anywhere"
                })
                .ToList();
        }
    }
}
