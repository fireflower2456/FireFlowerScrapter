using ScrapWeaver;
using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable, Category("Operators")]
    public abstract class OperatorValue : ValueSource { }

    [Serializable, Display(nameof(value))]
    public class NumberValue : OperatorValue
    {
        [SerializeField] float value;
        public NumberValue() { }
        public NumberValue(float value) => this.value = value;
        public override bool IsConstant => true;
        public override bool TryRead(ScrapContext ctx, out float result)
        {
            result = value;
            return true;
        }
    }

    // Integer random (inclusive) if both are integers, otherwise a float random.
    // With ctx.roll (while re-evaluating repeatedly) it returns the same number each time
    [Serializable, Display("pick random", nameof(min), "to", nameof(max))]
    public class RandomValue : OperatorValue
    {
        [SerializeReference, SubclassSelector] ValueSource min;
        [SerializeReference, SubclassSelector] ValueSource max;
        public override bool TryRead(ScrapContext ctx, out float result)
        {
            result = 0f;
            if (!TryRead(ctx, min, max, out float from, out float to)) return false;
            float t = ctx.roll == 0
                ? UnityEngine.Random.value
                : (float)new System.Random(HashCode.Combine(ctx.roll, System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this))).NextDouble();
            if (from != Mathf.Round(from) || to != Mathf.Round(to))
            {
                result = Mathf.Lerp(from, to, t);
                return true;
            }
            int lo = (int)from, count = (int)to - lo + 1;
            result = lo + Mathf.Min((int)(t * count), count - 1);
            return true;
        }
    }

    [Serializable, Display(nameof(func), nameof(value))]
    public class MathValue : OperatorValue
    {
        enum Func
        {
            [InspectorName("round")] Round,
            [InspectorName("ceiling")] Ceil,
            [InspectorName("floor")] Floor,
            [InspectorName("abs")] Abs,
            [InspectorName("sqrt")] Sqrt,
            [InspectorName("sign")] Sign,
        }
        [SerializeField] Func func;
        [SerializeReference, SubclassSelector] ValueSource value;

        public override bool TryRead(ScrapContext ctx, out float result)
        {
            result = 0f;
            if (value == null || !value.TryRead(ctx, out float x) || (func == Func.Sqrt && x < 0f)) return false;
            result = func switch
            {
                Func.Round => Mathf.Round(x),
                Func.Ceil => Mathf.Ceil(x),
                Func.Floor => Mathf.Floor(x),
                Func.Abs => Mathf.Abs(x),
                Func.Sign => x > 0f ? 1f : x < 0f ? -1f : 0f,
                _ => Mathf.Sqrt(x),
            };
            return true;
        }
    }

    // Binary arithmetic. An infinite/NaN result (division by zero, etc.) counts as unreadable. Mod is never negative (-1 mod 3 = 2)
    [Serializable, Family("Arithmetic", true), Display(nameof(a), nameof(op), nameof(b))]
    public class ArithmeticValue : OperatorValue
    {
        public enum Op
        {
            [InspectorName("+")] Add,
            [InspectorName("-")] Subtract,
            [InspectorName("×")] Multiply,
            [InspectorName("÷")] Divide,
            [InspectorName("%")] Mod,
            [InspectorName("Min")] Min,
            [InspectorName("Max")] Max,
        }
        [SerializeReference, SubclassSelector] ValueSource a;
        [SerializeField] Op op;
        [SerializeReference, SubclassSelector] ValueSource b;

        public ArithmeticValue() { }
        public ArithmeticValue(ValueSource a, Op op, ValueSource b)
        {
            this.a = a;
            this.op = op;
            this.b = b;
        }

        public override bool TryRead(ScrapContext ctx, out float result)
        {
            result = 0f;
            if (!TryRead(ctx, a, b, out float x, out float y)) return false;
            result = op switch
            {
                Op.Subtract => x - y,
                Op.Multiply => x * y,
                Op.Divide => x / y,
                Op.Mod => (x % y + y) % y,
                Op.Min => Mathf.Min(x, y),
                Op.Max => Mathf.Max(x, y),
                _ => x + y,
            };
            if (float.IsFinite(result)) return true;
            result = 0f;
            return false;
        }
    }

    // a turned toward b by at most c degrees (the short way). Inside a loop, c = [degrees per second × frame time] follows a little every frame
    [Serializable, Display(nameof(a), "turned toward", nameof(b), "by at most", nameof(c), "°")]
    public class RotateTowardsValue : OperatorValue
    {
        [SerializeReference, SubclassSelector] ValueSource a;
        [SerializeReference, SubclassSelector] ValueSource b;
        [SerializeReference, SubclassSelector] ValueSource c;

        public RotateTowardsValue() { }
        public RotateTowardsValue(ValueSource a, ValueSource b, ValueSource c)
        {
            this.a = a;
            this.b = b;
            this.c = c;
        }

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            if (!ValueSource.TryRead(ctx, a, b, out float from, out float to) || c == null || !c.TryRead(ctx, out float step)) return false;
            value = Mathf.MoveTowardsAngle(from, to, step);
            return true;
        }
    }
}
