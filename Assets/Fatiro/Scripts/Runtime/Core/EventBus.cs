using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fatiro.Core
{
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T message);
    }

    /// <summary>
    /// Observer pattern: typed publish/subscribe. Publishers never hold references to listeners,
    /// so UI, audio and persistence can react to simulation events without coupling.
    /// One failing handler is logged and does not stop the others.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();
        private readonly List<Delegate> dispatchBuffer = new List<Delegate>();
        private int dispatchDepth;

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                handlers.Add(typeof(T), list);
            }
            if (!list.Contains(handler)) list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        }

        public void Publish<T>(T message)
        {
            if (!handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;

            // Copy so handlers may subscribe or unsubscribe while the event is being delivered.
            var snapshot = dispatchDepth == 0 ? dispatchBuffer : new List<Delegate>();
            snapshot.Clear();
            snapshot.AddRange(list);
            dispatchDepth++;
            try
            {
                foreach (var handler in snapshot)
                {
                    try
                    {
                        ((Action<T>)handler).Invoke(message);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                dispatchDepth--;
                snapshot.Clear();
            }
        }
    }
}
