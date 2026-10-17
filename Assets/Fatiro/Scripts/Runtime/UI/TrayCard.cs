using System;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// A member card in the build tray. Tap to select; drag it out over the scene and release on
    /// the glowing ghost to place it (FR-10). Horizontal swipes still scroll the tray.
    /// </summary>
    public sealed class TrayCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ScrollRect scroll;
        private bool draggingMember;

        public MemberSpec Spec { get; private set; }
        public event Action<MemberSpec> Clicked;
        public event Action<MemberSpec, Vector2> DragStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> DragEnded;

        public void Bind(MemberSpec spec, ScrollRect parentScroll)
        {
            Spec = spec;
            scroll = parentScroll;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging) Clicked?.Invoke(Spec);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Mostly-vertical drags pull the member out of the tray; sideways drags scroll the tray.
            var delta = eventData.delta;
            draggingMember = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 0.8f;
            if (draggingMember) DragStarted?.Invoke(Spec, eventData.position);
            else scroll?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (draggingMember) Dragged?.Invoke(eventData.position);
            else scroll?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (draggingMember) DragEnded?.Invoke(eventData.position);
            else scroll?.OnEndDrag(eventData);
            draggingMember = false;
        }
    }
}
