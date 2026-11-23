using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using ScrapWeaver;

namespace FireFlower
{
    [Serializable, Display("block")]
    public abstract class ScrapPart
    {
        Action release;
        // Slot + 1 in the runner's held list (0 = none)
        internal int stopSlot;

        public abstract void OnEnabled(ScrapContext ctx);

        // Call base when overriding
        public virtual void OnDisabled(ScrapContext ctx)
        {
            ctx.UnsubscribeStop(this);
            var r = release;
            release = null;
            r?.Invoke();
        }

        // onStop is called once when the effect stops or the parent stops this part
        protected void Hold(ScrapContext ctx, Action onStop)
        {
            if (onStop == null) return;
            if (release == null) ctx.SubscribeStop(this);
            release += onStop;
        }

        // What the original holds and its run machinery (line cursors, idle copies) are not handed to the copy
        internal ScrapPart ShallowCopy()
        {
            var copy = (ScrapPart)MemberwiseClone();
            copy.release = null;
            copy.stopSlot = 0;
            copy.OnCopied();
            return copy;
        }
        // Clear fields bound to the original (cached delegates, timers) so the copy makes its own
        private protected virtual void OnCopied() { }
        // Holds things to revert on stop. A copy that holds something is not reused
        internal bool Holding => release != null;
    }

    [Serializable, Category("Action")]
    public abstract class ActionBlock : ScrapPart { }

    [Serializable]
    public abstract class ParentBlock : ScrapPart
    {
        [SerializeReference, SubclassSelector, FormerlySerializedAs("triggers"), FormerlySerializedAs("effects")]
        ScrapPart[] children;
        // Lines paused on a wait that have not fully started yet
        List<Line> waiting;
        // The line that starts the original children. Usually one at a time, so it is reused (a new one if they overlap)
        Line enableLine;
        Action cancelPending;
        // Idle copies that finished running. Reused for each repeat loop, trigger fire and My Blocks call instead of cloning (several if they overlap)
        internal List<ScrapCloner.CopySet> idle;

        protected ScrapPart[] Children { get => children; set => children = value; }

        private protected override void OnCopied()
        {
            waiting = null;
            enableLine = null;
            cancelPending = null;
            idle = null;
        }

        // Starts the original. done is called once it has run to the end, waits included
        protected void Enable(ScrapContext ctx, Action done = null) => Enable(ctx, children, done);
        protected void Enable(ScrapContext ctx, ScrapPart[] parts, Action done = null)
        {
            if (enableLine == null || enableLine.Busy) enableLine = new Line(this);
            enableLine.Start(parts, ctx, done);
        }

        // Makes a copy and starts it. A copy with something to release subscribes to Stop itself.
        // Reuses an idle copy by resetting its runtime fields to the original's values (same as a fresh clone). A finished copy still holding something is not put to rest
        protected void Spawn(ScrapContext ctx, Action done = null)
        {
            ScrapCloner.CopySet set;
            if (idle is { Count: > 0 })
            {
                set = idle[^1];
                idle.RemoveAt(idle.Count - 1);
                set.Reset();
            }
            else set = new ScrapCloner.CopySet(children, this);
            set.Done = done;
            set.Line.Start(set.Parts, ctx, set.Finish);
        }

        protected void Disable(ScrapContext ctx)
        {
            CancelPending();
            foreach (ScrapPart c in children ?? Array.Empty<ScrapPart>()) c?.OnDisabled(ctx);
        }

        public override void OnDisabled(ScrapContext ctx)
        {
            CancelPending();
            base.OnDisabled(ctx);
        }

        protected void CancelPending()
        {
            if (waiting == null) return;
            while (waiting.Count > 0)
            {
                Line line = waiting[^1];
                waiting.RemoveAt(waiting.Count - 1);
                line.Cancel();
            }
        }

        // The cancel to return from IScrapStep.Run. Not a new delegate every time
        protected Action CancelPendingAction => cancelPending ??= CancelPending;

        // Starts one line top to bottom. On a stack-pausing block (IScrapStep) it waits, then continues.
        // The resume delegate is made once and reused, so waiting allocates nothing
        internal sealed class Line
        {
            readonly ParentBlock owner;
            readonly Action resume;
            ScrapPart[] parts;
            ScrapContext ctx;
            Action done;
            Action cancel;
            int next;
            bool inStep, resumedAtOnce, waiting;

            public Line(ParentBlock owner)
            {
                this.owner = owner;
                resume = Resume;
            }

            public bool Busy { get; private set; }

            public void Start(ScrapPart[] parts, ScrapContext ctx, Action done)
            {
                Busy = true;
                this.parts = parts;
                this.ctx = ctx;
                this.done = done;
                Continue(0);
            }

            void Continue(int start)
            {
                for (int i = start; i < (parts?.Length ?? 0); i++)
                {
                    ScrapPart part = parts[i];
                    if (part == null) continue;
                    if (part is not IScrapStep step)
                    {
                        part.OnEnabled(ctx);
                        continue;
                    }
                    next = i + 1;
                    resumedAtOnce = false;
                    inStep = true;
                    Action c = step.Run(ctx, resume);
                    inStep = false;
                    if (resumedAtOnce) continue;
                    cancel = c;
                    waiting = true;
                    (owner.waiting ??= new List<Line>()).Add(this);
                    return;
                }
                Action finished = done;
                Clear();
                finished?.Invoke();
            }

            // A step that finished at once calls this inside Run. A late call after finishing or cancelling is dropped
            void Resume()
            {
                if (inStep)
                {
                    resumedAtOnce = true;
                    return;
                }
                if (!waiting) return;
                waiting = false;
                cancel = null;
                owner.waiting?.Remove(this);
                Continue(next);
            }

            public void Cancel()
            {
                if (!waiting) return;
                Action c = cancel;
                Clear();
                c?.Invoke();
            }

            void Clear()
            {
                waiting = false;
                cancel = null;
                parts = null;
                done = null;
                ctx = default;
                Busy = false;
            }
        }
    }

    // Runs a copy of the children each time the event fires
    [Serializable, Category("Events")]
    public abstract class FireBlock : ParentBlock
    {
        public override void OnEnabled(ScrapContext ctx) => Hold(ctx, Subscribe(ctx));
        // Returns the unsubscribe Action
        protected abstract Action Subscribe(ScrapContext ctx);
    }

    [Serializable, Category("Control")]
    public abstract class ControlBlock : ParentBlock { }

    // Definition hat placed on its own on the canvas (function, buff). Starts before the root stacks
    [Serializable]
    public abstract class DefinitionBlock : ParentBlock
    {
        [HideInInspector] public Vector2 position;
    }

    // A block that pauses its stack. The runner calls Run instead of OnEnabled and moves to the next block when resume is called.
    // Calling the returned Action cancels it while paused
    public interface IScrapStep
    {
        Action Run(ScrapContext ctx, Action resume);
    }
}
