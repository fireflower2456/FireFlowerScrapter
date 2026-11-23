using System;
using UnityEngine;

namespace FireFlower
{
    [Serializable] public class AfterimageFeedback : FeedbackBase
    {
        [SerializeField] AfterimageSettings settings = new();

        public override IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent)
        {
            if (target == null) return null;

            AfterimageSpawner.Begin(target, settings);
            return new Handle(target);
        }

        sealed class Handle : IFeedbackHandle
        {
            readonly GameObject target;

            public Handle(GameObject target) => this.target = target;

            public void Stop() => AfterimageSpawner.EndOn(target);
        }
    }
}
