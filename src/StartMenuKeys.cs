using HarmonyLib;

namespace Bindrune
{
    /// <summary>
    /// Keeps the start menu's own keyboard handling out of the way while you are typing in the
    /// panel.
    ///
    /// Jotunn's input block only applies in the game scene, so nothing holds the menu back here,
    /// and the menu reads Return straight from ZInput rather than asking whether a text field has
    /// focus. Without this, a search typed at the start menu submits whatever the menu has
    /// selected, which starts the game when that is nothing.
    ///
    /// The whole method is skipped rather than the Return branch alone: it handles the arrow keys
    /// and the gamepad's menu selection too, and none of those belong to the menu while the text
    /// you are typing belongs to us.
    /// </summary>
    internal static class StartMenuKeys
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(FejdStartup), "UpdateKeyboard");
            if (target == null)
            {
                Plugin.WarnOnce("Bindrune: the start menu's keyboard handling was not found, so Return " +
                                "may reach the menu while you type in the panel.");
                return;
            }

            harmony.Patch(target, new HarmonyMethod(AccessTools.Method(typeof(StartMenuKeys), nameof(Skip))));
        }

        private static bool Skip() => !UI.BindrunePanel.Typing;
    }
}
