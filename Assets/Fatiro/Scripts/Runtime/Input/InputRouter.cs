using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Fatiro.Input
{
    public enum GestureType
    {
        Tap,
        DragStart,
        Drag,
        DragEnd,
        Pinch,
        Twist
    }

    /// <summary>One recognised gesture in screen space.</summary>
    public readonly struct Gesture
    {
        public Gesture(GestureType type, Vector2 position, Vector2 delta = default, float scale = 1f, float twistDegrees = 0f)
        {
            Type = type;
            Position = position;
            Delta = delta;
            Scale = scale;
            TwistDegrees = twistDegrees;
        }

        public GestureType Type { get; }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }

        /// <summary>Pinch: ratio of the current finger distance to the previous one.</summary>
        public float Scale { get; }

        /// <summary>Twist: rotation of the two-finger line since the previous frame.</summary>
        public float TwistDegrees { get; }
    }

    /// <summary>
    /// Turns raw touches (Input System EnhancedTouch) into taps, drags, pinches and twists.
    /// Touches that start on UI are ignored so buttons never place or move anything.
    /// In the editor the mouse stands in: click/drag, scroll to pinch, right-drag to twist.
    /// </summary>
    public sealed class InputRouter : MonoBehaviour
    {
        private const float TapMaxSeconds = 0.35f;
        private const float DragThresholdPixels = 18f;

        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        private bool tracking;
        private bool dragging;
        private bool startedOverUi;
        private Vector2 startPosition;
        private Vector2 lastPosition;
        private float startTime;
        private float lastPinchDistance;
        private float lastTwistAngle;
        private bool twoFinger;
        private bool touchActive;
        private PointerEventData pointerData;

        public event Action<Gesture> GestureRecognised;

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            var touches = Touch.activeTouches;
            if (touches.Count > 0)
            {
                touchActive = true;
                HandleTouches(touches);
                return;
            }

            if (touchActive)
            {
                // Fingers lifted without an Ended phase being seen: finish the gesture cleanly.
                if (tracking && !twoFinger) EndPointer(lastPosition);
                tracking = false;
                twoFinger = false;
                lastPinchDistance = 0f;
                touchActive = false;
                return;
            }

            HandleMouse();
        }

        private void HandleTouches(UnityEngine.InputSystem.Utilities.ReadOnlyArray<Touch> touches)
        {
            if (touches.Count >= 2)
            {
                var a = touches[0].screenPosition;
                var b = touches[1].screenPosition;
                var distance = Vector2.Distance(a, b);
                var angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                if (!twoFinger)
                {
                    if (dragging && !startedOverUi) Raise(new Gesture(GestureType.DragEnd, lastPosition));
                    twoFinger = true;
                    dragging = false;
                    tracking = false;
                    lastPinchDistance = 0f;
                    startedOverUi = IsOverUi(a) || IsOverUi(b);
                }
                else if (!startedOverUi)
                {
                    if (lastPinchDistance > 1f) Raise(new Gesture(GestureType.Pinch, (a + b) * 0.5f, scale: distance / lastPinchDistance));
                    Raise(new Gesture(GestureType.Twist, (a + b) * 0.5f, twistDegrees: Mathf.DeltaAngle(lastTwistAngle, angle)));
                }
                lastPinchDistance = distance;
                lastTwistAngle = angle;
                return;
            }

            if (twoFinger) return; // wait for all fingers to lift before single-finger gestures resume

            var touch = touches[0];
            var position = touch.screenPosition;
            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    BeginPointer(position);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    MovePointer(position);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    EndPointer(position);
                    break;
            }
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var position = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame) BeginPointer(position);
            else if (mouse.leftButton.isPressed && tracking) MovePointer(position);
            else if (mouse.leftButton.wasReleasedThisFrame && tracking) EndPointer(position);

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUi(position))
                Raise(new Gesture(GestureType.Pinch, position, scale: 1f + Mathf.Sign(scroll) * 0.06f));

            if (mouse.rightButton.isPressed && !IsOverUi(position))
            {
                var delta = mouse.delta.ReadValue();
                if (Mathf.Abs(delta.x) > 0.01f) Raise(new Gesture(GestureType.Twist, position, twistDegrees: -delta.x * 0.4f));
            }
        }

        private void BeginPointer(Vector2 position)
        {
            tracking = true;
            dragging = false;
            startedOverUi = IsOverUi(position);
            startPosition = position;
            lastPosition = position;
            startTime = Time.unscaledTime;
        }

        private void MovePointer(Vector2 position)
        {
            if (!tracking || startedOverUi) return;
            if (!dragging && Vector2.Distance(position, startPosition) > DragThresholdPixels)
            {
                dragging = true;
                Raise(new Gesture(GestureType.DragStart, startPosition));
            }

            if (dragging) Raise(new Gesture(GestureType.Drag, position, position - lastPosition));
            lastPosition = position;
        }

        private void EndPointer(Vector2 position)
        {
            if (!tracking) return;
            tracking = false;
            if (startedOverUi) return;

            if (dragging) Raise(new Gesture(GestureType.DragEnd, position));
            else if (Time.unscaledTime - startTime <= TapMaxSeconds) Raise(new Gesture(GestureType.Tap, position));
            dragging = false;
        }

        private void Raise(Gesture gesture) => GestureRecognised?.Invoke(gesture);

        private bool IsOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            pointerData ??= new PointerEventData(eventSystem);
            pointerData.position = screenPosition;
            UiHits.Clear();
            eventSystem.RaycastAll(pointerData, UiHits);
            return UiHits.Count > 0;
        }
    }
}
