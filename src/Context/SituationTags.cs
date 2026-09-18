using System.Collections.Generic;

namespace Bindrune.Context
{
    /// <summary>
    /// The words Bindrune describes a bind with. Three of them, used consistently everywhere:
    ///
    ///   situation   everything that has to be true for a bind to be live. A set of tags.
    ///   where       the part of a situation that names a place or a mode: this list.
    ///   held item   the part that names something in your hands, prefixed by EquippedItems.
    ///
    /// "Where" and "held item" are two independent axes, not rival answers: you hold a pickaxe
    /// in the world. Only an axis where both binds say something can rule a clash out.
    ///
    /// A fixed vocabulary is what makes two binds comparable at all: "build menu" and
    /// "buildmenu" would never match as free text.
    /// </summary>
    public static class SituationTags
    {
        /// <summary>Every "where" tag, in the order the panel offers them.</summary>
        public static readonly string[] All =
        {
            "World", "Build placement", "Build menu", "Inventory", "Container",
            "Crafting", "Map", "Chat", "Vehicle",
            // For situations this list does not cover: a mod's own mode. Two binds both marked
            // Custom count as sharing a situation.
            "Custom"
        };

        /// <summary>
        /// Maps a context - the label ContextIndex derives from the game's own code, naming the
        /// class that reads a button - onto the same vocabulary you pick from, so an auto-detected
        /// vanilla bind and a hand-set mod tag can be compared. Contexts with no equivalent stay
        /// unmapped and simply do not participate.
        /// </summary>
        private static readonly Dictionary<string, string> FromContexts = new Dictionary<string, string>
        {
            { "playing", "World" },
            { "building", "Build placement" },
            { "inventory", "Inventory" },
            { "store", "Crafting" },
            { "map", "Map" },
            { "chat", "Chat" }
        };

        public static string FromContext(string context) =>
            context != null && FromContexts.TryGetValue(context, out var tag) ? tag : null;
    }
}
