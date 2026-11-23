using UnityEngine;

namespace FireFlower
{
    public class LinearGroup : MonoBehaviour
    {
        bool isHovers;
        [SerializeField] LinearUIMove[] hovers;
        public bool IsHover
        {
            get => isHovers;
            set
            {
                if (isHovers != value)
                {
                    Refresh(value);
                }
                isHovers = value;
            }
        }
        private void OnEnable()
        {
            IsHover = false;
        }
        void Refresh(bool i)
        {
            foreach (LinearUIMove move in hovers)
            {
                move.IsMoving = i;
            }
        }
    }
}
