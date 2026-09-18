using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bindrune.Hints
{
    /// <summary>
    /// Lets the hint panel be dragged anywhere on screen. Its rect is anchored to the bottom left
    /// of the canvas with a matching pivot, so anchoredPosition is simply where the box sits and
    /// dragging is adding the pointer's movement to it.
    ///
    /// Only ever active while the Bindrune panel is open. The rest of the time the overlay takes
    /// no input at all, because a box that swallows clicks is not something you want on your HUD.
    /// </summary>
    public class HintDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Raised when a drag finishes, so the new place can be written down.</summary>
        public Action<Vector2> Moved;

        public bool Dragging { get; private set; }

        public void OnBeginDrag(PointerEventData eventData) => Dragging = true;

        public void OnDrag(PointerEventData eventData)
        {
            var rect = (RectTransform)transform;

            // The canvas scales with the screen, so pointer pixels are not canvas units.
            var canvas = rect.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

            rect.anchoredPosition = Clamped(rect, rect.anchoredPosition + eventData.delta / scale);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Dragging = false;
            Moved?.Invoke(((RectTransform)transform).anchoredPosition);
        }

        /// <summary>
        /// Keeps the whole box on screen, so it can never be dragged out of reach. Works from the
        /// pivot rather than assuming a corner, because the pivot moves with the growth direction:
        /// the position means the box's bottom edge when it grows upwards and its top edge when it
        /// grows down.
        /// </summary>
        public static Vector2 Clamped(RectTransform rect, Vector2 position)
        {
            var parent = rect.parent as RectTransform;
            if (parent == null) return position;

            var size = rect.rect.size;
            var pivot = rect.pivot;
            var room = parent.rect.size - size;

            return new Vector2(
                Mathf.Clamp(position.x, pivot.x * size.x, Mathf.Max(0f, room.x) + pivot.x * size.x),
                Mathf.Clamp(position.y, pivot.y * size.y, Mathf.Max(0f, room.y) + pivot.y * size.y));
        }
    }
}
