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
            try
            {
                if (!File.Exists(path)) return new string[0];
                return File.ReadAllLines(path);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Bindrune: could not read {Path.GetFileName(path)}: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>True for blank lines and comments, which every caller skips the same way.</summary>
        public static bool IsNoise(string line) => line.Length == 0 || line.StartsWith("#");

        /// <summary>
        /// Writes header comments followed by the body. When atomic, the file is written aside
        /// and swapped in, so a crash mid-write cannot leave a half-file where user data was.
        /// </summary>
        public static void Write(string path, IEnumerable<string> header, IEnumerable<string> body, bool atomic = false)
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
                    return;
                }

                var temp = path + ".tmp";
                File.WriteAllLines(temp, lines.ToArray());

                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Bindrune: could not save {Path.GetFileName(path)}: {ex.Message}");
            }
        }
    }
}
