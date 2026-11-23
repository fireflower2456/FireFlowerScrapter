using ScrapWeaver;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    public enum VariableScope
    {
        [InspectorName("Local")] Local,
        [InspectorName("Global")] Global,
        [InspectorName("Persistent")] Persistent,
        // Shared by all effects on the same object (mob). Blackboard role
        [InspectorName("Object")] Self,
    }

    // Kind of value a variable holds. Only float can be persistent. int and bool are stored in the number slot too (bool as 0/1)
    public enum VariableType
    {
        [InspectorName("float")] Number,
        [InspectorName("Vector2")] Vector2,
        [InspectorName("Vector3")] Vector3,
        [InspectorName("Transform")] Transform,
        [InspectorName("int")] Int,
        [InspectorName("bool")] Bool,
        // Which component is written in the declaration (VariableDecl.component)
        [InspectorName("Component")] Component,
    }

    // Variable name -> number. A name is looked up once when first used, and stores read the numbered slot directly
    public static class VariableIds
    {
        static readonly Dictionary<string, int> ids = new();
        static readonly List<string> names = new();

        public static int Of(string name)
        {
            if (ids.TryGetValue(name, out int id)) return id;
            ids[name] = id = names.Count;
            names.Add(name);
            return id;
        }

        public static string Name(int id) => names[id];
    }

    // A block's variable number. Not looked up again while the name string is the same (looked up again if the name changes while editing).
    // It only follows the name, so copies may share it
    public struct VariableId
    {
        string of;
        int id;

        public int Of(string key)
        {
            if (ReferenceEquals(of, key)) return id;
            id = VariableIds.Of(key);
            of = key;
            return id;
        }
    }

    // One set of variables. Slots are found by number; numbers and vectors are kept apart so they are never boxed (a Vector2 is a Vector3 with z 0)
    public sealed class VariableStore
    {
        float[] numbers = Array.Empty<float>();
        bool[] written = Array.Empty<bool>();
        Vector3[] vectors = Array.Empty<Vector3>();
        object[] objects = Array.Empty<object>();

        // 0 when never written
        public float Number(int id) => id < numbers.Length ? numbers[id] : 0f;
        public bool Has(int id) => id < written.Length && written[id];
        public void SetNumber(int id, float value)
        {
            Fit(ref numbers, id);
            Fit(ref written, id);
            numbers[id] = value;
            written[id] = true;
        }

        public Vector3 Vector(int id) => id < vectors.Length ? vectors[id] : default;
        public void SetVector(int id, Vector3 value)
        {
            Fit(ref vectors, id);
            vectors[id] = value;
        }

        public object Object(int id) => id < objects.Length ? objects[id] : null;
        public void SetObject(int id, object value)
        {
            Fit(ref objects, id);
            objects[id] = value;
        }

        // By name: for rare uses like saves and initial values
        public bool Has(string key) => Has(VariableIds.Of(key));
        public float Number(string key) => Number(VariableIds.Of(key));
        public void SetNumber(string key, float value) => SetNumber(VariableIds.Of(key), value);
        public void SetObject(string key, object value) => SetObject(VariableIds.Of(key), value);

        // Number variables that have been written
        public IEnumerable<KeyValuePair<string, float>> Numbers()
        {
            for (int i = 0; i < written.Length; i++)
                if (written[i]) yield return new KeyValuePair<string, float>(VariableIds.Name(i), numbers[i]);
        }

        public void Clear()
        {
            Array.Clear(numbers, 0, numbers.Length);
            Array.Clear(written, 0, written.Length);
            Array.Clear(vectors, 0, vectors.Length);
            Array.Clear(objects, 0, objects.Length);
        }

        static void Fit<T>(ref T[] array, int id)
        {
            if (id >= array.Length) Array.Resize(ref array, Mathf.Max(id + 1, array.Length * 2, 8));
        }
    }

    // Self variables, and local variables shared by several runners (AI). Gone when the owner is gone
    public static class SelfVariables
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, VariableStore> stores = new();

        public static VariableStore Of(object owner) => stores.GetOrCreateValue(owner);
    }

    // Kept in the save. Your save system loads it with SetNumber and saves Numbers() when Changed fires
    public static class PersistentVariables
    {
        public static readonly VariableStore Values = new();
        // On every value change. The game's save listens
        public static event Action Changed;

        public static void NotifyChanged() => Changed?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Values.Clear();
    }

    // Back to 0 when a new run starts
    public static class GlobalVariables
    {
        public static readonly VariableStore Values = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Values.Clear();
    }

    // Shared between copies, so keep no runtime state here
    [Serializable, Display("value")]
    public abstract class ValueSource : DataSource
    {
        public abstract bool TryRead(ScrapContext ctx, out float value);

        // Fixed value readable without running. Description text uses this value as is
        public virtual bool IsConstant => false;

        protected virtual Action Add(ScrapContext ctx, float amount) => null;

        // Revertible values (stat mods) return a revert Action; immediate values return null
        public virtual Action Apply(ScrapContext ctx, StatOp op, float value) =>
            TryRead(ctx, out float current) ? Add(ctx, new StatMod(op, value).Apply(current, current) - current) : null;

        public static float ReadOr(ScrapContext ctx, ValueSource v, float fallback) =>
            v != null && v.TryRead(ctx, out float x) ? x : fallback;

        public static bool TryRead(ScrapContext ctx, ValueSource a, ValueSource b, out float x, out float y)
        {
            x = y = 0f;
            return a != null && b != null && a.TryRead(ctx, out x) && b.TryRead(ctx, out y);
        }
    }

    // Built-in variable: time. Frame time follows the mob's action speed (slow, etc.). Game time stops when the game pauses
    [Serializable, Category("Variables"), Display(nameof(kind))]
    public class TimeValue : ValueSource
    {
        public enum Kind { [InspectorName("Time.time")] Game, [InspectorName("Time.deltaTime")] Frame }
        [SerializeField] Kind kind;

        public TimeValue() { }
        public TimeValue(Kind kind) => this.kind = kind;

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = kind switch
            {
                Kind.Frame => Motion.Dt(ctx),
                _ => Time.time,
            };
            return true;
        }
    }

    // Reads 0 the first time. Not saved
    [Serializable, Category("Variables"), Display(nameof(key))]
    public class VariableValue : ValueSource
    {
        [SerializeField, VariableKey(nameof(scope))] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public VariableValue() { }
        public VariableValue(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(key)) return false;
            value = ctx.Variables(scope).Number(id.Of(key));
            return true;
        }

        protected override Action Add(ScrapContext ctx, float amount)
        {
            if (string.IsNullOrEmpty(key)) return null;
            VariableStore variables = ctx.Variables(scope);
            int at = id.Of(key);
            variables.SetNumber(at, variables.Number(at) + amount);
            if (scope == VariableScope.Persistent) PersistentVariables.NotifyChanged();
            return null;
        }
    }

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class IntVariable : ValueSource
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Int)] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public IntVariable() { }
        public IntVariable(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(key)) return false;
            value = ctx.Variables(scope).Number(id.Of(key));
            return true;
        }

        protected override Action Add(ScrapContext ctx, float amount)
        {
            if (string.IsNullOrEmpty(key)) return null;
            VariableStore variables = ctx.Variables(scope);
            int at = id.Of(key);
            variables.SetNumber(at, Mathf.Round(variables.Number(at) + amount));
            return null;
        }
    }

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class BoolVariable : ScrapCondition
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Bool)] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public BoolVariable() { }
        public BoolVariable(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool Check(ScrapContext ctx) =>
            !string.IsNullOrEmpty(key) && ctx.Variables(scope).Number(id.Of(key)) != 0f;

        public override Action Write(ScrapContext ctx, bool value)
        {
            if (!string.IsNullOrEmpty(key)) ctx.Variables(scope).SetNumber(id.Of(key), value ? 1f : 0f);
            return null;
        }
    }

}
