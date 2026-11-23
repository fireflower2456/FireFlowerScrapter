using System;
using UnityEngine;

namespace FireFlower
{
    public abstract class MobBase : ObjectBase, IHitAble, IAttacker, IBehavior, IActionSource, IMainSprite, IScrapSignalListener
    {
        public SpriteRenderer Sr;
        SpriteRenderer IMainSprite.MainSprite => Sr;
        public HitBody Body { get; private set; } = new HitBody();
        //Atker
        public Action AttackPerformed { get; set; }
        public Action<IHitAble> OnAttackHit { get; set; }
        public Action<AttackInfo> OnAttacking { get; set; }
        public Action<KillInfo> OnKill { get; set; }
        public virtual Vector2 AimPoint => transform.position;

        // A mob that finished booting. Game-side attachments like the overhead UI listen
        public static event Action<MobBase> Booted;

        // Event sources: base damage is attack power, the action table is the root's ActionBinder
        ActionBinder actions;
        IAttacker IActionSource.Attacker => this;
        float IActionSource.Power => Stat.TryRead(MobStats.AtkPower, out float atk) ? atk : 0f;
        public void Raise(string key, ActionContext ctx) { if (actions != null) actions.Run(key, ctx); }
        FeedbackBinder feedback;

        // A signal with this key sent by a mob also counts as its attack starting (AttackPerformed)
        public const string AttackStartSignal = "Attack Start";

        // Signals this mob sends also run the feedback/action of the same name. Position is the receiver (self if none)
        public void Signaled(ScrapSignal signal)
        {
            if (signal.key == AttackStartSignal) AttackPerformed?.Invoke();
            Component at = signal.target != null ? signal.target : this;
            if (feedback != null) feedback.Play(signal.key, at.transform.position, at.gameObject);
            Raise(signal.key, new ActionContext(this, at.transform.position, at.gameObject));
        }
        public abstract EntityController Controller { get; set; }
        // Applies incapacitation (IsKnocked: can't move, jump or dash). Returns a token to pass to Unknock when releasing. null for mobs that can't be incapacitated
        public object Knock() => Stat.Has(MobStats.Flag.IsKnocked) ? Stat.Flag(MobStats.Flag.IsKnocked).Add() : null;

        public void Unknock(object token)
        {
            if (token != null && Stat.Has(MobStats.Flag.IsKnocked)) Stat.Flag(MobStats.Flag.IsKnocked).Remove(token);
        }

        // Incapacitated for seconds. The mob counts it so it releases even if whoever applied it disappears first
        public void KnockFor(float seconds)
        {
            if (Knock() is object token) StartCoroutine(UnknockAfter(token, seconds));
        }

        System.Collections.IEnumerator UnknockAfter(object token, float seconds)
        {
            yield return FixedWait.Get(seconds);
            Unknock(token);
        }

        protected override void OnBoot()
        {
            base.OnBoot();
            actions = GetComponent<ActionBinder>();
            feedback = GetComponent<FeedbackBinder>();
            Body.Init(Stat);
            Body.Hp = Body.MaxHp;   // Full hp by default. Anything that carries hp over overwrites after this
            Body.OnHit += Flash;
            Body.OnDie += Died;
            Sr = Sr != null ? Sr : GetComponent<SpriteRenderer>();
            Stat.RequireFlag(MobStats.Flag.Hidden).OnActiveChanged += SetHidden;
            Booted?.Invoke(this);
        }

        // Hidden: not drawn, physics (collision, hits, pulls) and overhead UI off. Restores only what was on before hiding
        readonly System.Collections.Generic.List<Renderer> hiddenRenderers = new();
        bool simulatedBeforeHide;

        void SetHidden(bool hidden)
        {
            var body = GetComponent<Rigidbody2D>();
            if (hidden)
            {
                hiddenRenderers.Clear();
                foreach (Renderer r in GetComponentsInChildren<Renderer>())
                    if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
                if (body != null) { simulatedBeforeHide = body.simulated; body.simulated = false; }
            }
            else
            {
                foreach (Renderer r in hiddenRenderers)
                    if (r != null) r.enabled = true;
                hiddenRenderers.Clear();
                if (body != null) body.simulated = simulatedBeforeHide;
            }
        }

        // Notify the killer first (so item effects can change the currency), then OnDied drops the currency
        void Died()
        {
            var info = new KillInfo(this, RollCoins());
            Body.LastHit?.Instigator?.OnKill?.Invoke(info);
            OnDied(info);
        }

        protected virtual int RollCoins() => 0;
        protected virtual void OnDied(KillInfo info) { }

        void Flash(HitInfo info)
        {
            if (info.IsDot) return;
            VFXManager.Instance.FlashWhite(Sr);
        }
    }
}
