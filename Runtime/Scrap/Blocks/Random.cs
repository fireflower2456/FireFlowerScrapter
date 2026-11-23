using System;
using System.Collections.Generic;
using ScrapWeaver;
using UnityEngine;

namespace FireFlower
{
    // Picks one branch with probability proportional to its weight and runs it
    [Serializable, Category("Control"), Display("pick one at random")]
    public class RandomBlock : ActionBlock, IScrapStep
    {
        [SerializeReference, Branches, Display("branch")] BranchBlock[] branches = { new(), new() };

        public override void OnEnabled(ScrapContext ctx) { }

        public Action Run(ScrapContext ctx, Action resume)
        {
            float total = 0f;
            foreach (BranchBlock branch in branches)
                if (branch != null) total += branch.Weight(ctx);

            float roll = UnityEngine.Random.value * total;
            foreach (BranchBlock branch in branches)
            {
                if (branch == null) continue;
                roll -= branch.Weight(ctx);
                if (roll > 0f) continue;
                branch.Play(ctx, resume);
                return () => branch.OnDisabled(ctx);
            }
            resume();
            return null;
        }
    }

    [Serializable, Display("◆ weight", nameof(weight))]
    public class BranchBlock : ControlBlock
    {
        [SerializeReference, SubclassSelector, Tooltip("1 if empty")] ValueSource weight;

        internal float Weight(ScrapContext ctx) => Mathf.Max(0f, ValueSource.ReadOr(ctx, weight, 1f));

        public override void OnEnabled(ScrapContext ctx) { }

        internal void Play(ScrapContext ctx, Action done) => Spawn(ctx, done);
    }
}
