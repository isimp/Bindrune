using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Bindrune.UI
{
    /// <summary>
    /// The panel's drawing primitives: Jotunn widgets wrapped so the rest of the panel can say
    /// what it wants rather than how to build it. Nothing here knows about binds or conflicts.
    /// </summary>
    public static partial class BindrunePanel
    {
        private static GameObject Label(string text, Transform parent, float width, float height, int fontSize, Color color, bool bold = false)
        {
            var go = GUIManager.Instance.CreateText(text, parent,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                bold ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif,
                fontSize, color, true, Color.black, width, height, false);

            var label = go.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return go;
        }

        /// <summary>Text that grows downwards instead of being cut off.</summary>
        private static GameObject Wrapped(string text, Transform parent, float width, int fontSize, Color color, bool bold = false)
        {
            var go = GUIManager.Instance.CreateText(text, parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                bold ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif,
                fontSize, color, true, Color.black, width, 20f, false);

            var label = go.GetComponent<Text>();
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            // No preferred height on the LayoutElement: the fitter derives it from the wrapped
            // text, which is what lets long explanations show in full.
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;

            var fitter = go.GetComponent<ContentSizeFitter>() ?? go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        private static GameObject Button(string text, Transform parent, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            var go = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, width, height);
            go.GetComponent<Button>().onClick.AddListener(onClick);
            return go;
        }

        /// <summary>
        /// A button that keeps the size it was given. Inside a layout group a bare button is
        /// resized by its parent, so nearly every button in the panel wants this one.
        /// </summary>
        private static GameObject FixedButton(string text, Transform parent, float width, float height, UnityEngine.Events.UnityAction onClick)
        {
            var go = Button(text, parent, width, height, onClick);
            Fix(go, width, height);
            return go;
        }

        /// <summary>Text that keeps the size it was given, for the same reason as FixedButton.</summary>
        private static GameObject FixedLabel(string text, Transform parent, float width, float height, int fontSize, Color color, bool bold = false)
        {
            var go = Label(text, parent, width, height, fontSize, color, bold);
            Fix(go, width, height);
            return go;
        }

        private static Transform HorizontalRow(Transform parent, float height)
        {
            var row = new GameObject("buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            Fix(row, DetailWidth - 40f, height);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10f;
            return row.transform;
        }

        private static void Spacer(float height)
        {
            var go = new GameObject("spacer", typeof(RectTransform));
            go.transform.SetParent(_detail, false);
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void Cell(Transform parent, string text, float width, Color color, int fontSize)
        {
            var go = Label(text, parent, width, RowHeight, fontSize, color);
            Fix(go, width, RowHeight);

            var label = go.GetComponent<Text>();
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void Fix(GameObject go, float width, float height)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>Places a widget by its left edge, so the given x is where it starts.</summary>
        private static void AnchorLeft(GameObject go, float x, float y)
        {
            var rect = go.GetComponent<RectTransform>();
            var size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Places a widget by its right edge, so the given negative x is where it ends.</summary>
        private static void AnchorRight(GameObject go, float x, float y)
        {
            var rect = go.GetComponent<RectTransform>();
            var size = rect.rect.size;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void Anchor(GameObject go, Vector2 anchor, Vector2 position)
        {
            var rect = go.GetComponent<RectTransform>();
            var size = rect.rect.size;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
