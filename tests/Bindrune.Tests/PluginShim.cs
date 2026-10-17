using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using UnityEngine;

namespace Bindrune
{
    /// <summary>
    /// Stands in for the plugin class, which needs the game to exist, so the code that reports
    /// through it or reads its settings can be tested. The settings answer with their defaults,
    /// and warnings are collected for the tests to look at.
    /// </summary>
    internal static class Plugin
    {
        public static readonly ManualLogSource Log = new ManualLogSource("Bindrune");

        public static readonly List<string> Warnings = new List<string>();

        public static void WarnOnce(string message, Exception detail = null,
            [CallerFilePath] string file = null, [CallerLineNumber] int line = 0) => Warnings.Add(message);

        public static float ScrollSpeed => 300f;
        /// <summary>
        /// Off, though the setting's default is on: names by keyboard layout come from asking the
        /// game's keyboard, which the stubs in lib/ cannot answer, so the tests read keys by Unity's
        /// names. What is made of the keyboard's answer is tested through KeyLabels.Label, and the
        /// self-test's Key names part asks the keyboard in game.
        /// </summary>
        public static bool KeyboardLabels { get; set; }
        public static Vector2 PanelSize { get; set; }
        public static bool HintsVisible { get; set; }
        public static int HintFontSize => 15;
        public static bool HintModNames { get; set; }
        public static Vector2 HintPosition { get; set; }
        public static string HintsKeyText => "the hints key";

        public static string ResolveModName(string guid) => string.IsNullOrEmpty(guid) ? "unknown" : guid;
    }
}
