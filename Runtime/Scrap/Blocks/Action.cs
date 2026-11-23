using System;
using UnityEngine;
using ScrapWeaver;

namespace FireFlower
{
    // Change a value. Numbers, Vector2 and Vector3 support every operation; components only =. Does nothing if the two sockets' types differ.
    // A non-fixed value plugged into a number is re-read every tick and re-applied when it changes. Vector % is per component
    [Serializable, Reusable, Display(nameof(source), nameof(op), nameof(value))]
    public class ApplyBlock : ActionBlock
    {
        enum Op
        {
            [InspectorName("+=")] AddPure = StatOp.AddPure,
            [InspectorName("-=")] Sub = -2,
            [InspectorName("+= %")] AddPer = StatOp.AddPer,
            [InspectorName("*= %")] Mul = StatOp.Mul,
            [InspectorName("=")] Set = -1,
        }
        [SerializeReference, SubclassSelector] DataSource source;
        [SerializeField] Op op = Op.AddPure;
        [SerializeReference, SubclassSelector] DataSource value;

        ScrapContext context;
        float applied;
        Action undo;

        public override void OnEnabled(ScrapContext ctx)
        {
            switch (source)
            {
                case ValueSource when value is ValueSource:
                    ApplyNumber(ctx);
                    break;
                case Vector2Source v when value is Vector2Source by && v.TryRead(ctx, out Vector2 now) && by.TryRead(ctx, out Vector2 amount):
                    Hold(ctx, v.Write(ctx, op switch
                    {
                        Op.Sub => now - amount,
                        Op.Set => amount,
                        Op.AddPer => now + Vector2.Scale(now, amount) / 100f,
                        Op.Mul => Vector2.Scale(now, amount) / 100f,
                        _ => now + amount,
                    }));
                    break;
                case Vector3Source v when value is Vector3Source by && v.TryRead(ctx, out Vector3 now) && by.TryRead(ctx, out Vector3 amount):
                    Hold(ctx, v.Write(ctx, op switch
                    {
                        Op.Sub => now - amount,
                        Op.Set => amount,
                        Op.AddPer => now + Vector3.Scale(now, amount) / 100f,
                        Op.Mul => Vector3.Scale(now, amount) / 100f,
                        _ => now + amount,
                    }));
                    break;
                // = with an empty right side clears the object (none)
                case ObjectSource o when value == null && op == Op.Set:
                    Hold(ctx, o.Write(ctx, (Component)null));
                    break;
                case TransformSource t when value is TransformSource to && op == Op.Set:
                    Hold(ctx, t.Write(ctx, TransformSource.ReadOrNull(ctx, to)));
                    break;
                case ObjectSource o when value is ObjectSource to && op == Op.Set:
                    Hold(ctx, o.Write(ctx, to.TryReadComponent(ctx, out Component c) ? c : null));
                    break;
                case ScrapCondition flag when value is ScrapCondition to && op == Op.Set:
                    Hold(ctx, flag.Write(ctx, to.Check(ctx)));
                    break;
            }
        }

        ValueSource Number => source as ValueSource;
        ValueSource Amount => value as ValueSource;

        void ApplyNumber(ScrapContext ctx)
        {
            if (!Amount.TryRead(ctx, out applied)) return;
            context = ctx;
            undo = Push(ctx, applied);
            if (undo == null) return;
            Hold(ctx, () =>
            {
                undo?.Invoke();
                undo = null;
            });
            if (!Amount.IsConstant) Hold(ctx, ctx.SubscribeTick(Reapply));
        }

        // Keeps the last value if it can't be read
        void Reapply()
        {
            if (!Amount.TryRead(context, out float next) || next == applied) return;
            undo?.Invoke();
            applied = next;
            undo = Push(context, applied);
        }

        // Set adds the difference from the current value. In Reapply the previous one is already reverted, so the difference comes out right again
        Action Push(ScrapContext ctx, float amount)
        {
            if (op == Op.Sub) return Number.Apply(ctx, StatOp.AddPure, -amount);
            if (op != Op.Set) return Number.Apply(ctx, (StatOp)op, amount);
            return Number.TryRead(ctx, out float current) ? Number.Apply(ctx, StatOp.AddPure, amount - current) : null;
        }
    }

    // Each copy keeps one timer and reuses it (waiting inside a repeat allocates nothing)
    [Serializable, Reusable, Category("Control"), Display("wait", nameof(seconds), "seconds")]
    public class WaitBlock : ActionBlock, IScrapStep
    {
        [SerializeReference, SubclassSelector] ValueSource seconds;
        ScrapTimer timer;

        public override void OnEnabled(ScrapContext ctx) { }
        public Action Run(ScrapContext ctx, Action resume) => (timer ??= new ScrapTimer()).Start(ctx, ValueSource.ReadOr(ctx, seconds, 0f), resume);
        private protected override void OnCopied() => timer = null;
    }

    // Checks from the next tick. The tick and cancel delegates are made once per copy
    [Serializable, Reusable, Category("Control"), Display("wait until", nameof(condition))]
    public class WaitUntilBlock : ActionBlock, IScrapStep
    {
        [SerializeReference, SubclassSelector] ScrapCondition condition;
        ScrapRunner runner;
        ScrapContext context;
        Action resume, tick, cancel;

        public override void OnEnabled(ScrapContext ctx) { }

        public Action Run(ScrapContext ctx, Action resume)
        {
            Cancel();
            tick ??= Tick;
            cancel ??= Cancel;
            context = ctx;
            this.resume = resume;
            runner = ctx.Runner;
            runner.AddTick(tick);
            return cancel;
        }

        void Tick()
        {
            if (condition != null && !condition.Check(context)) return;
            Action done = resume;
            Cancel();
            done?.Invoke();
        }

        void Cancel()
        {
            if (runner == null) return;
            runner.RemoveTick(tick);
            runner = null;
            resume = null;
            context = default;
        }

        // So a copy does not inherit delegates bound to the original
        private protected override void OnCopied()
        {
            runner = null;
            resume = tick = cancel = null;
        }
    }

    [Serializable, Display("send signal", nameof(key))]
    public class SignalBlock : ActionBlock
    {
        [SerializeField] string key;
        public override void OnEnabled(ScrapContext ctx) => ScrapSignal.Raise(key, ctx);
    }

}
