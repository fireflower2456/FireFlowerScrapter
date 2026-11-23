using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FireFlower
{
    // Reads and writes a value through a "member.member" path from the previous value (a component). Same as typing body.linearVelocity.x in C#.
    // Reachable members follow the same rules as C# and the Inspector: public fields/properties and [SerializeField] fields. Everything else counts as private
    public static class MemberPath
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public sealed class Resolved
        {
            public Type Start;
            public MemberInfo[] Members;
            public Type End => TypeOf(Members[^1]);
            // Reads and writes the end value without reflection (built from registered fast members). Null means reflection
            internal Func<object, float> GetNumber;
            internal Action<object, float> SetNumber;
            internal Func<object, Vector3> GetVector;
            internal Action<object, Vector3> SetVector;
        }

        // Common members are read and written directly, without reflection (no boxing). Used when the path is that member alone or its .x/.y/.z.
        // Members not listed go through reflection, with the same result. Game code can add more with Register
        sealed class FastMember
        {
            public Func<object, float> GetNumber;
            public Action<object, float> SetNumber;
            public Func<object, Vector3> GetVector;
            public Action<object, Vector3> SetVector;
            public bool Flat;
        }
        static readonly Dictionary<(Type, string), FastMember> fast = new();

        public static void RegisterNumber<T>(string name, Func<T, float> get, Action<T, float> set = null) where T : class =>
            fast[(typeof(T), name)] = new FastMember { GetNumber = o => get((T)o), SetNumber = set == null ? null : (o, v) => set((T)o, v) };

        public static void RegisterVector2<T>(string name, Func<T, Vector2> get, Action<T, Vector2> set = null) where T : class =>
            fast[(typeof(T), name)] = new FastMember { Flat = true, GetVector = o => get((T)o), SetVector = set == null ? null : (o, v) => set((T)o, v) };

        public static void RegisterVector3<T>(string name, Func<T, Vector3> get, Action<T, Vector3> set = null) where T : class =>
            fast[(typeof(T), name)] = new FastMember { GetVector = o => get((T)o), SetVector = set == null ? null : (o, v) => set((T)o, v) };

        static MemberPath()
        {
            RegisterVector3<Transform>("position", t => t.position, (t, v) => t.position = v);
            RegisterVector3<Transform>("localPosition", t => t.localPosition, (t, v) => t.localPosition = v);
            RegisterVector3<Transform>("localScale", t => t.localScale, (t, v) => t.localScale = v);
            RegisterVector3<Transform>("eulerAngles", t => t.eulerAngles, (t, v) => t.eulerAngles = v);
            RegisterVector3<Transform>("localEulerAngles", t => t.localEulerAngles, (t, v) => t.localEulerAngles = v);
            RegisterVector3<Transform>("right", t => t.right, (t, v) => t.right = v);
            RegisterVector3<Transform>("up", t => t.up, (t, v) => t.up = v);
            RegisterVector2<Rigidbody2D>("linearVelocity", b => b.linearVelocity, (b, v) => b.linearVelocity = v);
            RegisterVector2<Rigidbody2D>("position", b => b.position, (b, v) => b.position = v);
            RegisterNumber<Rigidbody2D>("angularVelocity", b => b.angularVelocity, (b, v) => b.angularVelocity = v);
            RegisterNumber<Rigidbody2D>("rotation", b => b.rotation, (b, v) => b.rotation = v);
            RegisterNumber<Rigidbody2D>("gravityScale", b => b.gravityScale, (b, v) => b.gravityScale = v);
            RegisterNumber<Rigidbody2D>("linearDamping", b => b.linearDamping, (b, v) => b.linearDamping = v);
            RegisterNumber<SpriteRenderer>("flipX", r => r.flipX ? 1f : 0f, (r, v) => r.flipX = v != 0f);
            RegisterNumber<SpriteRenderer>("flipY", r => r.flipY ? 1f : 0f, (r, v) => r.flipY = v != 0f);
            RegisterNumber<Behaviour>("enabled", b => b.enabled ? 1f : 0f, (b, v) => b.enabled = v != 0f);
            RegisterNumber<Animator>("speed", a => a.speed, (a, v) => a.speed = v);
        }

        static FastMember FastFor(Type type, string name)
        {
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                if (fast.TryGetValue((t, name), out FastMember f)) return f;
            return null;
        }

        // Vector parts: x/y/z = 0/1/2, magnitude = 3, sqrMagnitude = 4. Otherwise -1
        static int VectorPart(string name) => name switch
        {
            "x" => 0,
            "y" => 1,
            "z" => 2,
            "magnitude" => 3,
            "sqrMagnitude" => 4,
            _ => -1,
        };

        static float Part(Vector3 v, int part) => part switch
        {
            0 => v.x,
            1 => v.y,
            2 => v.z,
            3 => v.magnitude,
            _ => v.sqrMagnitude,
        };

        // Builds the fast path once, when the path is resolved
        static void Compile(Resolved r)
        {
            if (r.Start.IsValueType) return;
            FastMember first = FastFor(r.Start, r.Members[0].Name);
            if (first == null) return;
            if (r.Members.Length == 1)
            {
                r.GetNumber = first.GetNumber;
                r.SetNumber = first.SetNumber;
                r.GetVector = first.GetVector;
                r.SetVector = first.SetVector;
                return;
            }
            int part = r.Members.Length == 2 && first.GetVector != null ? VectorPart(r.Members[1].Name) : -1;
            if (part < 0 || (part == 2 && first.Flat)) return;
            Func<object, Vector3> get = first.GetVector;
            Action<object, Vector3> set = first.SetVector;
            r.GetNumber = o => Part(get(o), part);
            if (set != null && part <= 2) r.SetNumber = (o, v) =>
            {
                Vector3 now = get(o);
                now[part] = v;
                set(o, now);
            };
        }

        // Per-block slot that resolves the path once. Not resolved again while the root block and path string are the same (it only follows names, so copies may share it)
        public struct Cache
        {
            DataSource root;
            string path;
            Resolved resolved;
            int part;
            // Remembers dead paths too, so they are not looked up every time
            bool tried;

            internal Resolved Get(DataSource r, string p)
            {
                if (!tried || !ReferenceEquals(r, root) || !ReferenceEquals(p, path))
                {
                    tried = true;
                    root = r;
                    path = p;
                    resolved = Resolve(StartType(r), p, out _);
                    part = p != null && p.IndexOf('.') < 0 ? VectorPart(p) : -1;
                }
                return resolved;
            }

            // The part number when the path is a single vector part, else -1. Read after Get
            internal int Part => part;
        }

        static Dictionary<string, Type> components;
        static Dictionary<string, Type> enums;
        static readonly Dictionary<(Type, string), Resolved> cache = new();

        // Short component name → type. On a name clash, the first one found
        public static IReadOnlyDictionary<string, Type> Components
        {
            get
            {
                if (components != null) return components;
                components = new Dictionary<string, Type>();
                foreach (Type t in AllTypes())
                    if (typeof(Component).IsAssignableFrom(t) && !t.IsGenericTypeDefinition && (t.IsPublic || t.IsNestedPublic))
                        components.TryAdd(t.Name, t);
                return components;
            }
        }

        // null if no such name
        public static Type ComponentType(string name) => !string.IsNullOrEmpty(name) && Components.TryGetValue(name, out Type t) ? t : null;

        // Short enum name → type. On a name clash, the first one found
        public static IReadOnlyDictionary<string, Type> Enums
        {
            get
            {
                if (enums != null) return enums;
                enums = new Dictionary<string, Type>();
                foreach (Type t in AllTypes())
                    if (t.IsEnum && (t.IsPublic || t.IsNestedPublic)) enums.TryAdd(t.Name, t);
                return enums;
            }
        }

        static IEnumerable<Type> AllTypes()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                foreach (Type t in types) yield return t;
            }
        }

        // "RigidbodyType2D.Kinematic" → that value's integer. false if invalid
        public static bool TryEnum(string text, out float value)
        {
            value = 0f;
            int dot = text?.LastIndexOf('.') ?? -1;
            if (dot <= 0 || !Enums.TryGetValue(text[..dot], out Type type) || !Enum.IsDefined(type, text[(dot + 1)..])) return false;
            value = System.Convert.ToInt32(Enum.Parse(type, text[(dot + 1)..]));
            return true;
        }

        public static Type TypeOf(MemberInfo m) => m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;

        // Members reachable as the next step from this type
        public static IEnumerable<MemberInfo> Members(Type type)
        {
            var seen = new HashSet<string>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
                foreach (MemberInfo m in t.GetMembers(Instance | BindingFlags.DeclaredOnly))
                    if (Visible(m) && seen.Add(m.Name)) yield return m;
        }

        static bool Visible(MemberInfo m) => m switch
        {
            FieldInfo f => !f.IsStatic && !f.Name.Contains('<') && (f.IsPublic || f.IsDefined(typeof(SerializeField), true)) && !f.IsDefined(typeof(ObsoleteAttribute), true),
            PropertyInfo p => p.GetMethod is { IsPublic: true, IsStatic: false } && p.GetIndexParameters().Length == 0 && !p.IsDefined(typeof(ObsoleteAttribute), true),
            _ => false,
        };

        static MemberInfo Find(Type type, string name) => Members(type).FirstOrDefault(m => m.Name == name);

        // Follows path from the start type. null if invalid; error tells where it broke
        public static Resolved Resolve(Type start, string path, out string error)
        {
            error = null;
            if (start == null)
            {
                error = "Unknown type of the previous value";
                return null;
            }
            if (string.IsNullOrEmpty(path))
            {
                error = "Path is empty";
                return null;
            }
            if (cache.TryGetValue((start, path), out Resolved hit)) return hit;
            string[] steps = path.Split('.');
            var members = new MemberInfo[steps.Length];
            Type type = start;
            for (int i = 0; i < steps.Length; i++)
            {
                MemberInfo m = Find(type, steps[i]);
                if (m == null)
                {
                    error = $"'{steps[i]}' does not exist on {type.Name} or is private";
                    return null;
                }
                members[i] = m;
                type = TypeOf(m);
            }
            var resolved = new Resolved { Start = start, Members = members };
            Compile(resolved);
            return cache[(start, path)] = resolved;
        }

        static object Get(MemberInfo m, object owner) => m is FieldInfo f ? f.GetValue(owner) : ((PropertyInfo)m).GetValue(owner);

        static void Set(MemberInfo m, object owner, object value)
        {
            if (m is FieldInfo f) f.SetValue(owner, value);
            else ((PropertyInfo)m).SetValue(owner, value);
        }

        public static bool TryGet(object root, Resolved path, out object value)
        {
            value = null;
            if (root == null || path == null || !path.Start.IsInstanceOfType(root)) return false;
            object node = root;
            foreach (MemberInfo m in path.Members)
            {
                if (node == null || node is UnityEngine.Object o && o == null) return false;
                node = Get(m, node);
            }
            value = node;
            return true;
        }

        // Edits inside a struct (Color, Vector) are written back up to the outer value
        public static bool TrySet(object root, Resolved path, object value) =>
            root != null && path != null && path.Start.IsInstanceOfType(root) && Write(root, path.Members, 0, value, out _);

        // The value reached by reading the previous value and following the path
        // The left value's type: the component for components, Vector2/Vector3 for vectors. Null if unknown
        public static Type StartType(DataSource root) => root switch
        {
            ObjectSource o => o.Type,
            Vector2Source => typeof(Vector2),
            Vector3Source => typeof(Vector3),
            _ => null,
        };

        // Reads the left value. Vectors come back as a boxed copy
        static bool TryRoot(ScrapContext ctx, DataSource root, out object value)
        {
            value = null;
            switch (root)
            {
                case ObjectSource o when o.TryReadComponent(ctx, out Component c):
                    value = c;
                    return true;
                case Vector2Source v when v.TryRead(ctx, out Vector2 x):
                    value = x;
                    return true;
                case Vector3Source v when v.TryRead(ctx, out Vector3 x):
                    value = x;
                    return true;
            }
            return false;
        }

        public static bool TryRead(ScrapContext ctx, DataSource root, string path, out object value)
        {
            value = null;
            return TryRoot(ctx, root, out object start) && TryGet(start, Resolve(StartType(root), path, out _), out value);
        }

        // Writes and returns an undo (back to the value before). False if it can't.
        // If the left value is a vector, the copy is changed and written back through the left socket (variable, position …)
        public static bool TryWrite(ScrapContext ctx, DataSource root, string path, object value, out Action undo)
        {
            undo = null;
            if (!TryRoot(ctx, root, out object start)) return false;
            Resolved resolved = Resolve(StartType(root), path, out _);
            if (!TryGet(start, resolved, out object before) || !TrySet(start, resolved, value)) return false;
            switch (root)
            {
                case ObjectSource:
                    undo = () => TrySet(start, resolved, before);
                    return true;
                case Vector2Source v:
                    Vector2 v2 = (Vector2)start;
                    v.Write(ctx, v2);
                    undo = () => { if (v.TryRead(ctx, out Vector2 now)) { object box = now; if (TrySet(box, resolved, before)) v.Write(ctx, (Vector2)box); } };
                    return true;
                case Vector3Source v:
                    Vector3 v3 = (Vector3)start;
                    v.Write(ctx, v3);
                    undo = () => { if (v.TryRead(ctx, out Vector3 now)) { object box = now; if (TrySet(box, resolved, before)) v.Write(ctx, (Vector3)box); } };
                    return true;
            }
            return false;
        }

        static bool Alive(ObjectSource o, ScrapContext ctx, Resolved r, out Component c) =>
            o.TryReadComponent(ctx, out c) && c != null && r.Start.IsInstanceOfType(c);

        // Reads a number. Without boxing when there is a fast path (a part of a vector root, a registered member), otherwise by reflection
        public static bool TryReadNumber(ScrapContext ctx, DataSource root, string path, ref Cache cache, out float value)
        {
            value = 0f;
            Resolved r = cache.Get(root, path);
            if (r == null) return false;
            int part = cache.Part;
            switch (root)
            {
                case Vector2Source v2 when part >= 0 && part != 2:
                    if (!v2.TryRead(ctx, out Vector2 a)) return false;
                    value = Part(a, part);
                    return true;
                case Vector3Source v3 when part >= 0:
                    if (!v3.TryRead(ctx, out Vector3 b)) return false;
                    value = Part(b, part);
                    return true;
                case ObjectSource o when r.GetNumber != null:
                    if (!Alive(o, ctx, r, out Component c)) return false;
                    value = r.GetNumber(c);
                    return true;
            }
            return TryRoot(ctx, root, out object start) && TryGet(start, r, out object raw) && TryNumber(raw, out value);
        }

        // Adds to a number. With revert, returns an undo (back to the value before adding)
        public static bool TryAddNumber(ScrapContext ctx, DataSource root, string path, ref Cache cache, float amount, bool revert, out Action undo)
        {
            undo = null;
            if (!TryReadNumber(ctx, root, path, ref cache, out float before)) return false;
            Resolved r = cache.Get(root, path);
            int part = cache.Part;
            float after = before + amount;
            switch (root)
            {
                case Vector2Source v2 when part is 0 or 1 && v2.TryRead(ctx, out Vector2 a):
                    a[part] = after;
                    v2.Write(ctx, a);
                    if (revert) undo = UndoPart(ctx, v2, part, before);
                    return true;
                case Vector3Source v3 when part is 0 or 1 or 2 && v3.TryRead(ctx, out Vector3 b):
                    b[part] = after;
                    v3.Write(ctx, b);
                    if (revert) undo = UndoPart(ctx, v3, part, before);
                    return true;
                case ObjectSource o when r.SetNumber != null && Alive(o, ctx, r, out Component c):
                    r.SetNumber(c, after);
                    if (revert) undo = Undo(c, r.SetNumber, before);
                    return true;
            }
            if (!TryWrite(ctx, root, path, after, out Action reflected)) return false;
            if (revert) undo = reflected;
            return true;
        }

        // Undos are made only when used. The lambdas live here so calls without revert never allocate a closure
        static Action Undo<T>(Component c, Action<object, T> set, T before) => () => { if (c != null) set(c, before); };
        static Action UndoPart(ScrapContext ctx, Vector2Source v, int part, float before) =>
            () => { if (v.TryRead(ctx, out Vector2 now)) { now[part] = before; v.Write(ctx, now); } };
        static Action UndoPart(ScrapContext ctx, Vector3Source v, int part, float before) =>
            () => { if (v.TryRead(ctx, out Vector3 now)) { now[part] = before; v.Write(ctx, now); } };

        // Reads a vector (a path ending in Vector2 or Vector3). A Vector2 has z 0
        public static bool TryReadVector(ScrapContext ctx, DataSource root, string path, ref Cache cache, out Vector3 value)
        {
            value = default;
            Resolved r = cache.Get(root, path);
            if (r == null) return false;
            if (root is ObjectSource o && r.GetVector != null)
            {
                if (!Alive(o, ctx, r, out Component c)) return false;
                value = r.GetVector(c);
                return true;
            }
            if (!TryRoot(ctx, root, out object start) || !TryGet(start, r, out object raw)) return false;
            if (raw is Vector3 deep) value = deep;
            else if (raw is Vector2 flat) value = flat;
            else return false;
            return true;
        }

        // Writes a vector. Writing a Vector2 to a Vector3 member keeps its z. With revert, returns an undo
        public static bool TryWriteVector(ScrapContext ctx, DataSource root, string path, ref Cache cache, Vector3 value, bool flat, bool revert, out Action undo)
        {
            undo = null;
            Resolved r = cache.Get(root, path);
            if (r == null) return false;
            if (root is ObjectSource o && r.SetVector != null)
            {
                if (!Alive(o, ctx, r, out Component c)) return false;
                Vector3 before = r.GetVector(c);
                r.SetVector(c, flat ? new Vector3(value.x, value.y, before.z) : value);
                if (revert) undo = Undo(c, r.SetVector, before);
                return true;
            }
            object boxed = flat ? (Vector2)value : value;
            if (!TryWrite(ctx, root, path, boxed, out Action reflected)) return false;
            if (revert) undo = reflected;
            return true;
        }

        static bool Write(object owner, MemberInfo[] members, int i, object value, out object written)
        {
            written = owner;
            if (owner == null || owner is UnityEngine.Object o && o == null) return false;
            MemberInfo m = members[i];
            if (i == members.Length - 1)
            {
                if (m is PropertyInfo { CanWrite: false }) return false;
                Set(m, owner, Convert(value, TypeOf(m), Get(m, owner)));
                return true;
            }
            object child = Get(m, owner);
            if (!Write(child, members, i + 1, value, out object changed)) return false;
            if (TypeOf(m).IsValueType)
            {
                if (m is PropertyInfo { CanWrite: false }) return false;
                Set(m, owner, changed);
            }
            return true;
        }

        public static bool IsNumber(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(double) || t == typeof(long) || t.IsEnum;

        public static bool TryNumber(object value, out float number)
        {
            number = value switch
            {
                float f => f,
                int i => i,
                bool b => b ? 1f : 0f,
                double d => (float)d,
                long l => l,
                Enum e => System.Convert.ToInt32(e),
                _ => float.NaN,
            };
            return !float.IsNaN(number);
        }

        // Writing a Vector2 into a Vector3 socket keeps the original z
        static object Convert(object value, Type to, object old)
        {
            if (value == null || to.IsInstanceOfType(value)) return value;
            if (value is Vector2 flat && to == typeof(Vector3)) return new Vector3(flat.x, flat.y, old is Vector3 o ? o.z : 0f);
            if (value is Vector3 deep && to == typeof(Vector2)) return (Vector2)deep;
            if (value is float f)
            {
                if (to == typeof(int)) return Mathf.RoundToInt(f);
                if (to == typeof(bool)) return f != 0f;
                if (to == typeof(double)) return (double)f;
                if (to == typeof(long)) return (long)Mathf.Round(f);
                if (to.IsEnum) return Enum.ToObject(to, Mathf.RoundToInt(f));
            }
            return value;
        }
    }

    // A member of the previous value (C#'s dot). The version matching the socket's type goes in (number, Vector2, Vector3). Writable, so it plugs into change, set and tween
    [Serializable, ScrapWeaver.Category("Action"), ScrapWeaver.Family("Dot", true), ScrapWeaver.Display(nameof(root), ".", nameof(path))]
    public class MemberValue : ValueSource
    {
        [SerializeReference, SubclassSelector] DataSource root;
        [SerializeField, ScrapWeaver.MemberPath(nameof(root))] string path;
        [SerializeField, Tooltip("On: when the block that changed this value turns off, restore the value from before the change")] bool revert;

        public MemberValue() { }
        public MemberValue(DataSource root, string path)
        {
            this.root = root;
            this.path = path;
        }

        MemberPath.Cache cache;

        public override bool TryRead(ScrapContext ctx, out float value) => MemberPath.TryReadNumber(ctx, root, path, ref cache, out value);

        // Type of the value at the end of the path. Null if the path is broken (the editor uses it to narrow enum pickers)
        public Type EndType => MemberPath.Resolve(MemberPath.StartType(root), path, out _)?.End;

        // An angle value (eulerAngles, rotation, angle). Tweens turn the short way
        public bool IsAngle => !string.IsNullOrEmpty(path) && (path.IndexOf("euler", StringComparison.OrdinalIgnoreCase) >= 0
            || path.EndsWith("rotation", StringComparison.OrdinalIgnoreCase) || path.EndsWith("angle", StringComparison.OrdinalIgnoreCase));

        protected override Action Add(ScrapContext ctx, float amount) =>
            MemberPath.TryAddNumber(ctx, root, path, ref cache, amount, revert, out Action undo) ? undo : null;
    }

    [Serializable, ScrapWeaver.Category("Action"), ScrapWeaver.Family("Dot"), ScrapWeaver.Display(nameof(root), ".", nameof(path))]
    public class MemberVector2Value : Vector2Source
    {
        [SerializeReference, SubclassSelector] DataSource root;
        [SerializeField, ScrapWeaver.MemberPath(nameof(root))] string path;
        [SerializeField, Tooltip("On: when the block that changed this value turns off, restore the value from before the change")] bool revert;

        public MemberVector2Value() { }
        public MemberVector2Value(DataSource root, string path)
        {
            this.root = root;
            this.path = path;
        }

        MemberPath.Cache cache;

        public override bool TryRead(ScrapContext ctx, out Vector2 value)
        {
            bool ok = MemberPath.TryReadVector(ctx, root, path, ref cache, out Vector3 v);
            value = v;
            return ok;
        }

        public override Action Write(ScrapContext ctx, Vector2 value) =>
            MemberPath.TryWriteVector(ctx, root, path, ref cache, value, true, revert, out Action undo) ? undo : null;
    }

    [Serializable, ScrapWeaver.Category("Action"), ScrapWeaver.Family("Dot"), ScrapWeaver.Display(nameof(root), ".", nameof(path))]
    public class MemberVector3Value : Vector3Source
    {
        [SerializeReference, SubclassSelector] DataSource root;
        [SerializeField, ScrapWeaver.MemberPath(nameof(root))] string path;
        [SerializeField, Tooltip("On: when the block that changed this value turns off, restore the value from before the change")] bool revert;

        public MemberVector3Value() { }
        public MemberVector3Value(DataSource root, string path)
        {
            this.root = root;
            this.path = path;
        }

        MemberPath.Cache cache;

        public override bool TryRead(ScrapContext ctx, out Vector3 value) => MemberPath.TryReadVector(ctx, root, path, ref cache, out value);

        public override Action Write(ScrapContext ctx, Vector3 value) =>
            MemberPath.TryWriteVector(ctx, root, path, ref cache, value, false, revert, out Action undo) ? undo : null;
    }

    // A single enum value ("RigidbodyType2D.Kinematic"). Plugs into set for a dot path's enum socket
    [Serializable, ScrapWeaver.Category("Operators"), ScrapWeaver.Display(nameof(value))]
    public class EnumValue : ValueSource
    {
        [SerializeField, ScrapWeaver.EnumName] string value;

        public EnumValue() { }
        public EnumValue(string value) => this.value = value;

        public override bool IsConstant => true;
        public override bool TryRead(ScrapContext ctx, out float result) => MemberPath.TryEnum(value, out result);
    }
}
