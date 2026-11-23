using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // ── Operation kinds ─────────────────────────
    // Only AddPure adds the raw value; every other Value is a percentage.
    // AddPer and Mul are relative to the current value (20 → +20%, 150 → ×1.5); Set, Floor and Cap are relative to Home (50 → half the base).
    // Enum values are serialized, so don't reorder or insert in the middle — StatMod.ApplyOrder decides the application order.
    public enum StatOp
    {
        AddPer,       // additive
        Mul,       // multiplicative
        Set,  // set
        Floor,     // max(v, value)
        Cap,       // min(v, value)
        AddPure,   // raw value change - for percentage stats etc.
    }

    public class StatMod
    {
        public readonly int Priority;
        public readonly StatOp Op;
        public readonly float Value;

        public StatMod(StatOp op, float value) : this(op, value, DefaultPriority(op)) { }
        public StatMod(StatOp op, float value, int priority) { Op = op; Value = value; Priority = priority; }

        // Default order per op. Spaced by 100 → room for custom ones in between
        public static int DefaultPriority(StatOp op) => op switch
        {
            StatOp.AddPure => 0,
            StatOp.AddPer => 0,
            StatOp.Mul => 100,
            StatOp.Set => 200,
            StatOp.Floor => 300,
            StatOp.Cap => 300,
            _ => 0,
        };

        // Application order within the same priority. Plain adds first, clamps last
        public static readonly StatOp[] ApplyOrder =
        {
            StatOp.AddPure, StatOp.AddPer, StatOp.Mul, StatOp.Set, StatOp.Floor, StatOp.Cap,
        };

        // Percentage → multiplier. 20 → 1.2 / 150 → 1.5
        public static float AddRate(float percent) => 1f + percent / 100f;
        public static float MulRate(float percent) => percent / 100f;
        // Percent of baseV. Baseline for Set, Floor and Cap
        public static float Of(float baseV, float percent) => baseV * MulRate(percent);

        public float Apply(float v, float baseV) => Op switch
        {
            StatOp.AddPure => v + Value,
            StatOp.AddPer => v * AddRate(Value),
            StatOp.Mul => v * MulRate(Value),
            StatOp.Set => Of(baseV, Value),
            StatOp.Floor => Math.Max(v, Of(baseV, Value)),
            StatOp.Cap => Math.Min(v, Of(baseV, Value)),
            _ => v,
        };
    }

    public abstract class StatBase { }

    // Common base for numeric stats with mods. Computed as float, then Round fits the type at each step (ints are rounded)
    public abstract class ModStat<T> : StatBase
    {
        readonly List<StatMod> mods = new();     // Always kept in ascending Priority
        float baseValue;

        public event Action<T> OnChanged;    // (old) — subscribe only for stats that need push (MaxHp, etc.)
        public T Value { get; private set; }
        // Recomputes immediately when changed
        public T Base
        {
            get => FromFloat(baseValue);
            set
            {
                baseValue = ToFloat(value);
                Refresh();
            }
        }

        protected ModStat(float baseValue)
        {
            this.baseValue = baseValue;
            Value = Compute();
        }

        protected abstract T FromFloat(float v);
        protected abstract float ToFloat(T v);
        protected virtual float Round(float v) => v;

        public void Add(StatMod m)
        {
            int i = mods.Count;
            while (i > 0 && mods[i - 1].Priority > m.Priority) i--;
            mods.Insert(i, m);
            Refresh();
        }

        public void Remove(StatMod m)
        {
            if (mods.Remove(m)) Refresh();
        }

        // Removal by value (first mod matching op+value). For when no mod reference is held
        public void Remove(StatOp op, float value)
        {
            for (int i = 0; i < mods.Count; i++)
                if (mods[i].Op == op && mods[i].Value == value) { mods.RemoveAt(i); Refresh(); return; }
        }

        void Refresh()
        {
            T old = Value;
            Value = Compute();
            if (!EqualityComparer<T>.Default.Equals(old, Value)) OnChanged?.Invoke(old);
        }

        // Applies mods of the same priority in ApplyOrder; priority groups stack multiplicatively
        T Compute()
        {
            float v = baseValue;
            int idx = 0;
            while (idx < mods.Count)
            {
                int p = mods[idx].Priority;
                int end = idx;
                while (end < mods.Count && mods[end].Priority == p) end++;

                foreach (var op in StatMod.ApplyOrder)
                {
                    // Additive: AddPer mods of the same priority sum their percentages and apply once
                    if (op == StatOp.AddPer)
                    {
                        float sum = 0f; bool any = false;
                        for (int k = idx; k < end; k++)
                            if (mods[k].Op == StatOp.AddPer) { sum += mods[k].Value; any = true; }
                        if (any) v = Round(v * StatMod.AddRate(sum));
                    }
                    else
                    {
                        for (int k = idx; k < end; k++)
                            if (mods[k].Op == op) v = Round(mods[k].Apply(v, baseValue));
                    }
                }
                idx = end;
            }
            return FromFloat(v);
        }
    }

    public sealed class FloatStat : ModStat<float>
    {
        public FloatStat(float baseValue) : base(baseValue) { }
        protected override float FromFloat(float v) => v;
        protected override float ToFloat(float v) => v;
    }

    public sealed class IntStat : ModStat<int>
    {
        public IntStat(int baseValue) : base(baseValue) { }
        protected override int FromFloat(float v) => Mathf.RoundToInt(v);
        protected override float ToFloat(int v) => v;
        protected override float Round(float v) => Mathf.RoundToInt(v);
    }

    public class FlagStat : StatBase
    {
        readonly HashSet<object> tokens = new();
        public bool Active => tokens.Count > 0;
        public event Action<bool> OnActiveChanged;   // Notifies only when Active flips false↔true

        public object Add()  // returns a handle
        {
            bool was = Active;
            var t = new object(); tokens.Add(t);
            if (!was) OnActiveChanged?.Invoke(true);
            return t;
        }
        public void Remove(object t)
        {
            if (!tokens.Remove(t)) return;
            if (tokens.Count == 0) OnActiveChanged?.Invoke(false);
        }
        public void Clear()
        {
            if (!Active) return;
            tokens.Clear();
            OnActiveChanged?.Invoke(false);
        }
    }
    public class StatSheet
    {
        readonly Dictionary<string, StatBase> stats = new();


        public FloatStat Float(string key) => (FloatStat)stats[key];
        public IntStat Int(string key) => (IntStat)stats[key];
        public FlagStat Flag(string key) => (FlagStat)stats[key];
        public bool Has(string key) => stats.ContainsKey(key);
        public bool IsFlag(string key) => stats.TryGetValue(key, out var s) && s is FlagStat;
        public bool TryRead(string key, out float value)
        {
            stats.TryGetValue(key, out var s);
            value = s switch { FloatStat f => f.Value, IntStat i => i.Value, FlagStat g => g.Active ? 1f : 0f, _ => 0f };
            return s != null;
        }

        // Path for adding mods without knowing the type (item effects, etc.). Accepts Float or Int.
        public void AddMod(string key, StatMod m)
        {
            switch (stats[key])
            {
                case FloatStat f: f.Add(m); break;
                case IntStat i: i.Add(m); break;
            }
        }
        public void RemoveMod(string key, StatOp op, float value)
        {
            switch (stats[key])
            {
                case FloatStat f: f.Remove(op, value); break;
                case IntStat i: i.Remove(op, value); break;
            }
        }

        // Capabilities register their stats in Init (only Base is updated if it exists)
        public FloatStat RequireFloat(string key, float baseValue)
        {
            if (stats.TryGetValue(key, out var s)) { ((FloatStat)s).Base = baseValue; return (FloatStat)s; }
            var f = new FloatStat(baseValue);
            stats[key] = f;
            return f;
        }
        // Uses the existing stat, creating it with the default only if missing. Never overwrites a Base set by someone else
        public FloatStat EnsureFloat(string key, float fallback) =>
            stats.TryGetValue(key, out var s) ? (FloatStat)s : RequireFloat(key, fallback);
        public FlagStat RequireFlag(string key)
        {
            if (stats.TryGetValue(key, out var s)) return (FlagStat)s;
            var f = new FlagStat();
            stats[key] = f;
            return f;
        }
        public IntStat RequireInt(string key, int baseValue)
        {
            if (stats.TryGetValue(key, out var s)) { ((IntStat)s).Base = baseValue; return (IntStat)s; }
            var i = new IntStat(baseValue);
            stats[key] = i;
            return i;
        }

    }

    // Authoring entry for one mod (key, op, value) that external sources such as items add to a StatSheet.
    [Serializable] public struct StatModEntry { public string key; public StatOp op; public float value; }

    // Draws a string field as a StatKeys dropdown. sheetField names the sibling enum field that filters the keys (Player/Mob/Debuff; Target/Self for StatValue)
    public class StatKeyAttribute : PropertyAttribute
    {
        public readonly string SheetField;
        // When on, FlagStat keys (incapacitated, invincible, etc.) are also offered. Off for fields that add mods
        public readonly bool Flags;
        public StatKeyAttribute(string sheetField = null, bool flags = false)
        {
            SheetField = sheetField;
            Flags = flags;
        }
    }
}
