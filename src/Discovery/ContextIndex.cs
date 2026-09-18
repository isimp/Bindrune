using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Bindrune.Discovery
{
    /// <summary>
    /// Works out when each vanilla bind is actually read, by finding the code that reads it.
    /// "TabRight" is only polled from Player.UpdatePlacementGhost, so it cannot collide with
    /// "Use" outside of build placement even though both sit on E.
    ///
    /// What comes out is a context: the game's own name for where a button is read, taken from
    /// the class doing the reading. Contexts are finer than the situations the panel compares
    /// binds in - "Hud" and "Minimap" are separate contexts and neither is a situation you can
    /// pick - which is exactly what makes them worth keeping as they are. Two vanilla binds are
    /// ruled out against each other on contexts, and SituationTags.FromContext translates the
    /// ones that do have an equivalent for everything else.
    ///
    /// This lives with the scanners rather than with the situations because it is discovery: it
    /// reads IL out of the shipped assemblies and knows nothing about tags or conflicts.
    ///
    /// The game assemblies are scanned once and cached against their own file stamp, so a game
    /// update re-derives the map instead of leaving a stale hand-written table behind.
    /// </summary>
    public static class ContextIndex
    {
        private static Dictionary<string, HashSet<string>> _contexts;

        private static readonly Dictionary<string, string> TypeContexts = new Dictionary<string, string>
        {
            { "Player", "playing" },
            { "PlayerController", "playing" },
            { "Character", "playing" },
            { "GameCamera", "playing" },
            { "InventoryGui", "inventory" },
            { "StoreGui", "store" },
            { "Minimap", "map" },
            { "Terminal", "chat" },
            { "Chat", "chat" },
            { "Hud", "hud" },
            { "KeyHints", "hud" },
            { "TextViewer", "menus" },
            { "AchievementsGui", "menus" },
            { "Menu", "menus" },
            { "FejdStartup", "menus" },
            { "CinematicsManager", "cinematics" }
        };

        private static readonly object Gate = new object();

        /// <summary>
        /// Builds the map if it is not ready. Cecil touches no Unity API, so Warm() can run this
        /// on a background thread at startup; a caller that arrives first simply waits here.
        /// </summary>
        public static void Ensure()
        {
            if (_contexts != null) return;

            lock (Gate)
            {
                if (_contexts != null) return;
                _contexts = Load() ?? Build();
            }
        }

        /// <summary>
        /// Starts the scan off the main thread. A cold build reads several megabytes of IL and
        /// would otherwise freeze the game on the first panel open after a game update.
        /// </summary>
        public static void Warm()
        {
            if (_contexts != null) return;

            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    Ensure();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Bindrune: background context scan failed: {ex.Message}");
                }
            })
            {
                IsBackground = true,
                Name = "Bindrune context scan"
            };

            thread.Start();
        }

        public static HashSet<string> For(string buttonName)
        {
            Ensure();
            return _contexts.TryGetValue(buttonName, out var set) ? set : null;
        }

        /// <summary>True when both binds are understood and never read in the same situation.</summary>
        public static bool AreDisjoint(string buttonA, string buttonB, out string description)
        {
            description = null;
            var a = For(buttonA);
            var b = For(buttonB);
            if (a == null || b == null || a.Count == 0 || b.Count == 0) return false;
            if (a.Overlaps(b)) return false;

            description = $"{buttonA} is only read while {Join(a)}, {buttonB} only while {Join(b)}";
            return true;
        }

        private static string Join(IEnumerable<string> values) => string.Join(" / ", values.OrderBy(v => v).ToArray());

        private static string CachePath => TextStore.Ours(Paths.CachePath, "vanilla-contexts.txt");

        private static string Stamp()
        {
            var parts = Assemblies().Select(p =>
            {
                var f = new FileInfo(p);
                return f.Exists ? $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}" : $"{Path.GetFileName(p)}:missing";
            });
            return string.Join("|", parts.ToArray());
        }

        private static IEnumerable<string> Assemblies()
        {
            var managed = Path.Combine(Paths.GameRootPath, "valheim_Data", "Managed");
            yield return Path.Combine(managed, "assembly_valheim.dll");
            yield return Path.Combine(managed, "assembly_utils.dll");
            yield return Path.Combine(managed, "assembly_guiutils.dll");
        }

        private static Dictionary<string, HashSet<string>> Load()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                var lines = File.ReadAllLines(CachePath);
                if (lines.Length == 0 || lines[0] != Stamp()) return null;

                var map = new Dictionary<string, HashSet<string>>();
                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    map[parts[0]] = new HashSet<string>(parts[1].Split(','));
                }

                Plugin.Log.LogDebug($"Bindrune: loaded {map.Count} vanilla bind contexts from cache.");
                return map;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: context cache unreadable ({ex.Message}); rebuilding.");
                return null;
            }
        }

        private static Dictionary<string, HashSet<string>> Build()
        {
            var map = new Dictionary<string, HashSet<string>>();
            var started = DateTime.UtcNow;

            foreach (var path in Assemblies())
            {
                if (!File.Exists(path)) continue;
                try
                {
                    using (var module = ModuleDefinition.ReadModule(path))
                        ScanModule(module, map);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Bindrune: could not scan {Path.GetFileName(path)}: {ex.Message}");
                }
            }

            Plugin.Log.LogDebug($"Bindrune: derived contexts for {map.Count} vanilla binds in {(DateTime.UtcNow - started).TotalMilliseconds:F0} ms.");

            // An empty result means the assemblies were missing or unreadable, not that the game
            // has no binds. Caching that would keep the wrong answer under a stamp that never
            // changes, so leave it uncached and try again next launch.
            if (map.Count > 0) Save(map);
            return map;
        }

        private static void ScanModule(ModuleDefinition module, Dictionary<string, HashSet<string>> map)
        {
            foreach (var type in module.GetTypes())
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;

                string pending = null;
                foreach (var instruction in method.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Ldstr)
                    {
                        pending = instruction.Operand as string;
                        continue;
                    }

                    if (pending == null) continue;

                    if (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    {
                        if (instruction.Operand is MethodReference callee &&
                            callee.DeclaringType?.Name == "ZInput" &&
                            callee.Name.StartsWith("GetButton", StringComparison.Ordinal))
                        {
                            Record(map, pending, type, method);
                        }
                    }

                    pending = null;
                }
            }
        }

        private static void Record(Dictionary<string, HashSet<string>> map, string button, TypeDefinition type, MethodDefinition method)
        {
            var owner = type.DeclaringType?.Name ?? type.Name;

            // Placement is a mode inside Player, not a separate class, so the method name is
            // the only thing that separates build binds from ordinary world binds.
            string context;
            if (method.Name.IndexOf("Placement", StringComparison.OrdinalIgnoreCase) >= 0)
                context = "building";
            else if (!TypeContexts.TryGetValue(owner, out context))
                context = owner;

            if (!map.TryGetValue(button, out var set))
                map[button] = set = new HashSet<string>();
            set.Add(context);
        }

        private static void Save(Dictionary<string, HashSet<string>> map)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
                var lines = new List<string> { Stamp() };
                lines.AddRange(map.Select(kv => kv.Key + "=" + string.Join(",", kv.Value.ToArray())));
                File.WriteAllLines(CachePath, lines.ToArray());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not cache contexts: {ex.Message}");
            }
        }
    }
}
