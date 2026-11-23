using System;
using UnityEngine;
using ScrapWeaver;

namespace FireFlower
{
    // Shared between copies, so subscription state is not kept in fields
    [Serializable, Display("event")]
    public abstract class TriggerEvent
    {
        public abstract Action Subscribe(ScrapContext ctx, Action<ScrapContext> fire);
    }

    // Listens to an event of type T. Each time it fires, the line under the hat runs with the event as its payload, so (other) reads it
    [Serializable]
    public abstract class TriggerEvent<T> : TriggerEvent
    {
        protected abstract void Listen(ScrapContext ctx, Action<T> handler, bool on);
        protected virtual bool Accept(ScrapContext ctx, T e) => true;
        protected virtual object PayloadOf(T e) => e;

        public sealed override Action Subscribe(ScrapContext ctx, Action<ScrapContext> fire)
        {
            Action<T> handler = e => { if (Accept(ctx, e)) fire(ctx.WithEvent(PayloadOf(e))); };
            Listen(ctx, handler, true);
            return () => Listen(ctx, handler, false);
        }
    }

    // (other) is the sender
    [Serializable, Display("when I receive signal", nameof(key))]
    public class OnSignal : TriggerEvent<ScrapSignal>
    {
        enum From
        {
            [InspectorName("from anyone")] Anyone,
            [InspectorName("sent by this")] Self,
            [InspectorName("sent to this")] ToSelf,
        }
        [SerializeField] string key;
        [SerializeField] From from;
        protected override void Listen(ScrapContext ctx, Action<ScrapSignal> h, bool on) { if (on) ScrapSignal.Raised += h; else ScrapSignal.Raised -= h; }
        protected override bool Accept(ScrapContext ctx, ScrapSignal s) => s.key == key && from switch
        {
            From.Self => s.sender != null && ctx.self != null && s.sender.gameObject == ctx.self.gameObject,
            From.ToSelf => s.target != null && ctx.self != null && s.target.gameObject == ctx.self.gameObject,
            _ => true,
        };
    }
}
