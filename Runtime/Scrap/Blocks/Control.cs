using UnityEngine;
using System;
using System.Collections.Generic;
using ScrapWeaver;

namespace FireFlower
{
    // Checks once when enabled. The outer stack moves on only after the inner stack finishes, waits included
    [Serializable, Display("if", nameof(condition), "then")]
    public class ConditionBlock : ControlBlock, IScrapStep
    {
        [SerializeReference, SubclassSelector] protected ScrapCondition condition;

        protected virtual ScrapPart[] Otherwise => null;

        public override void OnEnabled(ScrapContext ctx) => Run(ctx, null);

        public Action Run(ScrapContext ctx, Action resume)
        {
            if (condition == null || condition.Check(ctx)) Enable(ctx, resume);
            else Enable(ctx, Otherwise, resume);
            return CancelPendingAction;
        }
    }

    [Serializable, Display("if", nameof(condition), "then")]
    public class IfElseBlock : ConditionBlock
    {
        [SerializeReference, SubclassSelector, Display("else")] ScrapPart[] otherwise;
        protected override ScrapPart[] Otherwise => otherwise;
    }

    // Re-checks every tick and enables/disables. Chances keep the roll made when enabled (ctx.roll)
    [Serializable, Reusable, Display("while", nameof(condition))]
    public class WhileBlock : ControlBlock
    {
        [SerializeReference, SubclassSelector] ScrapCondition condition;

        ScrapContext context;
        bool active;

        public override void OnEnabled(ScrapContext ctx)
        {
            context = ctx.WithRoll();
            active = false;
            Hold(ctx, ctx.SubscribeTick(Reevaluate));
            Hold(ctx, () =>
            {
                if (active) Disable(context);
                active = false;
            });
            Reevaluate();
        }

        void Reevaluate()
        {
            bool pass = condition == null || condition.Check(context);
            if (pass == active) return;
            active = pass;
            if (pass) Enable(context);
            else Disable(context);
        }
    }

    // After the child stack runs to the end, it runs again next tick.
    // Each pass is a fresh copy (reset like one), so effects that revert (stats) stack every repeat
    [Serializable, Reusable]
    public abstract class LoopBlock : ControlBlock
    {
        bool busy;
        int finished;

        protected abstract bool KeepGoing(ScrapContext ctx, int finished);
        protected virtual void Begin(ScrapContext ctx) { }

        public override void OnEnabled(ScrapContext ctx) => Hold(ctx, Run(ctx, null));

        public Action Run(ScrapContext ctx, Action resume)
        {
            busy = false;
            finished = 0;
            Begin(ctx);
            Action unsubscribe = null;
            bool over = false;
            Action next = () =>
            {
                busy = false;
                finished++;
            };
            void Step()
            {
                if (busy || over) return;
                if (!KeepGoing(ctx, finished))
                {
                    over = true;
                    unsubscribe?.Invoke();
                    resume?.Invoke();
                    return;
                }
                busy = true;
                Spawn(ctx, next);
            }
            unsubscribe = ctx.SubscribeTick(Step);
            Step();
            return () =>
            {
                over = true;
                unsubscribe();
                CancelPending();
            };
        }

    }

    // Holds the stack like Scratch (blocks after it never run). To run it alongside, put it inside [run separately]
    [Serializable, Cap, Display("forever")]
    public class ForeverBlock : LoopBlock, IScrapStep
    {
        protected override bool KeepGoing(ScrapContext ctx, int finished) => true;
    }

    // Runs the inner stack alongside while the outer stack moves on immediately. The inner stack stops when this effect (state) is disabled
    [Serializable, Display("run separately")]
    public class ParallelBlock : ControlBlock
    {
        public override void OnEnabled(ScrapContext ctx) => Spawn(ctx);
    }

    // Runs the inner line once per object whose collider is within the radius, nearest first. Drag the (object) pill from the header to use it.
    // Objects with a Rigidbody2D count once (as the body). The running object itself is skipped
    [Serializable, Display("for each object within", nameof(radius), "of", nameof(center), "on", nameof(layers))]
    public class ForEachNearbyBlock : ControlBlock, IElementSource
    {
        [SerializeField, HideInInspector] string element = Guid.NewGuid().ToString("N");
        public string ElementId => element;
        public string ElementName => "object";

        [SerializeReference, SubclassSelector] Vector2Source center;
        [SerializeReference, SubclassSelector] ValueSource radius;
        [SerializeField] LayerMask layers = ~0;
        [SerializeReference, SubclassSelector, Display("at most")] ValueSource max;

        static readonly Collider2D[] buffer = new Collider2D[64];
        static readonly List<Transform> found = new();

        public override void OnEnabled(ScrapContext ctx)
        {
            if (center == null || !center.TryRead(ctx, out Vector2 c)) return;
            int count = Physics2D.OverlapCircle(c, ValueSource.ReadOr(ctx, radius, 0f), new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = true }, buffer);
            found.Clear();
            for (int i = 0; i < count; i++)
            {
                Transform t = buffer[i].attachedRigidbody != null ? buffer[i].attachedRigidbody.transform : buffer[i].transform;
                if (t == ctx.selfTransform || found.Contains(t)) continue;
                found.Add(t);
            }
            found.Sort((a, b) => ((Vector2)a.position - c).sqrMagnitude.CompareTo(((Vector2)b.position - c).sqrMagnitude));
            int left = Mathf.RoundToInt(ValueSource.ReadOr(ctx, max, 0f));
            // Copy first: inner lines may run another search and reuse the shared list
            foreach (Transform t in found.GetRange(0, Mathf.Min(left, found.Count)))
                if (t != null) Spawn(ctx.WithElement(element, t));
        }
    }

    [Serializable, Reusable, Display("repeat", nameof(count))]
    public class RepeatBlock : LoopBlock, IScrapStep
    {
        [SerializeReference, SubclassSelector] ValueSource count;
        int times;
        protected override void Begin(ScrapContext ctx) => times = Mathf.RoundToInt(ValueSource.ReadOr(ctx, count, 0f));
        protected override bool KeepGoing(ScrapContext ctx, int finished) => finished < times;
    }

    [Serializable, Display("repeat until", nameof(condition))]
    public class RepeatUntilBlock : LoopBlock, IScrapStep
    {
        [SerializeReference, SubclassSelector] ScrapCondition condition;
        protected override bool KeepGoing(ScrapContext ctx, int finished) => condition == null || !condition.Check(ctx);
    }

    [Serializable, Display("for", nameof(seconds), "s")]
    public class DurationBlock : ControlBlock
    {
        [SerializeReference, SubclassSelector] ValueSource seconds;

        public override void OnEnabled(ScrapContext ctx)
        {
            bool on = true;
            Enable(ctx);
            Action cancel = ctx.After(ValueSource.ReadOr(ctx, seconds, 0f), () =>
            {
                on = false;
                Disable(ctx);
            });
            Hold(ctx, () =>
            {
                cancel();
                if (on) Disable(ctx);
                on = false;
            });
        }
    }

}
