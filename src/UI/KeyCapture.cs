using System;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Bindrune.UI
{
    /// <summary>What a capture is for, so only the control that started it shows as waiting.</summary>
    public enum CapturePurpose
    {
        None,
        Rebind,
        Search
    }

    /// <summary>
    /// Listens for the next key press. Reads through BepInEx's input abstraction rather than
    /// UnityEngine.Input directly, so it works whichever input backend the game was built against.
    /// </summary>
    public static class KeyCapture
    {
        private static Action<KeyCombo> _onCaptured;
        private static KeyCode[] _candidates;

        public static CapturePurpose Purpose { get; private set; }

        public static bool Active => Purpose != CapturePurpose.None;

        /// <summary>Raised whenever capture starts or stops, so the UI can stop showing "press a key".</summary>
        public static Action Changed;

        public static bool IsCapturingFor(CapturePurpose purpose) => Purpose == purpose;

        public static void Begin(CapturePurpose purpose, Action<KeyCombo> onCaptured)
        {
            _onCaptured = onCaptured;
            Purpose = purpose;
            Changed?.Invoke();
        }

        public static void Cancel()
        {
            if (Purpose == CapturePurpose.None) return;

            Purpose = CapturePurpose.None;
            _onCaptured = null;
            Changed?.Invoke();
        }

        public static void Tick()
        {
            if (!Active) return;

            var input = UnityInput.Current;
            if (input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (_candidates == null)
                _candidates = input.SupportedKeyCodes.Where(k => k != KeyCode.None && k != KeyCode.Escape).ToArray();

            var modifiers = _candidates.Where(k => KeyCombo.IsModifier(k) && input.GetKey(k)).ToList();

            foreach (var key in _candidates)
            {
                if (KeyCombo.IsModifier(key)) continue;
                if (input.GetKeyDown(key))
                {
                    Complete(new KeyCombo(key, modifiers));
                    return;
                }
            }

            // Releasing a modifier on its own binds that modifier, which is how mods that want a
            // held key (Alt to drag, Shift to favourite) are set.
            foreach (var key in _candidates.Where(KeyCombo.IsModifier))
            {
                if (input.GetKeyUp(key))
                {
                    Complete(new KeyCombo(key, null));
                    return;
                }
            }
        }

        private static void Complete(KeyCombo combo)
        {
            var callback = _onCaptured;

            Purpose = CapturePurpose.None;
            _onCaptured = null;

            callback?.Invoke(combo);
            Changed?.Invoke();
        }
    }
}
