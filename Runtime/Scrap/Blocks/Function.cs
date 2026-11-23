using System;
using System.Collections.Generic;
using UnityEngine;
using ScrapWeaver;

namespace FireFlower
{
    // One piece of a My Block's shape. Inputs are looked up by id, so renaming doesn't break links
    [Serializable]
    public class FunctionPiece
    {
        public enum Kind { Label, Number, Bool }
        public Kind kind;
        public string text;
        public string id;
    }

    // Only registers when enabled. The body runs as a copy on each call
    [Serializable, Category("My Blocks"), Display("define")]
    public class FunctionBlock : DefinitionBlock
    {
        [HideInInspector] public string id;
        [HideInInspector] public List<FunctionPiece> pieces = new();

        public override void OnEnabled(ScrapContext ctx) => ctx.Runner.DefineFunction(id, this);
        internal void Invoke(ScrapContext ctx, Action done) => Spawn(ctx, done);
    }

    // Waits until the body finishes. Arguments follow the definition's number/boolean input order
    [Serializable, Category("My Blocks"), Display("call")]
    public class CallBlock : ActionBlock, IScrapStep
    {
        [HideInInspector] public string function;
        [SerializeReference] public ValueSource[] numbers = Array.Empty<ValueSource>();
        [SerializeReference] public ScrapCondition[] conditions = Array.Empty<ScrapCondition>();

        public override void OnEnabled(ScrapContext ctx) { }

        public Action Run(ScrapContext ctx, Action resume)
        {
            if (ctx.Runner.Function(function) is not FunctionBlock definition)
            {
                resume();
                return null;
            }
            var args = new Dictionary<string, float>();
            int number = 0, condition = 0;
            foreach (FunctionPiece piece in definition.pieces)
            {
                switch (piece.kind)
                {
                    case FunctionPiece.Kind.Number:
                        args[piece.id] = ValueSource.ReadOr(ctx, number < numbers.Length ? numbers[number] : null, 0f);
                        number++;
                        break;
                    case FunctionPiece.Kind.Bool:
                        ScrapCondition c = condition < conditions.Length ? conditions[condition] : null;
                        args[piece.id] = c != null && c.Check(ctx) ? 1f : 0f;
                        condition++;
                        break;
                }
            }
            // If the calling stack is disabled, the body still runs to the end; it just doesn't continue to the next block
            bool cancelled = false;
            definition.Invoke(ctx.WithArgs(args), () =>
            {
                if (!cancelled) resume();
            });
            return () => cancelled = true;
        }
    }

    [Serializable, Category("My Blocks"), Display("input")]
    public class ArgumentValue : ValueSource
    {
        [HideInInspector] public string id;

        public override bool TryRead(ScrapContext ctx, out float value)
        {
            value = 0f;
            return ctx.args != null && ctx.args.TryGetValue(id, out value);
        }
    }

    [Serializable, Category("My Blocks"), Display("input")]
    public class ArgumentCondition : ScrapCondition
    {
        [HideInInspector] public string id;

        public override bool Check(ScrapContext ctx) => ctx.args != null && ctx.args.TryGetValue(id, out float value) && value != 0f;
    }
}
