using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;

namespace Bindrune.Conflicts
{
    /// <summary>Remembers which conflicts you have already decided are fine.</summary>
    public static class MuteStore
    {
        private static HashSet<string> _muted;

        private static string FilePath => TextStore.Ours(Paths.ConfigPath, "muted.txt");

        private static HashSet<string> Muted
        {
            get
            {
                if (_muted != null) return _muted;

                _muted = new HashSet<string>(
                    TextStore.Read(FilePath).Where(line => !TextStore.IsNoise(line)).Select(line => line.Trim()));

                return _muted;
            }
        }

        public static bool IsMuted(Conflict conflict) => Muted.Contains(conflict.PairKey);

        public static void Toggle(Conflict conflict)
        {
            if (!Muted.Add(conflict.PairKey)) Muted.Remove(conflict.PairKey);

            TextStore.Write(FilePath,
                new[] { "# Conflicts Bindrune should stop flagging." },
                Muted.OrderBy(k => k));
        }
    }
}
