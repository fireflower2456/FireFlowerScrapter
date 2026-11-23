using System.Collections;
using UnityEngine;

namespace FireFlower
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ParticleUp : ParticleBase
    {
        SpriteRenderer sr;
        [SerializeField] float upSpeed = 0.5f;
        [SerializeField] float disappearedSpeed = 0.1f;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        protected override void OnInit()
        {
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }

        protected override void OnPlay() => StartCoroutine(Die());

        IEnumerator Die()
        {
            while (sr.color.a > 0)
            {
                transform.position += Vector3.up * upSpeed  * Time.deltaTime;
                sr.color -= new Color(0, 0, 0, Time.deltaTime / disappearedSpeed);
                yield return null;
            }
            Stop();
        }
    }
}
