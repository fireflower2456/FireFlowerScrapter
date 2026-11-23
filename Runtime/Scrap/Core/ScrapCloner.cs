using System;
using System.Collections.Generic;
using System.Reflection;

namespace FireFlower
{
    public static class ScrapCloner
    {
        const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        static readonly Dictionary<Type, FieldInfo[]> partFieldCache = new();

        // The block and every block plugged into it. With firstBranch, only one pass-through path: for multiple mouths (if / else) the first filled one,
        // for branch lists (narrowed arrays like OptionEffect[]) only the first branch — used for drawing cutscene paths
        public static IEnumerable<ScrapPart> Walk(ScrapPart source, bool firstBranch = false)
        {
            if (source == null) yield break;
            yield return source;
            foreach (var field in PartFields(source.GetType()))
            {
                switch (field.GetValue(source))
                {
                    case ScrapPart part:
                        foreach (var inner in Walk(part, firstBranch)) yield return inner;
                        if (firstBranch) yield break;
                        break;
                    case ScrapPart[] { Length: > 0 } array:
                        bool branches = array.GetType().GetElementType() != typeof(ScrapPart);
                        foreach (var element in array)
                        {
                            if (element == null) continue;
                            foreach (var inner in Walk(element, firstBranch)) yield return inner;
                            if (firstBranch && branches) break;
                        }
                        if (firstBranch) yield break;
                        break;
                }
            }
        }

        // A stack cloned once. On reuse, each block's runtime fields (not serialized by Unity) are reset to the original (template) values.
        // The template never runs, so its values are always pristine. They are captured once at clone time, so resets allocate nothing
        internal sealed class CopySet
        {
            public readonly ScrapPart[] Parts;
            // Called when the run finishes. Created once per copy
            public readonly Action Finish;
            // The cursor that runs this copy's line. Reused with the copy
            public readonly ParentBlock.Line Line;
            public Action Done;
            readonly List<ScrapPart> all = new();
            readonly List<(ScrapPart copy, FieldInfo[] fields, object[] values)> resets = new();

            public CopySet(ScrapPart[] templates, ParentBlock owner)
            {
                Line = new ParentBlock.Line(owner);
                Parts = new ScrapPart[templates?.Length ?? 0];
                for (int i = 0; i < Parts.Length; i++) Parts[i] = Track(templates[i]);
                Finish = () =>
                {
                    Action done = Done;
                    Done = null;
                    if (!Holding) (owner.idle ??= new List<CopySet>()).Add(this);
                    done?.Invoke();
                };
            }

            ScrapPart Track(ScrapPart source)
            {
                if (source == null) return null;
                var copy = source.ShallowCopy();
                all.Add(copy);
                FieldInfo[] runtime = RuntimeFields(source.GetType());
                if (runtime.Length > 0)
                {
                    var values = new object[runtime.Length];
                    for (int i = 0; i < runtime.Length; i++) values[i] = runtime[i].GetValue(source);
                    resets.Add((copy, runtime, values));
                }
                foreach (var field in PartFields(source.GetType()))
                {
                    switch (field.GetValue(source))
                    {
                        case ScrapPart part:
                            field.SetValue(copy, Track(part));
                            break;
                        case ScrapPart[] array:
                            var result = (ScrapPart[])Array.CreateInstance(array.GetType().GetElementType(), array.Length);
                            for (int i = 0; i < array.Length; i++) result[i] = Track(array[i]);
                            field.SetValue(copy, result);
                            break;
                    }
                }
                return copy;
            }

            public bool Holding
            {
                get
                {
                    for (int i = 0; i < all.Count; i++)
                        if (all[i].Holding) return true;
                    return false;
                }
            }

            public void Reset()
            {
                for (int i = 0; i < resets.Count; i++)
                {
                    var (copy, fields, values) = resets[i];
                    for (int f = 0; f < fields.Length; f++) fields[f].SetValue(copy, values[f]);
                }
            }
        }

        static readonly Dictionary<Type, FieldInfo[]> runtimeFieldCache = new();

        // Fields to reset: non-serialized instance fields (runtime state) whose declaring class does not claim, via [Reusable], to fill them itself.
        // The template's (ScrapPart / ParentBlock) hold and wait lists are only reused when "finished and holding nothing", so they are already empty. Each copy has its own idle-copy pool
        static FieldInfo[] RuntimeFields(Type type)
        {
            if (runtimeFieldCache.TryGetValue(type, out var cached)) return cached;
            var found = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(ScrapPart) && t != typeof(ParentBlock) && t != typeof(object); t = t.BaseType)
            {
                if (t.IsDefined(typeof(ScrapWeaver.ReusableAttribute), false)) continue;
                foreach (var field in t.GetFields(InstanceFields))
                {
                    bool saved = !field.IsNotSerialized
                                 && (field.IsPublic || field.IsDefined(typeof(UnityEngine.SerializeField), true) || field.IsDefined(typeof(UnityEngine.SerializeReference), true));
                    if (!saved) found.Add(field);
                }
            }
            return runtimeFieldCache[type] = found.ToArray();
        }

        static FieldInfo[] PartFields(Type type)
        {
            if (partFieldCache.TryGetValue(type, out var cached)) return cached;
            var found = new List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (var field in t.GetFields(InstanceFields))
                    if (typeof(ScrapPart).IsAssignableFrom(field.FieldType)
                        || typeof(ScrapPart[]).IsAssignableFrom(field.FieldType))
                        found.Add(field);
            return partFieldCache[type] = found.ToArray();
        }
    }
}
