using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bindrune.Discovery;
using Bindrune.UI;
using UnityEngine;

namespace Bindrune.SelfTest
{
    internal sealed partial class SelfTest
    {
        /// <summary>How many details are drawn before the game gets a frame, so the sweep does not freeze it.</summary>
        private const int DetailsPerFrame = 10;

        /// <summary>
        /// Opens the help page, the hints page and the detail of every bind the profile has, with
        /// every section open, as clicking does: a real profile holds binds of shapes no test
        /// fixture has. A detail that throws fails, with the bind it was drawing. Errors logged
        /// meanwhile are warned of, as another mod may log them too. This shows that each detail
        /// draws, not that it draws the right thing.
        /// </summary>
        private IEnumerator EveryDetail()
        {
            if (!BindrunePanel.IsOpen)
            {
                Skip("drawing every bind's detail", "the panel is not open");
                yield break;
            }

            var selected = BindrunePanel.Selected;
            var collapsed = BindrunePanel.CollapsedSections.ToList();
            var binds = BindRegistry.All.ToList();
            var thrown = new List<string>();
            var logged = new List<string>();
            var slowest = TimeSpan.Zero;
            var slowestId = "";

            Application.LogCallback listen = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception) logged.Add(message);
            };

            var clock = Stopwatch.StartNew();
            Application.logMessageReceived += listen;
            try
            {
                BindrunePanel.CollapsedSections.Clear();

                foreach (var hints in new[] { false, true })
                {
                    try
                    {
                        BindrunePanel.ShowPage(hints);
                    }
                    catch (Exception ex)
                    {
                        // Told in the report as the page that failed to draw, with what it threw.
                        thrown.Add((hints ? "the hints page" : "the help page") + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
                    yield return null;
                }

                for (var i = 0; i < binds.Count; i++)
                {
                    var each = Stopwatch.StartNew();
                    try
                    {
                        BindrunePanel.ShowDetailOf(binds[i]);
                    }
                    catch (Exception ex)
                    {
                        // Told in the report with the bind whose detail failed to draw.
                        thrown.Add(binds[i].Id + ": " + ex.GetType().Name + ": " + ex.Message);
                    }
                    if (each.Elapsed > slowest)
                    {
                        slowest = each.Elapsed;
                        slowestId = binds[i].Id;
                    }
                    if (i % DetailsPerFrame == DetailsPerFrame - 1) yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived -= listen;
                BindrunePanel.CollapsedSections.Clear();
                foreach (var key in collapsed) BindrunePanel.CollapsedSections.Add(key);
                Step("the detail left on the bind it showed", () => BindrunePanel.ShowDetailOf(selected));
            }

            Check(thrown.Count == 0, "the help page, the hints page and every bind's detail draw, with every section open",
                thrown.Count == 0
                    ? Count(binds.Count) + " binds"
                    : Count(thrown.Count) + " failed: " + string.Join(" | ", thrown.Take(5).ToArray()) + (thrown.Count > 5 ? " and more" : ""));
            Note("drawing them all", Ms(clock.Elapsed) + " ms, the slowest " + slowestId + " at " + Ms(slowest) + " ms");

            if (logged.Count == 0) Check(true, "no error was logged while they drew");
            else
                Warn("errors were logged while they drew, by Bindrune or another mod",
                    Count(logged.Count) + ": " + string.Join(" | ", logged.Distinct().Take(3).ToArray()));
        }
    }
}
