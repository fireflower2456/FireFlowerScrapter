using UnityEngine;

namespace FireFlower
{
    // Applies look's material to the fire canvas and holds the bloom volume between On and Off. Shared by many fires, it turns off when the last one releases it
    public class FireGlow
    {
        readonly FireLook look;
        IFeedbackHandle hold;

        public FireGlow(FireLook look, SpriteRenderer body)
        {
            this.look = look;
            if (look.material != null) body.sharedMaterial = look.material;
        }

        public void On()
        {
            if (hold == null && look.bloom != null) hold = VFXManager.Instance.HoldVolume(look.bloom, look.bloomIn, look.bloomOut, 50f);
        }

        public void Off()
        {
            hold?.Stop();
            hold = null;
        }
    }
}
