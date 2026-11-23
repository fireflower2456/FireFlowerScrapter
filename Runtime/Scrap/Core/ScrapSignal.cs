using System;
using UnityEngine;

namespace FireFlower
{
    // A broadcast message with a key. (other) in the receiving script is the sender
    public sealed class ScrapSignal : IScrapEvent
    {
        const int MaxDepth = 16;
        static int depth;

        public readonly string key;
        public readonly Component sender;
        // Optional receiver. "sent to this" filters on it
        public readonly Component target;
        public readonly object payload;

        public Component Other => sender;

        ScrapSignal(string key, Component sender, Component target, object payload)
        {
            this.key = key;
            this.sender = sender;
            this.target = target;
            this.payload = payload;
        }

        public static event Action<ScrapSignal> Raised;

        public static void Raise(string key, ScrapContext ctx, Component target = null) => Raise(key, ctx.self, target, ctx.payload);

        // For sending a signal from code
        public static void Raise(string key, Component sender, Component target = null, object payload = null)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (depth >= MaxDepth)
            {
                Debug.LogWarning($"Signal '{key}' chained more than {MaxDepth} times and was cut off. Check whether signals are triggering each other");
                return;
            }
            depth++;
            try
            {
                var signal = new ScrapSignal(key, sender, target, payload);
                if (sender != null && sender.TryGetComponent(out IScrapSignalListener listener)) listener.Signaled(signal);
                Raised?.Invoke(signal);
            }
            finally { depth--; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Raised = null;
            depth = 0;
        }
    }

    // A component on the sender's object that reacts to every signal it sends (for example to play matching feedback)
    public interface IScrapSignalListener
    {
        void Signaled(ScrapSignal signal);
    }
}
