using System.Collections.Generic;
using System.Linq;
using Bindrune.Discovery;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Bindrune.Hints
{
    /// <summary>Which way the list extends as rows are added, which is to say which edge stays put.</summary>
    public enum HintGrowth
    {
        /// <summary>The bottom edge is pinned and the list climbs. For a box near the bottom.</summary>
        Up,
        /// <summary>The top edge is pinned and the list hangs. For a box near the top.</summary>
        Down
    }

    /// <summary>Where the text sits inside the box.</summary>
    public enum HintAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>Which half of a hint comes first: the key you press, or what it does.</summary>
    public enum HintOrder
    {
        KeyFirst,
        ActionFirst
    }

    /// <summary>
    /// A small list of keys on the HUD, for binds that never show themselves. Drawn by us rather
    /// than through Jotunn's key hints on purpose: a Jotunn hint can only key off an equipped item
    /// or a build piece, so it cannot express "while the map is open", and registering hints for
    /// another mod's bind would mean inventing button configs in that mod's name.
    ///
    /// It takes no input while you play - every element has raycastTarget off - and becomes
    /// draggable only while the Bindrune panel is open, which is the moment you are arranging
    /// things anyway. Its place is kept as a fraction of the screen, so it lands where you left
    /// it at any resolution.
    /// </summary>
    public static class HintOverlay
    {
        private static GameObject _root;
        private static RectTransform _list;
        private static HintDrag _drag;
        private static Image _background;
        private static string _drawn;
        private static bool _stale = true;
        private static bool _movable;

        /// <summary>
        /// Whether the overlay can be dragged right now. The panel sets this while it is open;
        /// changing it redraws, because it changes how the box looks as well as what it does.
        /// </summary>
        public static bool Movable
        {
            get => _movable;
            set
            {
                if (_movable == value) return;
                _movable = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Says the rows may be wrong for a reason the world cannot tell us: a rebind, a change to
        /// which binds are shown, an edited situation, a setting. Forgetting what was drawn is
        /// part of it, so a change that leaves the text identical still takes effect.
        /// </summary>
        public static void Invalidate()
        {
            _stale = true;
            _drawn = null;
        }

        public static void Toggle()
        {
            Plugin.HintsVisible = !Plugin.HintsVisible;

            if (!Plugin.HintsVisible)
            {
                Hide();
                return;
            }

            // Turning the hints on may be the first thing that ever needed the bind list.
            if (BindRegistry.All.Count == 0) BindRegistry.Refresh();

            Invalidate();
            Tick(true);
        }

        public static void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        public static void Close()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _list = null;
            _drag = null;
            _background = null;
            _drawn = null;
        }

        /// <summary>
        /// Keeps the overlay in step with the world. Called every frame, but it only samples the
        /// situation on a cadence and only rebuilds rows when what they would say has changed.
        /// </summary>
        public static void Tick(bool force = false)
        {
            if (!Plugin.HintsVisible)
            {
                Hide();
                return;
            }

            // The only per-frame cost when nothing is happening: one bool and one clock check
            // inside Sample, which reads the world five times a second at most.
            var moved = SituationNow.Sample();
            if (!moved && !_stale && !force) return;
            _stale = false;

            // While it can be moved it stays on screen even with nothing to say, because an
            // invisible box is one you cannot drag.
            if (!SituationNow.InGame && !Movable)
            {
                Hide();
                return;
            }

            var rows = Rows();
            var signature = (Movable ? "move|" : "") + string.Join("\n", rows.ToArray());
            if (!force && signature == _drawn && _root != null)
            {
                _root.SetActive(Movable || rows.Count > 0);
                return;
            }

            Draw(rows, signature);
        }

        /// <summary>
        /// What the panel should say, in the order the list shows it. The mod's name is optional
        /// because it is only sometimes what you need: two mods naming an action the same thing
        /// makes it essential, and everywhere else it is a wider box for nothing.
        /// </summary>
        private static List<string> Rows() =>
            BindRegistry.All
                .Where(b => b.Combo.IsBound && HintChoice.Shows(b.Id) && SituationNow.Shows(b))
                .OrderBy(b => b.OwnerName)
                .ThenBy(b => b.Label)
                .Select(Row)
                .ToList();

        private static string Row(BindEntry bind)
        {
            var action = Plugin.HintModNames ? $"{bind.OwnerName} - {bind.Label}" : bind.Label;

            return Plugin.HintOrder == HintOrder.KeyFirst
                ? $"{bind.Combo}   {action}"
                : $"{action}   {bind.Combo}";
        }

        private static void Draw(List<string> rows, string signature)
        {
            if (rows.Count == 0 && !Movable)
            {
                Hide();
                _drawn = signature;
                return;
            }

            // The GUI may not exist yet - the hints can be switched on before Jotunn has built
            // its canvas. Leave the record of what was drawn alone so this is tried again rather
            // than remembered as done.
            if (_root == null && !Build()) return;

            _root.SetActive(true);

            _background.color = Movable ? new Color(0.35f, 0.25f, 0.1f, 0.8f) : new Color(0f, 0f, 0f, 0.45f);
            _background.raycastTarget = Movable;
            _drag.enabled = Movable;

            // Siblings later in the canvas draw on top and get the pointer first. The panel is
            // built after the overlay, so without this a box left under it could not be grabbed.
            if (Movable) _root.transform.SetAsLastSibling();

            // Unparent before destroying: Destroy only takes effect at the end of the frame, and
            // a row still in the layout group is a row the layout still makes room for, which
            // shows up as one frame of doubled text every time the list changes.
            for (var i = _list.childCount - 1; i >= 0; i--)
            {
                var row = _list.GetChild(i).gameObject;
                row.transform.SetParent(null, false);
                Object.Destroy(row);
            }

            if (Movable)
            {
                AddRow("drag to move", GUIManager.Instance.ValheimOrange);
                if (rows.Count == 0)
                    AddRow("nothing applies right now", new Color(1f, 1f, 1f, 0.5f));
            }

            foreach (var row in rows) AddRow(row, Color.white);

            // Last, once the rows are in: the box is sized from its contents, and where it fits
            // on screen depends on how big it just became.
            Place();
            _drawn = signature;
        }

        private static bool Build()
        {
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null) return false;

            _root = new GameObject("BindruneHints", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(HintDrag));
            _root.transform.SetParent(GUIManager.CustomGUIFront.transform, false);

            _background = _root.GetComponent<Image>();
            _background.color = new Color(0f, 0f, 0f, 0.45f);
            _background.raycastTarget = false;

            var layout = _root.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            // Every row takes the full width of the box, which is what gives the alignment
            // setting something to align against: rows sized to their own text are already
            // exactly as wide as the words in them, and centring one does nothing.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 2f;

            var fitter = _root.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _list = (RectTransform)_root.transform;

            // Bottom left of the canvas, with the pivot to match, so the saved position means the
            // box's own bottom left corner and the box grows up and to the right from it.
            _list.anchorMin = _list.anchorMax = _list.pivot = Vector2.zero;

            _drag = _root.GetComponent<HintDrag>();
            _drag.enabled = false;
            _drag.Moved = Remember;

            return true;
        }

        /// <summary>Puts the box where it was left, as a fraction of whatever screen this is.</summary>
        private static void Place()
        {
            // Mid-drag the pointer is the authority, not the saved value.
            if (_drag != null && _drag.Dragging) return;

            var parent = _root.transform.parent as RectTransform;
            if (parent == null) return;

            var saved = Plugin.HintPosition;
            var wanted = new Vector2(saved.x * parent.rect.width, saved.y * parent.rect.height);

            // The fitter sizes the box from its rows, so a fresh one has no size to clamp against
            // until the layout has run. Rebuilding it now keeps the first frame from overhanging.
            LayoutRebuilder.ForceRebuildLayoutImmediate(_list);

            // The pivot is the edge that stays put as the list grows, so it follows the growth
            // setting. The saved position always means wherever that pivot is.
            var pivot = new Vector2(0f, Plugin.HintGrowth == HintGrowth.Up ? 0f : 1f);

            if (_list.pivot != pivot)
            {
                // Changing which edge is pinned should not make the box jump across the screen:
                // carry the spot it is standing on over to the new pivot, and save it there.
                wanted.y += (pivot.y - _list.pivot.y) * _list.rect.height;
                _list.pivot = pivot;
                _list.anchoredPosition = HintDrag.Clamped(_list, wanted);
                Remember(_list.anchoredPosition);
                return;
            }

            _list.anchoredPosition = HintDrag.Clamped(_list, wanted);
        }

        private static void Remember(Vector2 position)
        {
            var parent = _root != null ? _root.transform.parent as RectTransform : null;
            if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;

            Plugin.HintPosition = new Vector2(
                position.x / parent.rect.width,
                position.y / parent.rect.height);
        }

        private static TextAnchor Anchor(HintAlign align)
        {
            if (align == HintAlign.Center) return TextAnchor.MiddleCenter;
            return align == HintAlign.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        }

        private static void AddRow(string text, Color color)
        {
            var label = GUIManager.Instance.CreateText(text, _list,
                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                GUIManager.Instance.AveriaSerifBold, Plugin.HintFontSize, color,
                true, Color.black, 320f, Plugin.HintFontSize + 8f, false);

            var component = label.GetComponent<Text>();
            component.alignment = Anchor(Plugin.HintAlign);
            component.horizontalOverflow = HorizontalWrapMode.Overflow;
            component.raycastTarget = false;

            var element = label.GetComponent<LayoutElement>() ?? label.AddComponent<LayoutElement>();
            element.minHeight = Plugin.HintFontSize + 8f;
            element.preferredHeight = Plugin.HintFontSize + 8f;
        }
    }
}
