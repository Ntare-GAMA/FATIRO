using System;
using Fatiro.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Fatiro.UI
{
    /// <summary>
    /// A component card in the build tray. Tap to select; drag it out over the scene and release on
    /// the glowing ghost to build it (FR-10). Horizontal swipes still scroll the tray.
    /// </summary>
    public sealed class TrayCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ScrollRect scroll;
        private bool draggingMember;

        public BuildComponent Component { get; private set; }
        public event Action<BuildComponent> Clicked;
        public event Action<BuildComponent, Vector2> DragStarted;
        public event Action<Vector2> Dragged;
        public event Action<Vector2> DragEnded;

        public void Bind(BuildComponent component, ScrollRect parentScroll)
        {
            Component = component;
            scroll = parentScroll;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging) Clicked?.Invoke(Component);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Mostly-vertical drags pull the component out of the tray; sideways drags scroll the tray.
            var delta = eventData.delta;
            draggingMember = Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 0.8f;
            if (draggingMember) DragStarted?.Invoke(Component, eventData.position);
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
