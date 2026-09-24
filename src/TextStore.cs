using System;
using System.Collections.Generic;
using System.IO;

namespace Bindrune
{
    /// <summary>
    /// Reading and writing the small line-based files Bindrune keeps. Plain text rather than a
    /// serialiser so the files stay hand-editable and diffable, which matters because some of
    /// them are meant to be shared and edited by people.
    ///
    /// Lines starting with # are comments, and a failure is logged and swallowed: a corrupt or
    /// unreadable side file must never stop the panel from opening.
    /// </summary>
    public static class TextStore
    {
        /// <summary>
        /// A file of ours inside a BepInEx directory. Everything Bindrune keeps goes in a folder
        /// of its own rather than scattering half a dozen files through a directory shared with
        /// every other mod - which also means the names inside it need no prefix.
        /// </summary>
        public static string Ours(string root, string name) =>
            Path.Combine(Path.Combine(root, "Bindrune"), name);

        public static IEnumerable<string> Read(string path)
        {
            TryRead(path, out var lines);
            return lines;
        }

        /// <summary>
        /// Reads, and says whether it worked. A missing file counts as read, being nothing rather
        /// than a failure; a file that is there but unreadable does not, which matters to a caller
        /// about to move its contents somewhere else and delete it.
        /// </summary>
        public static bool TryRead(string path, out IEnumerable<string> lines)
        {
            lines = new string[0];

            try
            {
                if (File.Exists(path)) lines = File.ReadAllLines(path);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not read {Path.GetFileName(path)}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// When a file was last written and how long it is, so a store that keeps its file in
        /// memory can tell that someone else changed it since. Taken before reading, not after:
        /// a change landing in between then shows as a change, rather than being marked as seen.
        /// </summary>
        public static string Stamp(string path)
        {
            try
            {
                var file = new FileInfo(path);
                return file.Exists ? file.LastWriteTimeUtc.Ticks + ":" + file.Length : "missing";
            }
            catch (Exception)
            {
                // Unreadable counts as unchanged: dropping what is in memory for a file that cannot
                // be read back would lose it.
                return null;
            }
        }

        /// <summary>Whether a file has been written since it was stamped, by anything but us.</summary>
        public static bool ChangedSince(string path, string stamp)
        {
            var now = Stamp(path);
            return now != null && stamp != null && now != stamp;
        }

        /// <summary>True for blank lines and comments, which every caller skips the same way.</summary>
        public static bool IsNoise(string line) => line.Length == 0 || line.StartsWith("#");

        /// <summary>
        /// Writes header comments followed by the body. When atomic, the file is written aside
        /// and swapped in, so a crash mid-write cannot leave a half-file where user data was.
        /// Returns whether the file was written. A failure is logged here.
        /// </summary>
        public static bool Write(string path, IEnumerable<string> header, IEnumerable<string> body, bool atomic = false)
        {
            try
            {
                // Our folder may not exist yet, and the first write is what creates it.
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                var lines = new List<string>(header);
                lines.AddRange(body);

                if (!atomic)
                {
                    File.WriteAllLines(path, lines.ToArray());
                    return true;
                }

                var temp = path + ".tmp";
                File.WriteAllLines(temp, lines.ToArray());

                // Replace swaps the two in one step, so there is no moment without the file.
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: could not save {Path.GetFileName(path)}: {ex.Message}");
                return false;
            }
        }
    }
}
