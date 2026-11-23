using System;
using ScrapWeaver;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace FireFlower
{
    // Blocks that call the feedback module (VFX / sound / feedback tables). Saved assets know them by assembly, hence MovedFrom

    [MovedFrom(false, sourceNamespace: "FireFlower", sourceAssembly: "FireFlower.Gameplay")]
    [Serializable, Category("Looks"), Display("play particle", nameof(key), "on", nameof(target))]
    public class ParticleBlock : ActionBlock
    {
        [SerializeField] string key;
        [SerializeReference, SubclassSelector] TransformSource target;
        [SerializeField, Tooltip("On: stays attached until this block turns off. Off: plays once and forgets")] bool untilOff;
        [SerializeReference, SubclassSelector, Tooltip("Amount the particle uses (drop count, etc.). Empty means 1")] ValueSource amount;

        public override void OnEnabled(ScrapContext ctx)
        {
            if (TransformSource.ReadOrNull(ctx, target) is not Transform t) return;
            float a = amount != null && amount.TryRead(ctx, out float v) ? v : 1f;
            ParticleCore particle = VFXManager.Instance.PlayParticle(key, t.position, t, t.gameObject, default, a);
            if (untilOff && particle != null) Hold(ctx, particle.Stop);
        }
    }

    [MovedFrom(false, sourceNamespace: "FireFlower", sourceAssembly: "FireFlower.Gameplay")]
    [Serializable, Category("Sound"), Display("set BGM to", nameof(key))]
    public class BgmBlock : ActionBlock
    {
        [SerializeField] string key;

        public override void OnEnabled(ScrapContext ctx) => SoundManager.Instance.PlayBgm(key);
    }

    // Plays feedback (shake, sound, hit stop, screen flash, animation, etc.) at the target's position
    [MovedFrom(false, sourceNamespace: "FireFlower", sourceAssembly: "FireFlower.Gameplay")]
    [Serializable, Category("Looks"), Display("play feedback on", nameof(target))]
    public class FeedbackBlock : ActionBlock
    {
        [SerializeReference, SubclassSelector] TransformSource target;
        [SerializeField] Feedback feedback = new();

        public override void OnEnabled(ScrapContext ctx)
        {
            if (TransformSource.ReadOrNull(ctx, target) is not Transform t) return;
            feedback.Play(t.position, t.gameObject, t);
        }
    }
}
