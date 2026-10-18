using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Bindrune.Context;
using Bindrune.Hints;
using Bindrune.Personal;
using Bindrune.UI;
using UnityEngine;

namespace Bindrune.SelfTest
{
    /// <summary>
    /// The in-game self-test: parts that use Bindrune as a player does, through the paths the
    /// panel, the hints key and the game itself take, and check what it did, in the game as it
    /// runs: whether its patches are in, whether the game still has what it reads by name, which
    /// binds it finds, how it judges clashes, what your own key and the profile's do through a
    /// sync, the hotbar keys, the panel's search, the hints and the key names.
    ///
    /// Safe on a real profile. The binds it judges and rebinds are settings of its own, bound in
    /// its own config file for the run and removed after. What it changes of yours (a hotbar
    /// key, a hint, a mute, a situation) it undoes through the same paths, and it checks that
    /// <c>bindrune.keys</c> and <c>situations.txt</c> hold what they held before; where they do
    /// not, the copies taken first are put back and the run says so. The panel and the hints are
    /// left open or closed as they were.
    /// </summary>
    internal sealed partial class SelfTest
    {
        private readonly ConfigFile _config;
        private readonly ManualLogSource _log;
        private readonly List<Check> _checks = new List<Check>();
        private string _part = "Self-test";

        public SelfTest(ConfigFile config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        private static string LogFile => Path.Combine(Paths.BepInExRootPath, "Bindrune-selftest.log");

        /// <summary>The parts, in the order they run: later ones lean on the binds and the panel earlier ones set up.</summary>
        private IEnumerable<(string Name, Func<IEnumerator> Part)> Parts()
        {
            yield return ("Game", Game);
            yield return ("Finding binds", Finding);
            yield return ("Clashes", Clashes);
            yield return ("Situations you set", SituationsYouSet);
            yield return ("Muting", Muting);
            yield return ("Free keys", FreeKeySuggestions);
            yield return ("Your key and the profile's", YoursAndTheProfiles);
            yield return ("What a bind takes", Refusals);
            yield return ("Hotbar keys", HotbarKeys);
            yield return ("Panel search", PanelSearch);
            yield return ("Every bind's detail", EveryDetail);
            yield return ("Hints", HintsOnScreen);
            yield return ("Hints by where you are", HintsBySituation);
            yield return ("Key names", KeyNames);
            yield return ("Game code", GameCode);
            yield return ("Jotunn buttons", JotunnBinds);
        }

        public IEnumerator Run(string why)
        {
            var clock = Stopwatch.StartNew();
            var started = DateTime.Now;
            Say("Bindrune self-test: it binds settings of its own for a moment, opens the panel, and puts back what it changes. About half a minute.");

            var before = Snapshot.Take();
            try
            {
                foreach (var (name, part) in Parts())
                    yield return Guarded(name, part);
            }
            finally
            {
                _part = "Putting back";
                PutBack(before);
                Write(why, started, clock.Elapsed);
            }
        }

        /// <summary>Runs one part a step at a time, so one that throws fails with what it threw, and its own cleanup still runs.</summary>
        private IEnumerator Guarded(string name, Func<IEnumerator> part)
        {
            _part = name;
            var clock = Stopwatch.StartNew();
            var steps = part();
            while (true)
            {
                object step;
                try
                {
                    if (!steps.MoveNext()) break;
                    step = steps.Current;
                }
                catch (Exception ex)
                {
                    // A part that throws fails, with what it threw, and the run goes on to the next.
                    Fail("ran to the end", ex.GetType().Name + ": " + ex.Message);
                    (steps as IDisposable)?.Dispose();
                    break;
                }
                yield return step;
            }
            Note("took", Ms(clock.Elapsed) + " ms");
        }

        // ---------- checks ----------

        private void Check(bool held, string what, string detail = null) =>
            _checks.Add(new Check(_part, what, held ? Outcome.Pass : Outcome.Fail, detail));

        private void Fail(string what, string detail) => _checks.Add(new Check(_part, what, Outcome.Fail, detail));

        private void Warn(string what, string detail) => _checks.Add(new Check(_part, what, Outcome.Warn, detail));

        private void Skip(string what, string why) => _checks.Add(new Check(_part, what, Outcome.Skip, why));

        private void Note(string what, string detail) => _checks.Add(new Check(_part, what, Outcome.Info, detail));

        private static string Ms(TimeSpan span) => span.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);

        private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);

        /// <summary>A moment of real time, as the game's own clock stops while it is paused.</summary>
        private static WaitForSecondsRealtime Wait(float seconds) => new WaitForSecondsRealtime(seconds);

        private static void Say(string text)
        {
            global::Console.instance.OrNull()?.AddString(text);
            Chat.instance.OrNull()?.AddString(text);
        }

        // ---------- the report ----------

