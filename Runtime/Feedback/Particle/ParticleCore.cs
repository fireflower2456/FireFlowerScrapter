using System;
using System.Collections.Generic;
using UnityEngine;
using FireFlower;

namespace FireFlower
{
    public class ParticleCore : MonoBehaviour, IFeedbackHandle
    {
        ParticleBase[] parts;
        int pending;
        bool stopping;
        Action<float> onArrive;
        float arrived;

        // Amount set by the caller. Parts use it as they see fit (drop count, etc.)
        public float Amount { get; private set; } = 1f;

        void Awake()
        {
            parts = GetComponentsInChildren<ParticleBase>(true);
            foreach (ParticleBase part in parts) part.Bind(this);
        }

        void OnEnable()
        {
            stopping = false;
            pending = 0;
        }

        public void Init(GameObject target, float amount = 1f)
        {
            Amount = amount;
            stopping = false;
            pending = 0;
            onArrive = null;
            arrived = 0f;
            foreach (ParticleBase part in parts) part.Init(target);
            foreach (ParticleBase part in parts) part.Play();
        }

        public void Stop()
        {
            if (stopping) return;
            stopping = true;

            pending = parts.Length;
            foreach (ParticleBase part in parts)
                if (part.Finish()) pending--;

            if (pending <= 0) Release();
        }

        public void Kill()
        {
            if (this == null) return;
            stopping = true;
            Release();
        }

        public void Done()
        {
            if (!stopping) return;
            if (--pending > 0) return;
            Release();
        }

        public void WhenArrived(Action<float> callback)
        {
            onArrive = callback;
            arrived = 0f;
        }

        // A part reports the share that reached the target
        public void Arrive(float share)
        {
            if (onArrive == null) return;
            share = Mathf.Min(share, 1f - arrived);
            if (share <= 0f) return;
            arrived += share;
            onArrive(share);
        }

        // Even if it ends before reaching, the remaining share is passed on so effects tied to the visual aren't lost
        void Release()
        {
            Action<float> callback = onArrive;
            onArrive = null;
            if (callback != null && arrived < 1f) callback(1f - arrived);
            ObjectPoolExtention.Release(gameObject);
        }
    }
}
