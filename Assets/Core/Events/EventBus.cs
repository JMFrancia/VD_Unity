using System;
using System.Collections.Generic;

namespace VoidDay.Core.Events
{
    /// The central event bus (CLAUDE.md rule 2, §15). Plain C# in Core, so the economy can emit without
    /// touching Unity. Emitters describe what happened; listeners decide what to do. Systems talk only here —
    /// no system holds a reference to, or calls a method on, another system.
    ///
    /// Type-keyed: one event type = one channel. Publishing to a channel with no listeners is a no-op
    /// (an event with no one interested is legitimate, not an error).
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.TryGetValue(typeof(T), out var existing);
            _handlers[typeof(T)] = (Action<T>)existing + handler;
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!_handlers.TryGetValue(typeof(T), out var existing)) return;
            var next = (Action<T>)existing - handler;
            if (next == null) _handlers.Remove(typeof(T));
            else _handlers[typeof(T)] = next;
        }

        public void Publish<T>(T evt)
        {
            if (_handlers.TryGetValue(typeof(T), out var d))
                ((Action<T>)d).Invoke(evt);
        }
    }
}
