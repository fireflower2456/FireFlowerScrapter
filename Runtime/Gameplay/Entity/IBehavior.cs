using UnityEngine;

namespace FireFlower
{
    public interface IBehavior
    {
        public void InitController(MobBase mob, Animator anim)
        {
            Controller = (this as MonoBehaviour).GetComponent<EntityController>();
            Controller?.Init(mob, anim);
        }
        EntityController Controller { get; set; }
    }
}
