using System;
using System.Collections.Generic;

namespace DynaraSim.Core
{
    public sealed class EventBus
    {
        private readonly List<Action<WorldEvent>> _subscribers = new List<Action<WorldEvent>>();

        public IDisposable Subscribe(Action<WorldEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _subscribers.Add(handler);
            return new Subscription(_subscribers, handler);
        }

        public void Publish(WorldEvent worldEvent)
        {
            if (worldEvent == null) return;
            Action<WorldEvent>[] snapshot = _subscribers.ToArray();
            for (int i = 0; i < snapshot.Length; i++) snapshot[i](worldEvent);
        }

        private sealed class Subscription : IDisposable
        {
            private List<Action<WorldEvent>> _list;
            private Action<WorldEvent> _handler;

            public Subscription(List<Action<WorldEvent>> list, Action<WorldEvent> handler)
            {
                _list = list;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_list == null || _handler == null) return;
                _list.Remove(_handler);
                _list = null;
                _handler = null;
            }
        }
    }
}
