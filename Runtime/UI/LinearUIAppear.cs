using UnityEngine;

namespace FireFlower
{
    public class LinearUIAppear : LinearUIMove
    {
        protected virtual void OnEnable()
        {
            IsMoving = true;
        }
    }
}
