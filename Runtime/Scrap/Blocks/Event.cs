using System;
using UnityEngine;
using ScrapWeaver;

namespace FireFlower
{
    [Serializable, Display("when", nameof(evt))]
    public class TriggerBlock : FireBlock
    {
        [SerializeReference, SubclassSelector] TriggerEvent evt;
        protected override Action Subscribe(ScrapContext ctx) => evt?.Subscribe(ctx, c => Spawn(c));
    }

    // Things enabled here may not be reverted, and nothing after a wait continues, so use it only for instant effects
    [Serializable, Display("when disabled")]
    public class OnStopBlock : FireBlock
    {
        protected override Action Subscribe(ScrapContext ctx) => () => Spawn(ctx);
    }
}
