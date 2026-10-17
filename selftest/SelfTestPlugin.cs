using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace Bindrune.SelfTest
{
    /// <summary>
    /// The self-test as a plugin of its own beside Bindrune, which only a development build
    /// installs, so a player's Bindrune holds nothing of it. <c>bindrune selftest</c>, typed in the
    /// console or with a slash in chat, tries Bindrune out in the world and writes what it found
    /// to <c>BepInEx/Bindrune-selftest.log</c>. With the file <c>BepInEx/Bindrune-selftest.run</c>
    /// present it starts by itself a few seconds after your character first appears in a session.
    /// </summary>
    [BepInPlugin(Guid, "Bindrune self-test", "1.0.0")]
    [BepInDependency(Plugin.Guid)]
    [BepInProcess("valheim.exe")]
    public sealed class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Bindrune.SelfTest";

        private const string Word = "selftest";

        /// <summary>How long after your character appears a run asked for by the marker file starts.</summary>
        private const float SettleSeconds = 5f;

        private static string MarkerFile => Path.Combine(Paths.BepInExRootPath, "Bindrune-selftest.run");

        private SelfTest _running;
        private bool _markerDone;
        private float _spawnedAt = -1f;

        private void Awake()
        {
            _ = new Terminal.ConsoleCommand("bindrune", Word + " - tries Bindrune out in this world and writes BepInEx/Bindrune-selftest.log", args =>
            {
                if (args.Length < 2 || !string.Equals(args[1], Word, StringComparison.OrdinalIgnoreCase))
                {
                    args.Context.OrNull()?.AddString("bindrune " + Word + " - tries Bindrune out in this world and writes BepInEx/Bindrune-selftest.log");
                    return;
                }

                args.Context.OrNull()?.AddString(Start("it was asked for with bindrune " + Word));
            }, optionsFetcher: () => new System.Collections.Generic.List<string> { Word });
        }

        private void Update()
        {
            if (_markerDone || _running != null) return;

            if (Player.m_localPlayer == null)
            {
                _spawnedAt = -1f;
                return;
            }

            if (_spawnedAt < 0f) _spawnedAt = Time.realtimeSinceStartup;
            if (Time.realtimeSinceStartup - _spawnedAt < SettleSeconds) return;

            _markerDone = true;
            if (File.Exists(MarkerFile)) Start("the marker file is present");
        }

        /// <summary>Starts a run unless one is going or there is no world to run in, and says which.</summary>
        private string Start(string why)
        {
            if (_running != null) return "The Bindrune self-test is already running.";
            if (Player.m_localPlayer == null) return "The Bindrune self-test runs in a world, with your character in it.";

            _running = new SelfTest(Config, Logger);
            StartCoroutine(Finish(_running.Run(why)));
            return "The Bindrune self-test is running; it takes about half a minute.";
        }

        private System.Collections.IEnumerator Finish(System.Collections.IEnumerator run)
        {
            yield return run;
            _running = null;
        }
    }
}
