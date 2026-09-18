using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Bindrune.Discovery;
using Bindrune.Hints;
using Bindrune.Personal;
using Bindrune.UI;
using UnityEngine;

namespace Bindrune
{
    [BepInPlugin(Guid, "Bindrune", "0.1.0")]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.Bindrune";

        public static ManualLogSource Log;

        private static ConfigEntry<float> _scrollSpeed;
        public static float ScrollSpeed => _scrollSpeed?.Value ?? 300f;

        private static ConfigEntry<float> _panelWidth;
        private static ConfigEntry<float> _panelHeight;

        private static ConfigEntry<bool> _hintsVisible;
        private static ConfigEntry<int> _hintFontSize;
        private static ConfigEntry<HintAlign> _hintAlign;
        private static ConfigEntry<HintGrowth> _hintGrowth;
        private static ConfigEntry<bool> _hintModNames;
        private static ConfigEntry<HintOrder> _hintOrder;
        private static ConfigEntry<float> _hintX;
        private static ConfigEntry<float> _hintY;

        /// <summary>Whether the on-screen hints are up. Remembered, so they come back as you left them.</summary>
        public static bool HintsVisible
        {
            get => _hintsVisible != null && _hintsVisible.Value;
            set { if (_hintsVisible != null) _hintsVisible.Value = value; }
        }

        public static int HintFontSize => _hintFontSize?.Value ?? 15;

        /// <summary>Where the text sits inside the hint box.</summary>
        public static HintAlign HintAlign
        {
            get => _hintAlign?.Value ?? Hints.HintAlign.Left;
            set { if (_hintAlign != null) _hintAlign.Value = value; }
        }

        /// <summary>Whether each hint names the mod it belongs to as well as the action.</summary>
        public static bool HintModNames
        {
            get => _hintModNames != null && _hintModNames.Value;
            set { if (_hintModNames != null) _hintModNames.Value = value; }
        }

        /// <summary>Whether a hint leads with the key or with what it does.</summary>
        public static HintOrder HintOrder
        {
            get => _hintOrder?.Value ?? HintOrder.KeyFirst;
            set { if (_hintOrder != null) _hintOrder.Value = value; }
        }

        /// <summary>Which way the hint list extends as rows are added.</summary>
        public static HintGrowth HintGrowth
        {
            get => _hintGrowth?.Value ?? HintGrowth.Up;
            set { if (_hintGrowth != null) _hintGrowth.Value = value; }
        }

        /// <summary>
        /// Where the hints sit, as a fraction of the screen from its bottom left corner. Kept as
        /// a fraction rather than in pixels so a change of resolution leaves them where they look
        /// like they were, instead of off the edge.
        /// </summary>
        public static Vector2 HintPosition
        {
            get => new Vector2(_hintX?.Value ?? 0.02f, _hintY?.Value ?? 0.06f);
            set
            {
                if (_hintX == null || _hintY == null) return;
                _hintX.Value = value.x;
                _hintY.Value = value.y;
            }
        }

        /// <summary>Panel size, remembered across sessions once you drag the corner.</summary>
        public static Vector2 PanelSize
        {
            get => new Vector2(_panelWidth?.Value ?? 1280f, _panelHeight?.Value ?? 820f);
            set
            {
                if (_panelWidth == null || _panelHeight == null) return;
                _panelWidth.Value = value.x;
                _panelHeight.Value = value.y;
            }
        }

        private ConfigEntry<KeyboardShortcut> _openKey;
        private static ConfigEntry<KeyboardShortcut> _hintsKey;

        /// <summary>The hint toggle as text, so the panel can name it rather than guess.</summary>
        public static string HintsKeyText => _hintsKey != null ? _hintsKey.Value.ToString() : "the hints key";
        private bool _restored;
        private float _restoreAt;
        private int _restoreAttempts;

