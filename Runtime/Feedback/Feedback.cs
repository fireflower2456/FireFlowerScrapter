using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    [Serializable]
    public class Feedback
    {
        [SerializeReference, SubclassSelector]
        FeedbackBase[] parts;

        public IFeedbackHandle Play(Vector3 position, GameObject target = null, Transform parent = null, Vector2 direction = default)
        {
            if (parts == null) return null;

            List<IFeedbackHandle> handles = null;
            foreach (FeedbackBase part in parts)
            {
                IFeedbackHandle handle = part?.Play(position, target, parent, direction);
                if (handle == null) continue;

                (handles ??= new List<IFeedbackHandle>()).Add(handle);
            }
            return handles == null ? null : new GroupHandle(handles);
        }

        sealed class GroupHandle : IFeedbackHandle
        {
            readonly List<IFeedbackHandle> handles;

            public GroupHandle(List<IFeedbackHandle> handles) => this.handles = handles;

            public void Stop()
            {
                foreach (IFeedbackHandle handle in handles) handle.Stop();
            }

            public void Kill()
            {
                foreach (IFeedbackHandle handle in handles) handle.Kill();
            }

            public void WhenArrived(Action<float> onArrive)
            {
                float each = 1f / handles.Count;
                foreach (IFeedbackHandle handle in handles) handle.WhenArrived(share => onArrive(share * each));
            }
        }
    }
    [Serializable]
    public abstract class FeedbackBase
    {
        public abstract IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent);

        // Override only in kinds that use direction
        public virtual IFeedbackHandle Play(Vector3 position, GameObject target, Transform parent, Vector2 direction)
            => Play(position, target, parent);
    }

    public interface IFeedbackHandle
    {
        void Stop();

        // Clears everything left (droplets in flight, etc.) on the spot. For when no wind-down should show, e.g. teleports
        void Kill() => Stop();

        // Reports how much of the effect has reached the target (summing to 1). Effects with no notion of reaching report 1 right away
        void WhenArrived(Action<float> onArrive) => onArrive?.Invoke(1f);
    }
}
