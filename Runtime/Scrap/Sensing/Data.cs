using ScrapWeaver;
using System;
using UnityEngine;

namespace FireFlower
{
    // Root of all values (number, Vector2, Vector3, Component). Drawn with the value shape; doesn't fit sockets of another kind.
    // Sockets of this type (set value, etc.) accept any kind. Shared between copies, so keep no runtime state
    [Serializable, Display("value")]
    public abstract class DataSource { }

    [Serializable, Display("Vector2")]
    public abstract class Vector2Source : DataSource
    {
        public abstract bool TryRead(ScrapContext ctx, out Vector2 value);

        // Only writable values (position, velocity, variables) are overwritten. Returns a revert Action if revertible
        public virtual Action Write(ScrapContext ctx, Vector2 value) => null;

        public static bool TryRead(ScrapContext ctx, Vector2Source a, Vector2Source b, out Vector2 x, out Vector2 y)
        {
            x = y = default;
            return a != null && b != null && a.TryRead(ctx, out x) && b.TryRead(ctx, out y);
        }
    }

    [Serializable, Display("Vector3")]
    public abstract class Vector3Source : DataSource
    {
        public abstract bool TryRead(ScrapContext ctx, out Vector3 value);

        public virtual Action Write(ScrapContext ctx, Vector3 value) => null;
    }

    // Component value. Type says which component — it decides the members offered after the dot (like a C# type)
    [Serializable, Display("Component")]
    public abstract class ObjectSource : DataSource
    {
        // null if unknown
        public abstract Type Type { get; }

        // false if missing or destroyed
        public abstract bool TryReadComponent(ScrapContext ctx, out Component value);

        public virtual Action Write(ScrapContext ctx, Component value) => null;
    }

    [Serializable, Display("Transform")]
    public abstract class TransformSource : ObjectSource
    {
        public sealed override Type Type => typeof(Transform);

        // false if missing or destroyed
        public abstract bool TryRead(ScrapContext ctx, out Transform value);

        public sealed override bool TryReadComponent(ScrapContext ctx, out Component value)
        {
            bool ok = TryRead(ctx, out Transform t);
            value = t;
            return ok;
        }

        public static Transform ReadOrNull(ScrapContext ctx, TransformSource source) =>
            source != null && source.TryRead(ctx, out Transform t) ? t : null;

        public virtual Action Write(ScrapContext ctx, Transform value) => null;
        public sealed override Action Write(ScrapContext ctx, Component value) => Write(ctx, value as Transform);
    }

    // Reading and writing non-number variables. Vectors read 0 and objects read nothing until written. Vectors are never boxed
    static class DataVariables
    {
        public static bool TryRead(ScrapContext ctx, string key, VariableScope scope, ref VariableId id, out Vector3 value)
        {
            value = default;
            if (string.IsNullOrEmpty(key)) return false;
            value = ctx.Variables(scope).Vector(id.Of(key));
            return true;
        }

        public static void Write(ScrapContext ctx, string key, VariableScope scope, ref VariableId id, Vector3 value)
        {
            if (!string.IsNullOrEmpty(key)) ctx.Variables(scope).SetVector(id.Of(key), value);
        }

        public static bool TryRead<T>(ScrapContext ctx, string key, VariableScope scope, ref VariableId id, out T value) where T : class
        {
            value = null;
            if (string.IsNullOrEmpty(key)) return false;
            value = ctx.Variables(scope).Object(id.Of(key)) as T;
            return true;
        }

        public static void Write(ScrapContext ctx, string key, VariableScope scope, ref VariableId id, object value)
        {
            if (!string.IsNullOrEmpty(key)) ctx.Variables(scope).SetObject(id.Of(key), value);
        }
    }

    // ───────────── Vector2 ─────────────

    // " x"/" y": text equal to a field name is read as that field, so it gets a leading space (trimmed in the title)
    [Serializable, Category("Operators"), Family("Vector", true), Display("Vector2", " x", nameof(x), " y", nameof(y))]
    public class Vector2Value : Vector2Source
    {
        [SerializeReference, SubclassSelector] ValueSource x;
        [SerializeReference, SubclassSelector] ValueSource y;

