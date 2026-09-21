using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Bindrune
{
    /// <summary>
    /// Where keys sit on a keyboard, in key widths, so that "near G" can be worked out.
    ///
    /// KeyCodes name positions on a US keyboard whatever layout is in use, so one table holds for
    /// every keyboard: the key a German board labels Y is KeyCode.Z, and it is Z's neighbours
    /// that surround it there too. Covers the main block, the function row, the navigation keys
    /// and the arrows. The numpad and mouse buttons have no place, and so no neighbours.
    /// </summary>
    public static class KeyGrid
    {
        private struct Spot
        {
            public readonly float X, Y;
            public Spot(float x, float y) { X = x; Y = y; }
        }

        private static readonly Dictionary<KeyCode, Spot> Places = Build();

        /// <summary>Every other placed key, nearest first. Empty for a key with no place.</summary>
        public static IEnumerable<KeyCode> Around(KeyCode key)
        {
            if (!Places.TryGetValue(key, out var from)) return Enumerable.Empty<KeyCode>();

            // OrderBy is stable, so keys the same distance away stay in reading order.
            return Places
                .Where(p => p.Key != key)
                .OrderBy(p => Distance(from, p.Value))
                .Select(p => p.Key);
        }

        /// <summary>How far apart two keys are, in key widths, or null when either has no place.</summary>
        public static float? Distance(KeyCode a, KeyCode b) =>
            Places.TryGetValue(a, out var from) && Places.TryGetValue(b, out var to) ? Distance(from, to) : (float?)null;

        private static float Distance(Spot a, Spot b) =>
            (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static Dictionary<KeyCode, Spot> Build()
        {
            var places = new Dictionary<KeyCode, Spot>();

            // A run of keys side by side, one key width apart, starting at x.
            void Run(float y, float x, params KeyCode[] keys)
            {
                for (var i = 0; i < keys.Length; i++) places[keys[i]] = new Spot(x + i, y);
            }

            // Escape and Caps Lock are left out on purpose: neither is ever worth suggesting.
            Run(0f, 2f, KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4);
            Run(0f, 6.5f, KeyCode.F5, KeyCode.F6, KeyCode.F7, KeyCode.F8);
            Run(0f, 11f, KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12);

            Run(1.5f, 0f, KeyCode.BackQuote, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
                KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
                KeyCode.Minus, KeyCode.Equals);
            Run(1.5f, 13.5f, KeyCode.Backspace);
            Run(1.5f, 15.5f, KeyCode.Insert, KeyCode.Home, KeyCode.PageUp);

            Run(2.5f, 0.25f, KeyCode.Tab);
            Run(2.5f, 1.5f, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U,
                KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket, KeyCode.RightBracket);
            Run(2.5f, 13.75f, KeyCode.Backslash);
            Run(2.5f, 15.5f, KeyCode.Delete, KeyCode.End, KeyCode.PageDown);

            Run(3.5f, 1.75f, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J,
                KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote);
            Run(3.5f, 13.4f, KeyCode.Return);

            Run(4.5f, 2.25f, KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V, KeyCode.B, KeyCode.N, KeyCode.M,
                KeyCode.Comma, KeyCode.Period, KeyCode.Slash);
            Run(4.5f, 16.5f, KeyCode.UpArrow);

            Run(5.5f, 6.25f, KeyCode.Space);
            Run(5.5f, 15.5f, KeyCode.LeftArrow, KeyCode.DownArrow, KeyCode.RightArrow);

            return places;
        }
    }
}
