using System;
using System.IO;
using System.Linq;
using BepInEx;

namespace Bindrune.Personal
{
    /// <summary>
    /// A spare copy of bindrune.keys outside the profile folder, for the one kind of update that
    /// takes the whole folder: Update existing profile in Thunderstore Mod Manager and r2modman
    /// builds the new profile beside the old one, deletes the old folder and renames the new one
    /// to its name. When the plugin starts in a profile without bindrune.keys and a spare copy
    /// under the profile's name is there, the copy comes back before anything reads the file.
    ///
    /// Which profiles get one, and where it goes, SpareFolder decides, the same way as in Keepsake.
    ///
    /// Nothing is written outside the profile until you agree to it: the panel asks once such a
    /// profile keeps something, and the answer is kept in BepInEx/bindrune.spare, not in the
    /// plugin's cfg file, which a sync hands out and an export carries, so no pack answers for the
    /// people who follow it. The answer is part of the spare copy and comes back with it.
    ///
    /// The copy follows every save of bindrune.keys, and is brought up to date as the plugin
    /// starts, for edits made by hand while the game was closed, only from a profile that has the
    /// file, so one that lost it never writes over the copy. Bindrune removes a spare copy only
    /// when you turn it off; a profile deleted in the manager leaves its spare copy behind.
    /// </summary>
    internal static class SpareCopy
    {
        private const string KeysName = "bindrune.keys";
        private const string AnswerName = "bindrune.spare";

        /// <summary>Whether this session's start brought the spare copy back, for the plugin to say so once your character appears.</summary>
        public static bool RestoredThisLaunch { get; private set; }

        /// <summary>BepInEx/bindrune.spare: whether you want a spare copy, once asked.</summary>
        public static string AnswerPath => Path.Combine(Paths.BepInExRootPath, AnswerName);

        private static string KeysPath => Path.Combine(Paths.BepInExRootPath, KeysName);

        /// <summary>The spare copy's folder for this profile, or null when the profile is not one of a manager that replaces profile folders.</summary>
        public static string Folder => SpareFolder.For(Paths.BepInExRootPath);

        /// <summary>Whether a spare copy can be kept for this profile at all: one of a manager that replaces profile folders.</summary>
        public static bool Offered => Folder != null;

        /// <summary>Whether to ask about a spare copy now: a profile that could have one, that keeps something, and no answer yet.</summary>
        public static bool Asks => Offered && KeepsSomething() && Wanted == null;

        /// <summary>Whether you want a spare copy: true or false once asked, null before, or when the file cannot be read.</summary>
        public static bool? Wanted => SpareFolder.WantedIn(AnswerLines());

        private static string[] AnswerLines()
        {
            try
            {
                if (!File.Exists(AnswerPath)) return Array.Empty<string>();
                return File.ReadAllLines(AnswerPath);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not read {AnswerName}: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>Whether bindrune.keys holds anything of yours: a key, a muted clash, a hint or a hotbar key.</summary>
        private static bool KeepsSomething() =>
            TextStore.Read(KeysPath).Select(l => l.Trim()).Any(l => !TextStore.IsNoise(l) && !(l.StartsWith("[", StringComparison.Ordinal) && l.EndsWith("]", StringComparison.Ordinal)));

        /// <summary>
        /// Records your answer. Yes makes the spare copy at once; no removes one made before, and
        /// the Bindrune folder beside profiles once it holds nothing. Returns why not, or null.
        /// </summary>
        public static string Answer(bool keep)
        {
            var written = TextStore.Write(AnswerPath,
                new[]
                {
                    SpareFolder.AnswerVersion,
                    "# Whether Bindrune keeps a spare copy of bindrune.keys outside the profile, for",
                    "# Update existing profile in Thunderstore Mod Manager and r2modman.",
                },
                new[] { "copy\t" + (keep ? "yes" : "no") }, atomic: true);
            if (!written) return "your answer could not be saved, see the log";

            var spare = Folder;
            if (spare == null) return null;

            try
            {
                if (keep)
                {
                    Update();
                    return null;
                }

                SpareFolder.Remove(spare);
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not {(keep ? "make" : "remove")} the spare copy in {spare}: {ex.Message}");
                return keep ? "the spare copy could not be made, see the log" : "the spare copy could not be removed, see the log";
            }
        }

        /// <summary>
        /// As the plugin starts, before anything reads bindrune.keys: brings the spare copy back
        /// into a profile that lost the file, then brings the copy up to date. Returns whether it
        /// came back.
        /// </summary>
        public static bool AtLaunch()
        {
            RestoredThisLaunch = false;
            try
            {
                RestoredThisLaunch = Restore();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: bringing back the spare copy failed: {ex.Message}");
            }

            Follow();
            return RestoredThisLaunch;
        }

        private static bool Restore()
        {
            var spare = Folder;
            if (spare == null || File.Exists(KeysPath) || !File.Exists(Path.Combine(spare, KeysName))) return false;

            Plugin.Log.LogInfo($"Bindrune: this profile has no {KeysName}, and a spare copy of it is in {spare}. " +
                               "The profile folder was replaced, as Update existing profile does, so the spare copy comes back.");

            // The answer first and the keys last: without the keys, the next start tries again.
            CopyOver(Path.Combine(spare, AnswerName), AnswerPath);
            CopyOver(Path.Combine(spare, KeysName), KeysPath);
            return true;
        }

        /// <summary>
        /// Brings the spare copy up to date after bindrune.keys was saved, once you said yes, and
        /// only from a profile that has the file. Never throws: the next start catches up either way.
        /// </summary>
        public static void Follow()
        {
            try
            {
                Update();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: saving the spare copy failed: {ex.Message}");
            }

            Followed?.Invoke();
        }

        /// <summary>Raised after bindrune.keys was saved, so an open panel can see whether the question is due now.</summary>
        public static event Action Followed;

        private static void Update()
        {
            var spare = Folder;
            if (spare == null || Wanted != true || !File.Exists(KeysPath)) return;

            CopyOver(AnswerPath, Path.Combine(spare, AnswerName));
            CopyOver(KeysPath, Path.Combine(spare, KeysName));
        }

        /// <summary>Copies a file over another when they differ, written aside and swapped in, keeping the time it was written.</summary>
        private static void CopyOver(string from, string to)
        {
            if (!File.Exists(from)) return;
            var source = new FileInfo(from);
            var target = new FileInfo(to);
            if (target.Exists && target.Length == source.Length && target.LastWriteTimeUtc == source.LastWriteTimeUtc) return;

            Directory.CreateDirectory(Path.GetDirectoryName(to));
            var temp = to + ".tmp";
            File.Copy(from, temp, true);
            if (File.Exists(to)) File.Replace(temp, to, null);
            else File.Move(temp, to);
            File.SetLastWriteTimeUtc(to, source.LastWriteTimeUtc);
        }
    }
}