        public override bool TryRead(ScrapContext ctx, out Vector2 value)
        {
            bool ok = ValueSource.TryRead(ctx, x, y, out float a, out float b);
            value = new Vector2(a, b);
            return ok;
        }
    }

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class Vector2Variable : Vector2Source
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Vector2)] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public Vector2Variable() { }
        public Vector2Variable(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool TryRead(ScrapContext ctx, out Vector2 value)
        {
            bool ok = DataVariables.TryRead(ctx, key, scope, ref id, out Vector3 v);
            value = v;
            return ok;
        }

        public override Action Write(ScrapContext ctx, Vector2 value)
        {
            DataVariables.Write(ctx, key, scope, ref id, value);
            return null;
        }
    }

    // Unity angle (same as eulerAngles.z and Mathf.Atan2): 0 = right, 90 = up, 180 = left, -90 = down. Increases counter-clockwise
    public static class Direction
    {
        public static Vector2 ToVector(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        public static float Of(Vector2 v) => Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
    }

    // The direction (degrees, Unity angle) from A looking at B. Put it into (this).eulerAngles.z to face that way, or tween to turn along a curve. Fails if the points are equal
    [Serializable, Category("Operators"), Display("direction from", nameof(from), "to", nameof(to))]
    public class LookDirectionValue : ValueSource
    {
        [SerializeReference, SubclassSelector] Vector2Source from;
        [SerializeReference, SubclassSelector] Vector2Source to;

        public LookDirectionValue() { }
        public LookDirectionValue(Vector2Source from, Vector2Source to)
        {
            this.from = from;
            this.to = to;
        }

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            if (!Vector2Source.TryRead(ctx, from, to, out Vector2 a, out Vector2 b) || a == b) return false;
            value = Direction.Of(b - a);
            return true;
        }
    }

    [Serializable, Category("Operators"), Display("unit vector at", nameof(degrees), "°")]
    public class DirectionVectorValue : Vector2Source
    {
        [SerializeReference, SubclassSelector] ValueSource degrees;

        public override bool TryRead(ScrapContext ctx, out Vector2 value)
        {
            value = default;
            if (degrees == null || !degrees.TryRead(ctx, out float d)) return false;
            value = Direction.ToVector(d);
            return true;
        }
    }

    // ───────────── Vector3 ─────────────

    [Serializable, Category("Operators"), Family("Vector"), Display("(", nameof(x), ",", nameof(y), ",", nameof(z), ")")]
    public class Vector3Value : Vector3Source
    {
        [SerializeReference, SubclassSelector] ValueSource x;
        [SerializeReference, SubclassSelector] ValueSource y;
        [SerializeReference, SubclassSelector] ValueSource z;

        public override bool TryRead(ScrapContext ctx, out Vector3 value)
        {
            value = default;
            if (!ValueSource.TryRead(ctx, x, y, out float a, out float b) || z == null || !z.TryRead(ctx, out float c)) return false;
            value = new Vector3(a, b, c);
            return true;
        }
    }

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class Vector3Variable : Vector3Source
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Vector3)] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public Vector3Variable() { }
        public Vector3Variable(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool TryRead(ScrapContext ctx, out Vector3 value) => DataVariables.TryRead(ctx, key, scope, ref id, out value);

        public override Action Write(ScrapContext ctx, Vector3 value)
        {
            DataVariables.Write(ctx, key, scope, ref id, value);
            return null;
        }
    }

    // ───────────── Transform ─────────────

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class TransformVariable : TransformSource
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Transform)] string key;
        [SerializeField, HideInInspector] VariableScope scope;

        public TransformVariable() { }
        public TransformVariable(string key, VariableScope scope)
        {
            this.key = key;
            this.scope = scope;
        }

        VariableId id;

        public override bool TryRead(ScrapContext ctx, out Transform value) =>
            DataVariables.TryRead(ctx, key, scope, ref id, out value) && value != null;

        public override Action Write(ScrapContext ctx, Transform value)
        {
            DataVariables.Write(ctx, key, scope, ref id, value);
            return null;
        }
    }

    // The object running the script (C#'s `this`)
    [Serializable, Display("this")]
    public class ThisValue : TransformSource
    {
        public override bool TryRead(ScrapContext ctx, out Transform value)
        {
            value = ctx.selfTransform;
            return value != null;
        }
    }

    // The other object of the event that started this line: who touched me, who sent the signal …
    [Serializable, Display("other")]
    public class OtherValue : TransformSource
    {
        public override bool TryRead(ScrapContext ctx, out Transform value)
        {
            Component other = ctx.payload is IScrapEvent e ? e.Other : ctx.payload as Component;
            value = other != null ? other.transform : null;
            return value != null;
        }
    }

    // What an element-giving block ("for each" …) passes to its inner line. Dragged out of the pill on that block's header, like a My Block input
    [Serializable, Display("element")]
    public class ElementValue : TransformSource
    {
        [HideInInspector] public string source;

        public ElementValue() { }
        public ElementValue(string source) => this.source = source;

        public override bool TryRead(ScrapContext ctx, out Transform value)
        {
            Component e = ctx.Element(source);
            value = e != null ? e.transform : null;
            return value != null;
        }
    }

    // A block that passes an object to its inner line. The editor puts a draggable (element) pill on its header
    public interface IElementSource
    {
        string ElementId { get; }
        // Name shown on the pill
        string ElementName { get; }
    }

    // ───────────── Component ─────────────

    // GetComponent. Unreadable if the component is missing
    [Serializable, Category("Action"), Display(nameof(component), "of", nameof(from))]
    public class GetComponentValue : ObjectSource
    {
        [SerializeReference, SubclassSelector] ObjectSource from;
        [SerializeField, ComponentType] string component = nameof(Rigidbody2D);

        public GetComponentValue() { }
        public GetComponentValue(ObjectSource from, string component)
        {
            this.from = from;
            this.component = component;
        }

        // Name -> type is not looked up again while the name string is the same
        string typeOf;
        Type type;

        public override Type Type
        {
            get
            {
                if (!ReferenceEquals(typeOf, component)) type = MemberPath.ComponentType(typeOf = component);
                return type;
            }
        }

        public override bool TryReadComponent(ScrapContext ctx, out Component value)
        {
            value = null;
            Type type = Type;
            return type != null && from != null && from.TryReadComponent(ctx, out Component c) && c.TryGetComponent(type, out value);
        }
    }

    [Serializable, Category("Variables"), Display(nameof(key))]
    public class ComponentVariable : ObjectSource
    {
        [SerializeField, VariableKey(nameof(scope), VariableType.Component, ComponentField = nameof(component))] string key;
        [SerializeField, HideInInspector] VariableScope scope;
        [SerializeField, HideInInspector] string component;

        public ComponentVariable() { }
        public ComponentVariable(string key, VariableScope scope, string component)
        {
            this.key = key;
            this.scope = scope;
            this.component = component;
        }

        VariableId id;
        // Name -> type is not looked up again while the name string is the same
        string typeOf;
        Type type;

        public override Type Type
        {
            get
            {
                if (!ReferenceEquals(typeOf, component)) type = MemberPath.ComponentType(typeOf = component);
                return type;
            }
        }

        public override bool TryReadComponent(ScrapContext ctx, out Component value) =>
            DataVariables.TryRead(ctx, key, scope, ref id, out value) && value != null && Type is { } t && t.IsInstanceOfType(value);

        // Components of a different type are not accepted
        public override Action Write(ScrapContext ctx, Component value)
        {
            if (value == null || Type is { } t && t.IsInstanceOfType(value)) DataVariables.Write(ctx, key, scope, ref id, value);
            return null;
        }
    }

    // Nearest collider within the radius, excluding self
    [Serializable, Display("nearest", nameof(layers), "within", nameof(radius), "of", nameof(center))]
    public class NearestColliderValue : TransformSource
    {
        [SerializeReference, SubclassSelector] Vector2Source center;
        [SerializeReference, SubclassSelector] ValueSource radius;
        [SerializeField] LayerMask layers;

        static readonly Collider2D[] buffer = new Collider2D[32];

        public override bool TryRead(ScrapContext ctx, out Transform value)
        {
            value = null;
            if (center == null || radius == null || !center.TryRead(ctx, out Vector2 c) || !radius.TryRead(ctx, out float r)) return false;
            int count = Physics2D.OverlapCircle(c, r, new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = true }, buffer);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Transform t = buffer[i].attachedRigidbody != null ? buffer[i].attachedRigidbody.transform : buffer[i].transform;
                if (ctx.self != null && t == ctx.self.transform) continue;
                float d = ((Vector2)t.position - c).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                value = t;
            }
            return value != null;
        }
    }

    [Serializable, Display(nameof(transform), "exists")]
    public class TransformExistsCondition : ScrapCondition
    {
        [SerializeReference, SubclassSelector] TransformSource transform;
        public override bool Check(ScrapContext ctx) => TransformSource.ReadOrNull(ctx, transform) != null;
    }

    // ───────────── To number ─────────────

    // Casts a line if size is 0, otherwise a box of that size. Ignores colliders overlapping at the start. Max distance if nothing is hit
    [Serializable, Display("distance to", nameof(layers), "from", nameof(from), "toward", nameof(direction), "up to", nameof(distance))]
    public class RaycastDistanceValue : ValueSource
    {
        [SerializeReference, SubclassSelector] Vector2Source from;
        [SerializeReference, SubclassSelector] Vector2Source direction;
        [SerializeReference, SubclassSelector] ValueSource distance;
        [SerializeField] LayerMask layers;
        [SerializeField, Tooltip("Box size. 0 for a line")] Vector2 size;

        static readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            if (!Vector2Source.TryRead(ctx, from, direction, out Vector2 origin, out Vector2 dir) || distance == null || !distance.TryRead(ctx, out float max)) return false;
            value = max;
            if (dir == Vector2.zero) return true;
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = layers };
            int count = size == Vector2.zero
                ? Physics2D.Raycast(origin, dir.normalized, filter, hits, max)
                : Physics2D.BoxCast(origin, size, 0f, dir.normalized, filter, hits, max);
            for (int i = 0; i < count; i++)
                if (hits[i].fraction > 0f && hits[i].distance < value) value = hits[i].distance;
            return true;
        }
    }

    // ───────────── Set variable ─────────────

    // Vector2 arithmetic. A number on the right applies to each component (× 2 = double)
    [Serializable, Category("Operators"), Family("Arithmetic"), Display(nameof(a), nameof(op), nameof(b))]
    public class Vector2ArithmeticValue : Vector2Source
    {
        public enum Op { [InspectorName("+")] Add, [InspectorName("-")] Subtract, [InspectorName("×")] Multiply, [InspectorName("÷")] Divide }
        [SerializeReference, SubclassSelector] Vector2Source a;
        [SerializeField] Op op;
        [SerializeReference, SubclassSelector] DataSource b;

        public Vector2ArithmeticValue() { }
        public Vector2ArithmeticValue(Vector2Source a, Op op, DataSource b)
        {
            this.a = a;
            this.op = op;
            this.b = b;
        }

        public override bool TryRead(ScrapContext ctx, out Vector2 value)
        {
            value = default;
            if (a == null || !a.TryRead(ctx, out Vector2 x)) return false;
            Vector2 y;
            if (b is ValueSource n && n.TryRead(ctx, out float s)) y = new Vector2(s, s);
            else if (b is not Vector2Source v || !v.TryRead(ctx, out y)) return false;
            value = op switch
            {
                Op.Subtract => x - y,
                Op.Multiply => Vector2.Scale(x, y),
                Op.Divide => new Vector2(x.x / y.x, x.y / y.y),
                _ => x + y,
            };
            return float.IsFinite(value.x) && float.IsFinite(value.y);
        }
    }

    [Serializable, Category("Operators"), Family("Arithmetic"), Display(nameof(a), nameof(op), nameof(b))]
    public class Vector3ArithmeticValue : Vector3Source
    {
        public enum Op { [InspectorName("+")] Add, [InspectorName("-")] Subtract, [InspectorName("×")] Multiply, [InspectorName("÷")] Divide }
        [SerializeReference, SubclassSelector] Vector3Source a;
        [SerializeField] Op op;
        [SerializeReference, SubclassSelector] DataSource b;

        public Vector3ArithmeticValue() { }
        public Vector3ArithmeticValue(Vector3Source a, Op op, DataSource b)
        {
            this.a = a;
            this.op = op;
            this.b = b;
        }

        public override bool TryRead(ScrapContext ctx, out Vector3 value)
        {
            value = default;
            if (a == null || !a.TryRead(ctx, out Vector3 x)) return false;
            Vector3 y;
            if (b is ValueSource n && n.TryRead(ctx, out float s)) y = new Vector3(s, s, s);
            else if (b is not Vector3Source v || !v.TryRead(ctx, out y)) return false;
            value = op switch
            {
                Op.Subtract => x - y,
                Op.Multiply => Vector3.Scale(x, y),
                Op.Divide => new Vector3(x.x / y.x, x.y / y.y, x.z / y.z),
                _ => x + y,
            };
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }

}
