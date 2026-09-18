using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;

namespace Bindrune
{
    /// <summary>
    /// Clears up settings an earlier version of Bindrune had and this one does not.
    ///
    /// BepInEx writes settings it no longer recognises back out on every save, so a setting we
    /// dropped lives on in the config file for good. That file sits in BepInEx/config, which is
    /// the directory a profile sync copies to everyone, so leaving them there means shipping
    /// other people our history.
    /// </summary>
    public static class Upkeep
    {
        public static void Run(ConfigFile config)
        {
            try
            {
                DropOrphanedSettings(config);
            }
            catch (Exception ex)
            {
                // Tidying is never worth failing a load over.
                Plugin.Log.LogWarning($"Bindrune: could not tidy up after an older version: {ex.Message}");
            }
        }

        /// <summary>
        /// Settings we no longer have. BepInEx keeps them so that turning a feature back on finds
        /// its old value, which is right for a mod that comes and goes and wrong for one that
        /// dropped the setting on purpose.
        /// </summary>
        private static void DropOrphanedSettings(ConfigFile config)
        {
            if (config == null) return;

            if (!(AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config) is IDictionary orphans))
            {
                Plugin.Log.LogDebug("Bindrune: orphaned settings are not readable on this BepInEx; leaving them.");
                return;
            }

            if (orphans.Count == 0) return;

            var names = new List<string>();
            foreach (DictionaryEntry orphan in orphans) names.Add(orphan.Key?.ToString() ?? "?");

            orphans.Clear();
            config.Save();

            Plugin.Log.LogInfo($"Bindrune: dropped {names.Count} setting(s) we no longer have: {string.Join(", ", names.ToArray())}.");
        }
    }
}
