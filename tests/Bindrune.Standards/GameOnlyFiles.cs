using System.Collections.Generic;

namespace Bindrune.Standards
{
    /// <summary>
    /// The files under src/ the unit tests leave out, as tests/Bindrune.Tests/Bindrune.Tests.csproj
    /// excludes them, each with why it needs the game. The self-test reaches these in game.
    /// </summary>
    internal static class GameOnlyFiles
    {
        public static readonly Dictionary<string, string> List = new Dictionary<string, string>
        {
            ["src/Plugin.cs"] = "the plugin class, which BepInEx starts with the game; tests/Bindrune.Tests/PluginShim.cs stands in for it",
            ["src/UI/**"] = "the panel, drawn with Jotunn's GUIManager and Unity UI",
            ["src/Hints/HintOverlay.cs"] = "the hints box on screen, drawn with Unity UI",
            ["src/Hints/HintDrag.cs"] = "drags the hints box by Unity's pointer events",
            ["src/StartMenuKeys.cs"] = "holds back the start menu's keys through a Harmony patch on the game",
        };
    }
}
