using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // What a block can see while running. The Stop event breaks if copied by value, so ctx only points to the ScrapRunner that owns it.
    // Like C#: self is `this`, payload is the event argument of the trigger that started this line, and elements are the foreach variables of the blocks around.
    // It is copied by value for every block, so settings that are the same for the whole runner live on the runner and only per-line things live here
    public struct ScrapContext
    {
        readonly ScrapRunner owner;
        // The event the trigger passed in (a collision, a hit, a signal …). (other) reads it
        public object payload { get; private set; }
        // What the surrounding element-giving blocks ("for each" …) passed in, innermost first. (element) pills read it
        ElementLink elements;
        // If nonzero, random rolls use this value and give the same result (so chances don't change while being re-checked)
        public int roll { get; private set; }
        // Inputs when inside a function body (booleans as 1/0). Keyed by input id
        public IReadOnlyDictionary<string, float> args { get; private set; }

        internal ScrapContext(ScrapRunner owner) : this() => this.owner = owner;

        // The object running the script (`this`). Null when run without one
        public Component self => owner.Settings.self;
        // Its Transform, looked up once per runner. Null when gone
        public Transform selfTransform => owner.SelfTransform;
        // Whatever started the runner (a player component, a state machine state …), for blocks that need to talk back to it
        public object host => owner.Settings.host;

        // Number, vector and object variables alike. Self scope without a self is treated as local
        public VariableStore Variables(VariableScope scope) => scope switch
        {
            VariableScope.Global => GlobalVariables.Values,
            VariableScope.Persistent => PersistentVariables.Values,
            VariableScope.Self when owner.SelfStore is VariableStore store => store,
            _ => owner.Locals,
        };

        public ScrapContext WithEvent(object payload) { var c = this; c.payload = payload; return c; }
        public ScrapContext WithElement(string id, Component value) { var c = this; c.elements = new ElementLink(id, value, elements); return c; }

        // What block `id` passed in. Null outside that block
        public Component Element(string id)
        {
            for (ElementLink e = elements; e != null; e = e.parent)
                if (e.id == id) return e.value;
            return null;
        }
        public ScrapContext WithRoll() { var c = this; c.roll = UnityEngine.Random.Range(1, int.MaxValue); return c; }
        internal ScrapContext WithArgs(IReadOnlyDictionary<string, float> args) { var c = this; c.args = args; return c; }

        internal ScrapRunner Runner => owner;
        internal void SubscribeStop(ScrapPart part) => owner.SubscribeStop(part);
        internal void UnsubscribeStop(ScrapPart part) => owner.UnsubscribeStop(part);
        public Action SubscribeTick(Action tick) => owner.SubscribeTick(tick);

        // 0 means next tick. Invoke the returned Action to cancel. A block that waits often can keep a ScrapTimer and reuse it to avoid allocating
        public Action After(float seconds, Action then) => new ScrapTimer().Start(this, seconds, then);
    }

    // Counts seconds, then calls then. A block that keeps one and reuses it does not allocate per wait (Start always returns the same cancel)
    public sealed class ScrapTimer
    {
        readonly Action tick;
        readonly Action cancel;
        ScrapRunner runner;
        Action then;
        float left;

        public ScrapTimer()
        {
            tick = Tick;
            cancel = Cancel;
        }

        public bool Running => runner != null;

        public Action Start(ScrapContext ctx, float seconds, Action then)
        {
            Cancel();
            runner = ctx.Runner;
            left = seconds;
            this.then = then;
            runner.AddTick(tick);
            return cancel;
        }

        public void Cancel()
        {
            if (runner == null) return;
            runner.RemoveTick(tick);
            runner = null;
            then = null;
        }

        void Tick()
        {
            left -= Time.deltaTime;
            if (left > 0f) return;
            Action done = then;
            Cancel();
            done?.Invoke();
        }
    }

    // One link of the element chain. ctx is copied by value, so links are stacked, never changed
    sealed class ElementLink
    {
        public readonly string id;
        public readonly Component value;
        public readonly ElementLink parent;

        public ElementLink(string id, Component value, ElementLink parent)
        {
            this.id = id;
            this.value = value;
            this.parent = parent;
        }
    }

    // An event payload that has an "other" object (who hit me, who I touched …). (other) reads it. A payload that is itself a Component also works
    public interface IScrapEvent
    {
        Component Other { get; }
    }
}
