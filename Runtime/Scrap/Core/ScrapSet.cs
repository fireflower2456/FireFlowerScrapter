using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FireFlower
{
    [Serializable]
    public struct VariableDecl
    {
        public string name;
        public VariableType type;
        // Component name for Component variables (Rigidbody2D, etc.)
        public string component;
        public VariableDecl(string name, VariableType type, string component = null)
        {
            this.name = name;
            this.type = type;
            this.component = component;
        }
    }

    // The source never runs; a copy is made and run, so the same data can be enabled in many places at once
    [Serializable]
    public class ScrapSet
    {
        [SerializeReference, SubclassSelector] public ScrapPart[] parts;
        [SerializeReference, HideInInspector] public DefinitionBlock[] definitions;
        // Enabled together with the blocks
        [SerializeReference, SubclassSelector] public ScrapScript[] scripts;
        // Local variable names. Only used as the editor's pick list
        [HideInInspector] public List<string> variables = new();
        // Non-number local variables (name, kind)
        [HideInInspector] public List<VariableDecl> typedVariables = new();

        // Definitions must be enabled first so the root line can call them
        public ScrapPart[] All
        {
            get
            {
                if (all != null && ReferenceEquals(allFrom.definitions, definitions) && ReferenceEquals(allFrom.parts, parts) && ReferenceEquals(allFrom.scripts, scripts))
                    return all;
                allFrom = (definitions, parts, scripts);
                return all = (definitions ?? Array.Empty<ScrapPart>())
                    .Concat(parts ?? Array.Empty<ScrapPart>())
                    .Concat(scripts ?? Array.Empty<ScrapPart>())
                    .ToArray();
            }
        }
        // Built once and reused while the three arrays are the same (the editor replaces arrays whole)
        [NonSerialized] ScrapPart[] all;
        [NonSerialized] (DefinitionBlock[] definitions, ScrapPart[] parts, ScrapScript[] scripts) allFrom;

        // self is the blocks' `this`: the object running the script. It can be null
        public ScrapRunner Run(Component self = null) =>
            new(All, new ScrapSettings(self));

        // Passes the runner side (cutscene player, etc.) so blocks can find it. If it were named Run, UnityEngine.Object would convert to bool and fall into the overload above
        public ScrapRunner RunWith(Component self, object host) =>
            new(All, new ScrapSettings(self, host));

        // Several runners run as one body (like the states of a state machine). Also enables shared's definitions (My Blocks); local variables share one locals
        public ScrapRunner RunWith(Component self, object host, ScrapSet shared, object locals) =>
            new((shared?.definitions ?? Array.Empty<DefinitionBlock>()).Concat(All).ToArray(), new ScrapSettings(self, host, locals));
    }

    // Asset holding several effect sets (states of a state machine, etc.). The editor checks all of them when finding where variables are used
    public interface IScrapSets
    {
        IEnumerable<ScrapSet> Sets { get; }
    }

    readonly struct ScrapSettings
    {
        public readonly Component self;
        public readonly object host;
        // Owner of local variables. If empty, each runner has its own
        public readonly object locals;

        public ScrapSettings(Component self, object host = null, object locals = null)
        {
            this.self = self;
            this.host = host;
            this.locals = locals;
        }
    }

    public sealed class ScrapRunner
    {
        readonly ScrapSettings settings;
        readonly ScrapContext root;
        readonly Transform selfTransform;
        readonly VariableStore selfStore;
        VariableStore locals;
        Dictionary<string, FunctionBlock> functions;
        // Blocks holding something to revert on stop. Releasing empties the slot (the ScrapPart knows its slot); compacted when many are empty
        readonly List<ScrapPart> holders = new();
        int emptyHolders;
        // Tick subscriptions. Combining delegates allocates a new array on every subscribe/unsubscribe, so keep a list. Unsubscribing empties the slot and it is cleaned up at the end of the tick
        readonly List<Action> ticks = new();
        int emptyTicks;
        bool running = true;
        bool rootDone;

        internal ScrapRunner(ScrapPart[] parts, ScrapSettings settings)
        {
            this.settings = settings;
            if (settings.self != null)
            {
                selfTransform = settings.self.transform;
                selfStore = SelfVariables.Of(settings.self.gameObject);
            }
            // Shared local variables (AI) use their owner's store
            if (settings.locals != null) locals = SelfVariables.Of(settings.locals);
            root = new ScrapContext(this);
            new RootBlock(parts, () => rootDone = true).OnEnabled(root);
        }

        // The root line has run to the end and no block (repeat, wait) is waiting on ticks. Same as a Scratch script finishing
        public bool Finished => rootDone && ticks.Count == emptyTicks;
        // The root line has reached the end (ignores repeats and reapplies running alongside)
        public bool LineDone => rootDone;

        // No default constructor, so it doesn't show in the palette
        sealed class RootBlock : ParentBlock
        {
            readonly Action done;
            public RootBlock(ScrapPart[] parts, Action done)
            {
                Children = parts;
                this.done = done;
            }
            public override void OnEnabled(ScrapContext ctx) => Spawn(ctx, done);
        }

        public void Stop()
        {
            if (!running) return;
            running = false;
            // Stops in the order things were held. Things held while stopping are not run
            int count = holders.Count;
            for (int i = 0; i < count; i++)
            {
                ScrapPart part = holders[i];
                if (part == null) continue;
                holders[i] = null;
                part.stopSlot = 0;
                part.OnDisabled(root);
            }
            holders.Clear();
            emptyHolders = 0;
            ticks.Clear();
            emptyTicks = 0;
        }

        internal ref readonly ScrapSettings Settings => ref settings;
        internal Transform SelfTransform => selfTransform;
        internal VariableStore SelfStore => selfStore;
        internal VariableStore Locals => locals ??= new VariableStore();
        internal ScrapContext Root => root;

        public void Tick()
        {
            // Things subscribed this tick start running next tick
            int count = ticks.Count;
            for (int i = 0; i < count && i < ticks.Count; i++) ticks[i]?.Invoke();
            if (emptyTicks == 0) return;
            ticks.RemoveAll(t => t == null);
            emptyTicks = 0;
        }

        internal void DefineFunction(string id, FunctionBlock function) => (functions ??= new())[id] = function;
        internal FunctionBlock Function(string id) => functions != null && id != null && functions.TryGetValue(id, out var f) ? f : null;

        // stopSlot is slot + 1 (0 = none), so it is right even when deserialization skips constructors
        internal void SubscribeStop(ScrapPart part)
        {
            if (part.stopSlot != 0) return;
            holders.Add(part);
            part.stopSlot = holders.Count;
        }

        internal void UnsubscribeStop(ScrapPart part)
        {
            int slot = part.stopSlot - 1;
            if (slot < 0) return;
            part.stopSlot = 0;
            if (slot >= holders.Count || !ReferenceEquals(holders[slot], part)) return;
            holders[slot] = null;
            if (++emptyHolders < 32 || emptyHolders * 2 < holders.Count) return;
            holders.RemoveAll(h => h == null);
            for (int i = 0; i < holders.Count; i++) holders[i].stopSlot = i + 1;
            emptyHolders = 0;
        }

        // For blocks that reuse the same delegate. Unsubscribing makes no new Action
        internal void AddTick(Action tick) => ticks.Add(tick);

        internal void RemoveTick(Action tick)
        {
            for (int i = ticks.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(ticks[i], tick)) continue;
                ticks[i] = null;
                emptyTicks++;
                return;
            }
        }

        internal Action SubscribeTick(Action tick)
        {
            ticks.Add(tick);
            return () => RemoveTick(tick);
        }
    }
}
