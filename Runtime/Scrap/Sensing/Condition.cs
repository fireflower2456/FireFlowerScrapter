using ScrapWeaver;
using System;
using UnityEngine;

namespace FireFlower
{
    // Shared between copies, so keep no runtime state. A condition is a bool value: it sits in the value tree, and writable ones (bool variables) can be set with the = block
    [Serializable, Display("condition")]
    public abstract class ScrapCondition : DataSource
    {
        public abstract bool Check(ScrapContext ctx);
        public virtual Action Write(ScrapContext ctx, bool value) => null;
    }

    [Serializable, Category("Operators")]
    public abstract class OperatorCondition : ScrapCondition { }

    [Serializable, Display(nameof(a), nameof(op), nameof(b))]
    public class CompareCondition : OperatorCondition
    {
        enum Op
        {
            [InspectorName(">")] Greater,
            [InspectorName("<")] Less,
            [InspectorName("≥")] GreaterEqual,
            [InspectorName("≤")] LessEqual,
            [InspectorName("=")] Equal,
        }
        [SerializeReference, SubclassSelector] ValueSource a;
        [SerializeField] Op op;
        [SerializeReference, SubclassSelector] ValueSource b;

        public override bool Check(ScrapContext ctx) =>
            ValueSource.TryRead(ctx, a, b, out float x, out float y) && op switch
            {
                Op.Greater => x > y,
                Op.Less => x < y,
                Op.GreaterEqual => x >= y,
                Op.LessEqual => x <= y,
                _ => Mathf.Approximately(x, y),
            };
    }

    [Serializable, Display(nameof(a), nameof(op), nameof(b))]
    public class LogicCondition : OperatorCondition
    {
        public enum Op { [InspectorName("&&")] And, [InspectorName("||")] Or }
        [SerializeReference, SubclassSelector] protected ScrapCondition a;
        [SerializeField] Op op;
        [SerializeReference, SubclassSelector] protected ScrapCondition b;

        public LogicCondition() { }
        public LogicCondition(ScrapCondition a, Op op, ScrapCondition b)
        {
            this.a = a;
            this.op = op;
            this.b = b;
        }

        public override bool Check(ScrapContext ctx) => op == Op.Or
            ? (a != null && a.Check(ctx)) || (b != null && b.Check(ctx))
            : a != null && b != null && a.Check(ctx) && b.Check(ctx);
    }

    [Serializable, Display(nameof(value))]
    public class BoolCondition : OperatorCondition
    {
        enum Value { [InspectorName("true")] True, [InspectorName("false")] False }
        [SerializeField] Value value;

        public BoolCondition() { }
        public BoolCondition(bool value) => this.value = value ? Value.True : Value.False;

        public override bool Check(ScrapContext ctx) => value == Value.True;
    }

    [Serializable, Display("not", nameof(condition))]
    public class NotCondition : OperatorCondition
    {
        [SerializeReference, SubclassSelector] ScrapCondition condition;
        public override bool Check(ScrapContext ctx) => condition != null && !condition.Check(ctx);
    }

}
