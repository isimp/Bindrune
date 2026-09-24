using System;
using HarmonyLib;

namespace Bindrune
{
    /// <summary>
    /// Keeps the start menu's own keyboard handling out of the way while the panel is open.
    ///
    /// Jotunn's input block only applies in the game scene, so nothing holds the menu back here,
    /// and the menu reads Return straight from ZInput rather than asking whether a text field has
    /// focus. Without this, a search typed at the start menu submits whatever the menu has
    /// selected, which starts the game when that is nothing.
    ///
    /// Held back for as long as the panel is open, not only while its search box has focus:
    /// Return ends the box's editing before the menu reads it in the same frame, so by then
    /// nothing is being typed any more. Also for the frame the panel closes in, so the Escape that
    /// closed it does not go on to the menu.
    ///
    /// The whole method is skipped rather than the Return branch alone: it handles the arrow keys
    /// and the gamepad's menu selection too, and none of those belong to the menu while the panel
    /// is in front of it.
    /// </summary>
    internal static class StartMenuKeys
    {
        public static void Patch(Harmony harmony)
        {
            try
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
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not hold the start menu's keys back while you type: {ex.Message}", ex);
            }
        }

        private static bool Skip() => !UI.BindrunePanel.HoldsKeyboard;
    }
}
