using System;
using ScrapWeaver;
using UnityEngine;

namespace FireFlower
{
    // Motion blocks that move any Transform. Direction is a Unity angle (0 = right, 90 = up); the Transform's right (+X) faces the direction.
    // Moves via the physics body (Rigidbody2D) if there is one

    static class Motion
    {
        public static float Dt(ScrapContext ctx) => Time.deltaTime;

        public static Rigidbody2D Body(Transform t) => t.TryGetComponent(out Rigidbody2D body) ? body : null;

        // Physics bodies move on the next physics tick; otherwise moves immediately so reading it back right away is correct
        public static void Place(Transform t, Vector2 at)
        {
            Rigidbody2D body = Body(t);
            if (body != null && body.simulated && body.bodyType == RigidbodyType2D.Dynamic)
            {
                body.MovePosition(at);
                return;
            }
            if (body != null) body.position = at;
            t.position = new Vector3(at.x, at.y, t.position.z);
        }

        public static float Ease(MoveCurve curve, float t) => curve switch
        {
            MoveCurve.EaseOut => 1f - (1f - t) * (1f - t),
            MoveCurve.EaseOutCubic => 1f - (1f - t) * (1f - t) * (1f - t),
            MoveCurve.EaseIn => t * t,
            MoveCurve.EaseInOut => t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t),
            MoveCurve.ExpoIn => t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f),
            MoveCurve.ExpoOut => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t),
            _ => t,
        };
    }

    public enum MoveCurve
    {
        [InspectorName("Linear")] Linear,
        [InspectorName("Ease Out")] EaseOut,
        [InspectorName("Ease Out Cubic")] EaseOutCubic,
        [InspectorName("Ease In")] EaseIn,
        [InspectorName("Ease In-Out")] EaseInOut,
        [InspectorName("Expo In")] ExpoIn,
        [InspectorName("Expo Out")] ExpoOut,
    }

    // Tweens any writable value (number, Vector2, Vector3) over time, like DOTween. The target is read once at the start.
    // Revertible values such as stats hold the final value until the effect stops
    [Serializable, Display("tween", nameof(value), nameof(mode), nameof(target), "over", nameof(seconds), "s", nameof(curve))]
    public class TweenBlock : ActionBlock, IScrapStep
    {
        enum Mode { [InspectorName("to")] To, [InspectorName("by")] By }
        [SerializeReference, SubclassSelector] DataSource value;
        [SerializeReference, SubclassSelector] ValueSource seconds;
        [SerializeField] MoveCurve curve = MoveCurve.EaseInOut;
        [SerializeReference, SubclassSelector] DataSource target;
        [SerializeField] Mode mode;

        public override void OnEnabled(ScrapContext ctx) => Run(ctx, null);

        public Action Run(ScrapContext ctx, Action resume)
        {
            Func<float, Action> step = Stepper(ctx);
            if (step == null)
            {
                resume?.Invoke();
                return null;
            }
            float total = ValueSource.ReadOr(ctx, seconds, 0f), elapsed = 0f;
            Action undo = null;
            Hold(ctx, () => undo?.Invoke());
            Action cancel = null;
            cancel = ctx.SubscribeTick(() =>
            {
                elapsed += Motion.Dt(ctx);
                float k = total <= 0f ? 1f : Mathf.Min(1f, elapsed / total);
                undo?.Invoke();
                undo = step(Motion.Ease(curve, k));
                if (k < 1f) return;
                cancel();
                resume?.Invoke();
            });
            return cancel;
        }

        // Function that takes progress (0–1, after the curve), writes the value and returns the revert. null if the type doesn't match or can't be read
        Func<float, Action> Stepper(ScrapContext ctx)
        {
            bool by = mode == Mode.By;
            switch (value)
            {
                case ValueSource number when target is ValueSource to && number.TryRead(ctx, out float a) && to.TryRead(ctx, out float b):
                    if (by) b += a;
                    // Angles turn the short way (350° → 10° is only 20°)
                    else if (number is MemberValue { IsAngle: true }) b = a + Mathf.DeltaAngle(a, b);
                    return k => number.TryRead(ctx, out float now) ? number.Apply(ctx, StatOp.AddPure, Mathf.LerpUnclamped(a, b, k) - now) : null;
                case Vector2Source v when target is Vector2Source to && v.TryRead(ctx, out Vector2 a) && to.TryRead(ctx, out Vector2 b):
                    if (by) b += a;
                    return k => v.Write(ctx, Vector2.LerpUnclamped(a, b, k));
                case Vector3Source v when target is Vector3Source to && v.TryRead(ctx, out Vector3 a) && to.TryRead(ctx, out Vector3 b):
                    if (by) b += a;
                    return k => v.Write(ctx, Vector3.LerpUnclamped(a, b, k));
            }
            return null;
        }
    }

    // Speed is the whole Animator's speed (empty = 1). Negative plays backwards from the end
    [Serializable, Category("Looks"), Display("play animation", nameof(clip), "on", nameof(transform), "at speed", nameof(speed))]
    public class PlayAnimationBlock : ActionBlock
    {
        [SerializeReference, SubclassSelector] TransformSource transform;
        [SerializeField] string clip;
        [SerializeReference, SubclassSelector] ValueSource speed;

        // The last block that played on each Animator. Another play during a rewind ends the rewind
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Animator, object> latest = new();

        public override void OnEnabled(ScrapContext ctx)
        {
            Transform t = TransformSource.ReadOrNull(ctx, transform);
            Animator anim = t == null ? null : t.TryGetComponent(out Animator a) ? a : t.GetComponentInChildren<Animator>();
            if (anim == null || string.IsNullOrEmpty(clip)) return;
            object me = new();
            latest.AddOrUpdate(anim, me);
            float s = speed != null && speed.TryRead(ctx, out float v) ? v : 1f;
            if (s >= 0f)
            {
                anim.speed = s;
                anim.Play(clip, 0, 0f);
                return;
            }
            // Animator ignores negative speed: freeze it and rewind the time by hand from the end
            anim.speed = 1f;
            anim.Play(clip, 0, 1f);
            anim.Update(0f);
            float length = Mathf.Max(anim.GetCurrentAnimatorStateInfo(0).length, 0.0001f), time = 1f;
            anim.speed = 0f;
            bool done = false;
            Action cancel = null;
            void End()
            {
                done = true;
                cancel?.Invoke();
            }
            // Fully rewound: stays frozen on the first frame (like forward play stops on the last). Stopped mid-rewind: speed goes back to 1
            Hold(ctx, () =>
            {
                if (done) return;
                End();
                if (anim != null) anim.speed = 1f;
            });
            cancel = ctx.SubscribeTick(() =>
            {
                if (anim == null || !latest.TryGetValue(anim, out object owner) || owner != me)
                {
                    End();
                    return;
                }
                time += Time.deltaTime * s / length;
                anim.Play(clip, 0, Mathf.Max(0f, time));
                if (time <= 0f) End();
            });
        }
    }

    // Goes back to the original color when the block stops
    [Serializable, Category("Looks"), Display("set color of", nameof(on), "to", nameof(color))]
    public class TintBlock : ActionBlock
    {
        [SerializeReference, SubclassSelector] TransformSource on;
        [SerializeField] Color color = Color.white;

        public override void OnEnabled(ScrapContext ctx)
        {
            Transform t = TransformSource.ReadOrNull(ctx, on);
            SpriteRenderer sr = t == null ? null : t.TryGetComponent(out SpriteRenderer s) ? s : t.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;
            Color original = sr.color;
            sr.color = color;
            Hold(ctx, () => { if (sr != null) sr.color = original; });
        }
    }

}