        private void Write(string why, DateTime started, TimeSpan took)
        {
            var version = Chainloader.PluginInfos.TryGetValue(Plugin.Guid, out var info) ? info.Metadata.Version.ToString() : "?";
            var lines = new List<string>
            {
                "",
                "==== Bindrune " + version + " self-test, " + started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) +
                ", started because " + why + ", took " + Ms(took) + " ms ====",
            };
            lines.AddRange(Report.Lines("Bindrune self-test", _checks));

            foreach (var line in lines.Skip(1)) _log.LogInfo(line);

            try
            {
                File.AppendAllText(LogFile, string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Bindrune self-test: could not write " + LogFile + ": " + ex.Message);
            }

            var headline = "Bindrune self-test: " + Report.Headline(_checks) + ". BepInEx/Bindrune-selftest.log has every check.";
            Say(headline);
            MessageHud.instance.OrNull()?.ShowMessage(MessageHud.MessageType.Center, headline);
        }

        // ---------- what is put back ----------

        /// <summary>What a run may change of yours, taken before it starts.</summary>
        private sealed class Snapshot
        {
            public static string KeysFile => Path.Combine(Paths.BepInExRootPath, "bindrune.keys");

            public static string SituationsFile => Path.Combine(Path.Combine(Paths.ConfigPath, "Bindrune"), "situations.txt");

            public byte[] Keys;
            public HashSet<string> KeysHeld;
            public byte[] Situations;
            public HashSet<string> SituationsHeld;
            public bool PanelOpen;
            public bool HintsVisible;
            public string Search;

            public static Snapshot Take() => new Snapshot
            {
                Keys = Bytes(KeysFile),
                KeysHeld = Held(KeysFile),
                Situations = Bytes(SituationsFile),
                SituationsHeld = Held(SituationsFile),
                PanelOpen = BindrunePanel.IsOpen,
                HintsVisible = Plugin.HintsVisible,
                Search = BindrunePanel.SearchText,
            };

            private static byte[] Bytes(string path) => File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <summary>
        /// What a file holds, as section and line, leaving out comments and blank lines: two files
        /// holding the same keys, mutes and hints in another order hold the same.
        /// </summary>
        private static HashSet<string> Held(string path)
        {
            var held = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return held;

            var section = "";
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line;
                    continue;
                }
                held.Add(section + "\t" + line);
            }
            return held;
        }

        private void PutBack(Snapshot before)
        {
            Step("the self-test's own settings removed", () => TestBinds.Unbind(_config));

            Step("the panel left as it was", () =>
            {
                if (BindrunePanel.IsOpen) BindrunePanel.SearchText = before.Search ?? "";
                if (before.PanelOpen && !BindrunePanel.IsOpen) BindrunePanel.Open();
                if (!before.PanelOpen && BindrunePanel.IsOpen) BindrunePanel.Close(quietly: true);
            });

            Step("the hints left as they were", () =>
            {
                if (Plugin.HintsVisible != before.HintsVisible) HintOverlay.Toggle();
            });

            Step("bindrune.keys", () => Compare(Snapshot.KeysFile, before.Keys, before.KeysHeld, () =>
            {
                PersonalStore.Reset();
                PersonalStore.Sync();
            }));

            Step("situations.txt", () => Compare(Snapshot.SituationsFile, before.Situations, before.SituationsHeld, () =>
            {
                SituationStore.Reset();
                SituationStore.Sync();
            }));

            Check(Plugin.HintsVisible == before.HintsVisible, "the hints are on or off as before the run");
            Check(BindrunePanel.IsOpen == before.PanelOpen, "the panel is open or closed as before the run");
        }

        /// <summary>
        /// Whether a file holds what it held before the run. Where it does not, the copy taken first
        /// goes back, written anew so Bindrune sees the change, and Bindrune reads it again.
        /// </summary>
        private void Compare(string path, byte[] before, HashSet<string> heldBefore, Action reread)
        {
            var name = Path.GetFileName(path);
            var heldAfter = Held(path);
            if (heldAfter.SetEquals(heldBefore))
            {
                Check(true, name + " holds what it held before the run");
                return;
            }

            var gone = heldBefore.Except(heldAfter).Take(3).ToList();
            var added = heldAfter.Except(heldBefore).Take(3).ToList();
            if (before == null) File.Delete(path);
            else File.WriteAllBytes(path, before);
            reread();

            Warn(name + " was put back from the copy taken before the run, as undoing the run's changes left it different",
                "missing " + (gone.Count == 0 ? "nothing" : string.Join(" | ", gone.ToArray())) + "; extra " +
                (added.Count == 0 ? "nothing" : string.Join(" | ", added.ToArray())));
        }

        /// <summary>One step of putting back, which fails alone so the rest still runs.</summary>
        private void Step(string what, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                // Told in the report as a failure of putting back, with what it threw.
                Fail(what, ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
