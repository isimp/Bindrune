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
        private static RectTransform _quit;

        public static CapturePurpose Purpose { get; private set; }

        public static bool Active => Purpose != CapturePurpose.None;

        /// <summary>Raised whenever capture starts or stops, so the UI can stop showing "press a key".</summary>
        public static Action Changed;

        public static bool IsCapturingFor(CapturePurpose purpose) => Purpose == purpose;

        public static void Begin(CapturePurpose purpose, Action<KeyCombo> onCaptured)
        {
            _onCaptured = onCaptured;
            _quit = null;
            Purpose = purpose;
            Changed?.Invoke();
        }

        /// <summary>
        /// The control that ends this capture, whose own left click must reach it rather than be
        /// taken as the new key: we see the press a frame before the button does, so without this
        /// a click on Cancel binds Mouse0 instead of cancelling.
        ///
        /// Set by whoever draws that control, not by whoever starts the capture, because starting
        /// one rebuilds the pane it lives in and the object the capture began with is gone by the
        /// time anyone clicks it.
        /// </summary>
        public static void QuitsOver(RectTransform quit) => _quit = quit;

        public static void Cancel()
        {
            if (Purpose == CapturePurpose.None) return;

            Purpose = CapturePurpose.None;
            _onCaptured = null;
            _quit = null;
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
                if (!input.GetKeyDown(key)) continue;

                // Over the button that ends this capture, a left click belongs to the button.
                // Everywhere else it is a key like any other, so Mouse0 stays bindable.
                if (key == KeyCode.Mouse0 && OverQuit(input)) continue;

                Complete(new KeyCombo(key, modifiers));
                return;
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

        /// <summary>True while the pointer is over the control that ends this capture.</summary>
        private static bool OverQuit(IInputSystem input)
        {
            // Destroyed along with the pane it was in, which Unity reports as null.
            if (_quit == null) return false;

            // An overlay canvas has no camera and passing one would place the rectangle wrong.
            // Asking the canvas rather than assuming keeps this right whichever Jotunn uses.
            var canvas = _quit.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.RectangleContainsScreenPoint(_quit, input.mousePosition, camera);
        }

        private static void Complete(KeyCombo combo)
        {
            var callback = _onCaptured;

            Purpose = CapturePurpose.None;
            _onCaptured = null;
            _quit = null;

            callback?.Invoke(combo);
            Changed?.Invoke();
        }
    }
}
