using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bindrune.Context;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Managers;

namespace Bindrune.Discovery
{
    /// <summary>
    /// Reads what a mod's own key hints already say about its binds.
    ///
    /// A Jotunn KeyHintConfig exists so the game can show "E: use remote" while the right thing
    /// is equipped, which means the mod has already declared the item its buttons belong to. That
    /// is the same fact we ask for in "only when holding", written for another purpose, so there
    /// is nothing for the author to adopt.
    /// </summary>
    public static class JotunnHints
    {
        public static void Read(List<BindEntry> binds)
        {
            try
            {
                var manager = KeyHintManager.Instance;
                if (manager == null) return;

                if (!(AccessTools.Field(typeof(KeyHintManager), "KeyHints")?.GetValue(manager) is IDictionary hints)) return;

                var found = 0;
                foreach (DictionaryEntry entry in hints)
                {
                    var config = entry.Value as KeyHintConfig;
                    if (config?.ButtonConfigs == null) continue;

                    var situations = new List<string>();
                    if (!string.IsNullOrEmpty(config.Item)) situations.Add(EquippedItems.Prefix + "item:" + config.Item);

                    // A hint tied to a build piece only shows while that piece is selected.
                    if (!string.IsNullOrEmpty(config.Piece)) situations.Add("Build placement");

                    if (situations.Count == 0) continue;

                    foreach (var button in config.ButtonConfigs)
                    {
                        if (button == null) continue;

                        // Matched by object identity: the same ButtonConfig instance is what the
                        // Jotunn scan stored as the bind's handle, so no name matching is needed.
                        var bind = binds.FirstOrDefault(b => ReferenceEquals(b.Handle, button));
                        if (bind == null) continue;

                        KnownSituations.RecordFromHints(bind.Id, situations);
                        Hints.SelfHinting.Record(bind.Id);
                        found++;
                    }
                }

                if (found > 0) Plugin.Log.LogDebug($"Bindrune: {found} binds described by their mod's key hints.");
            }
            catch (Exception ex)
            {
                Plugin.WarnOnce($"Bindrune: could not read key hints: {ex.Message}");
            }
        }
    }
}
