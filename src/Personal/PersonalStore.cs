using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace Bindrune.Personal
{
    /// <summary>
    /// The one file holding everything Bindrune remembers that a profile sync must not take away:
    /// the keys you set, the clashes you muted and the binds you pinned on screen.
    ///
    /// Gale rewrites and deletes files under BepInEx/config on every pull, and its export also
    /// picks up any .cfg, .txt, .json, .yml, .yaml or .ini anywhere else in the profile. A file is
    /// therefore only safe when it is both outside config and named something that list does not
    /// cover, which is why all three live here together rather than in three files of their own.
    ///
    /// One [section] per kind, comments and blank lines ignored. Lines before the first section
    /// are keys, which is exactly what a file written by an earlier version is, so it reads as it
    /// stands and needs no conversion of its own.
    /// </summary>
    public static class PersonalStore
    {
        public const string Keys = "keys";
        public const string Muted = "muted";
        public const string Hints = "hints";

        private const string Version = "# bindrune state v3";

        /// <summary>The order sections are written in. Any others found are kept and appended.</summary>
        private static readonly string[] Known = { Keys, Muted, Hints };

        private static readonly Dictionary<string, string[]> Notes = new Dictionary<string, string[]>
        {
            { Keys, new[] { "# bind id, your key, the profile's key, whether yours is live." } },
            {
                Muted, new[]
                {
                    "# Clashes Bindrune should stop flagging: the two bind ids, then what was",
                    "# waved through. A line with no severity mutes the pair whatever it does."
                }
            },
            {
                Hints, new[]
                {
                    "# Binds Bindrune shows on screen, one id per line.",
                    "# Each is shown only while its situations say it applies."
                }
            }
        };

        /// <summary>
        /// Where these lived before they were collected here, and the section each became. Read
        /// once, on the first launch after the upgrade, and then deleted.
        /// </summary>
        private static readonly Dictionary<string, string> Older = new Dictionary<string, string>
        {
            { Muted, "muted.txt" },
            { Hints, "hints.txt" }
        };

        private static Dictionary<string, List<string>> _sections;
        private static List<string> _order;
        private static string _stamp;

        /// <summary>
        /// Raised when the file turned out to have changed since it was read, so that everything
        /// holding a parsed copy of a section drops it and reads the section again.
        /// </summary>
        public static event Action Reloaded;

        private static string FilePath => Path.Combine(Paths.BepInExRootPath, "bindrune.keys");

        private static string OlderPath(string name) => TextStore.Ours(Paths.ConfigPath, name);

        /// <summary>The body lines of a section, as a copy: changes go back through Replace.</summary>
        public static List<string> Lines(string section)
        {
            Ensure();
            return new List<string>(Body(section));
        }

        /// <summary>Puts a section back and writes the file, leaving every other section as it is.</summary>
        public static void Replace(string section, IEnumerable<string> lines)
        {
            Ensure();

            var body = Body(section);
            body.Clear();
            body.AddRange(lines);

            Write();
        }

        /// <summary>
        /// Checks the file against what was read, and reads it again if something else wrote it:
        /// a hand edit made while the game runs would otherwise be undone by the next change,
        /// since the file is always written whole. Called before every change and when the panel
        /// opens, so owners call it first and then change what they read.
        /// </summary>
        public static void Sync()
        {
            if (_sections == null || !TextStore.ChangedSince(FilePath, _stamp)) return;

            _sections = null;
            Plugin.Log.LogInfo($"Bindrune: {Path.GetFileName(FilePath)} was changed outside the game; reading it again.");
            Reloaded?.Invoke();
        }

        private static void Ensure()
        {
            if (_sections == null) Load();
        }

        private static List<string> Body(string section)
        {
            if (!_sections.TryGetValue(section, out var body))
            {
                _sections[section] = body = new List<string>();
                _order.Add(section);
            }

            return body;
        }

        private static void Load()
        {
            _sections = new Dictionary<string, List<string>>();
            _order = new List<string>();
            _stamp = TextStore.Stamp(FilePath);

            // A file from an earlier version is all keys and carries no section line, so that is
            // where its lines belong. A section line found anywhere is also the one thing that
            // says this file has already been through the step below.
            var section = Keys;
            var sectioned = false;

            foreach (var raw in TextStore.Read(FilePath))
            {
                var line = raw.Trim();
                if (TextStore.IsNoise(line)) continue;

                if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2);
                    sectioned = true;
                    continue;
                }

                Body(section).Add(line);
            }

            if (sectioned) Announce();
            else Adopt();
        }

        /// <summary>
        /// Says so when one of the old files is there after the move. A profile sync can put the
        /// owner's copy back at any time, and it is not read again: without a word in the log, a
        /// file someone edits by hand would simply do nothing.
        /// </summary>
        private static void Announce()
        {
            foreach (var older in Older)
            {
                var path = OlderPath(older.Value);
                if (!File.Exists(path)) continue;

                Plugin.Log.LogInfo($"Bindrune: ignoring {older.Value}, which an older version used or a profile sync " +
                                   $"put back. What it held now lives in {Path.GetFileName(FilePath)} and is read from there.");
            }
        }

        /// <summary>
        /// Takes in what earlier versions kept in BepInEx/config. The new file is written before
        /// any of the old ones are removed, so a failure part way through leaves everything in at
        /// least one place, and the section lines it writes are what stops this running twice.
        /// </summary>
        private static void Adopt()
        {
            var taken = new List<string>();

            foreach (var older in Older)
            {
                var path = OlderPath(older.Value);
                if (!File.Exists(path)) continue;

                if (!TextStore.TryRead(path, out var lines))
                {
                    Plugin.Log.LogError($"Bindrune: {older.Value} could not be read, so what it held was not carried " +
                                        $"over into {Path.GetFileName(FilePath)}. It has been left where it is.");
                    continue;
                }

                var body = Body(older.Key);
                var seen = new HashSet<string>(body);

                foreach (var line in lines.Select(l => l.Trim()))
                {
                    if (TextStore.IsNoise(line) || !seen.Add(line)) continue;
                    body.Add(line);
                }

                taken.Add(path);
            }

            // Written even when there was nothing to take over: the sections it puts in the file
            // are the mark that this has been done, and without them every launch would look again.
            Write();

            foreach (var path in taken)
            {
                try
                {
                    File.Delete(path);
                    Plugin.Log.LogInfo($"Bindrune: moved {Path.GetFileName(path)} into {Path.GetFileName(FilePath)}, " +
                                       "where a profile sync cannot delete it.");
                }
                catch (Exception ex)
                {
                    // What it held is already in the new file, so a copy left behind is untidy
                    // rather than harmful: it is never read again.
                    Plugin.Log.LogWarning($"Bindrune: could not remove {Path.GetFileName(path)}: {ex.Message}");
                }
            }
        }

        private static void Write()
        {
            var lines = new List<string>();

            foreach (var section in Known.Concat(_order.Where(s => !Known.Contains(s))))
            {
                lines.Add(string.Empty);
                lines.Add("[" + section + "]");
                if (Notes.TryGetValue(section, out var notes)) lines.AddRange(notes);
                if (_sections.TryGetValue(section, out var body)) lines.AddRange(body);
            }

            // Atomic: these are choices made by hand that cannot be got back from anywhere else,
            // so a crash mid-write must not be able to truncate them.
            TextStore.Write(FilePath,
                new[]
                {
                    Version,
                    "# Everything Bindrune remembers that a profile sync must not take away.",
                    "# Kept out of BepInEx/config, with an extension no sync picks up, for that reason."
                },
                lines, atomic: true);

            _stamp = TextStore.Stamp(FilePath);
        }
    }
}