        private void Awake()
        {
            Log = Logger;

            _openKey = Config.Bind("General", "OpenKey", new KeyboardShortcut(KeyCode.Insert),
                "Opens the Bindrune panel.");
            _panelWidth = Config.Bind("Panel", "Width", 1280f,
                new ConfigDescription("Panel width in pixels. Set by dragging the corner handle.",
                    new AcceptableValueRange<float>(900f, 3840f)));
            _panelHeight = Config.Bind("Panel", "Height", 820f,
                new ConfigDescription("Panel height in pixels. Set by dragging the corner handle.",
                    new AcceptableValueRange<float>(520f, 2160f)));
            _scrollSpeed = Config.Bind("Panel", "ScrollSpeed", 300f,
                new ConfigDescription("Mouse wheel distance per notch in the bind list. Takes effect next time the panel is opened.",
                    new AcceptableValueRange<float>(20f, 1200f)));

            _hintsKey = Config.Bind("Hints", "ToggleKey", new KeyboardShortcut(KeyCode.H, KeyCode.LeftAlt),
                "Shows or hides the on-screen key hints.");
            _hintsVisible = Config.Bind("Hints", "Visible", false,
                "Whether the on-screen hints are showing. Set by the toggle key; kept so they come back as you left them.");
            _hintFontSize = Config.Bind("Hints", "FontSize", 15,
                new ConfigDescription("Text size of the on-screen hints.", new AcceptableValueRange<int>(9, 32)));
            _hintX = Config.Bind("Hints", "X", 0.02f,
                new ConfigDescription("Where the hints sit across the screen: 0 is the left edge, 1 the right. Set by dragging them while the panel is open.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _hintY = Config.Bind("Hints", "Y", 0.06f,
                new ConfigDescription("Where the hints sit up the screen: 0 is the bottom, 1 the top. Set by dragging them while the panel is open.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _hintAlign = Config.Bind("Hints", "TextAlignment", Hints.HintAlign.Left,
                "Where the text sits inside the hint box.");
            _hintGrowth = Config.Bind("Hints", "Growth", HintGrowth.Up,
                "Which way the list extends as hints are added: Up pins its bottom edge, Down pins its top.");
            _hintModNames = Config.Bind("Hints", "ShowModNames", false,
                "Whether each hint names the mod it comes from as well as the action.");
            _hintOrder = Config.Bind("Hints", "Order", HintOrder.KeyFirst,
                "Whether each hint leads with the key you press or with what it does.");

            HintChoice.Changed = HintOverlay.Invalidate;

            // A setting changed from the in-game config manager should land straight away rather
            // than wait for the world to change under the overlay.
            Config.SettingChanged += (sender, args) => HintOverlay.Invalidate();

            // After binding, so the settings we do have are registered and only the ones we have
            // dropped count as orphans.
            Upkeep.Run(Config);

            // Reads game IL to learn when vanilla binds are used. Off-thread so a cold scan after
            // a game update does not stall the first panel open.
            ContextIndex.Warm();

            Log.LogInfo("Bindrune loaded.");
        }

        private void OnDestroy()
        {
            BindrunePanel.Close();
            HintOverlay.Close();
        }

        private void Update()
        {
            try
            {
                RestoreOnce();

                // Capture finishes inside Tick, so asking afterwards whether it is still running
                // would let the very key that completed it fall through and toggle the panel too.
                var capturing = KeyCapture.Active;

                if (BindrunePanel.IsOpen) BindrunePanel.Tick();

                // While capturing, every key belongs to the rebind, including our own open key.
                if (capturing || KeyCapture.Active) return;

                // The overlay follows the world whether or not the panel is open: that is the
                // whole point of it, and it costs a sample every fifth of a second.
                HintOverlay.Tick();

                // Escape still closes the panel while typing - that is what you press to get out
                // of the search box - but a letter belongs to the search box, not to a hotkey.
                if (BindrunePanel.Typing)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) BindrunePanel.Close();
                    return;
                }

                if (_openKey.Value.IsDown()) BindrunePanel.Toggle();
                else if (_hintsKey.Value.IsDown()) HintOverlay.Toggle();
                else if (BindrunePanel.IsOpen && Input.GetKeyDown(KeyCode.Escape)) BindrunePanel.Close();
            }
            catch (Exception ex)
            {
                Log.LogDebug($"Bindrune input check failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Puts your own keys back once the game is up. A profile sync happens at launch, before
        /// the game starts, so by here any overwrite has already landed. Waiting for ZInput keeps
        /// us clear of mod Awake methods, which config writes would otherwise reach mid-startup.
        /// </summary>
        private void RestoreOnce()
        {
            if (_restored || ZInput.instance == null) return;

            if (_restoreAt == 0f) _restoreAt = Time.realtimeSinceStartup + 3f;
            if (Time.realtimeSinceStartup < _restoreAt) return;

            // Nothing of yours to put back and nothing to show on screen, so do not pay for a scan.
            if (PersonalKeys.Count == 0 && HintChoice.Count == 0)
            {
                _restored = true;
                return;
            }

            BindRegistry.Refresh();

            // Mods bind their configs at their own pace, some only once a world loads, so a single
            // early pass would quietly leave those keys on whatever the sync wrote. Keep looking
            // until every recorded bind has turned up, then stop.
            _restoreAttempts++;
            if (PersonalKeys.Reconcile() == 0 || _restoreAttempts >= 8) _restored = true;
            else _restoreAt = Time.realtimeSinceStartup + 30f;
        }

        public static string ResolveModName(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "unknown";
            return Chainloader.PluginInfos.TryGetValue(guid, out var info) && info?.Metadata != null
                ? info.Metadata.Name
                : guid;
        }
    }
}
