using System.Collections;
using TMPro;
using UnityEngine;

namespace FireFlower
{
    public class DamageParticle : ParticleBase
    {
        TextMeshPro damageT;
        Vector3 baseScale;
        float y;
        float x;
        [SerializeField] float gravity = 30;
        [SerializeField] float ySpeedOffset = 2.5f;
        [SerializeField] float ySpeed = 7.5f;
        [SerializeField] float lifeTime = 1.33f;
        [SerializeField] float xSpeed = 2f;
        static readonly WaitForFixedUpdate FixedStep = new();
        void Awake()
        {
            damageT = GetComponentInChildren<TextMeshPro>();
            baseScale = transform.localScale;
        }

        public void Show(float damage, Color color, float scale = 1f)
        {
            transform.localScale = baseScale * scale;
            damageT.color = new Color(color.r, color.g, color.b, 1f);
            damageT.text = Mathf.Abs(damage).ToString("F1");

            Core.Init(null);
        }

        protected override void OnPlay()
        {
            transform.position += Random.Range(-0.5f, 0.5f) * Vector3.right
                                + Random.Range(0.5f, 1f) * Vector3.up;
            y = Random.Range(ySpeed - ySpeedOffset, ySpeed + ySpeedOffset);
            x = Random.Range(-xSpeed, xSpeed);
            StartCoroutine(Die());
        }

        IEnumerator Die()
        {
            float t = 0f;
            while (t < lifeTime)
            {
                t += Time.fixedDeltaTime;
                transform.position += new Vector3(x, y, 0) * Time.fixedDeltaTime;
                y -= Time.fixedDeltaTime * gravity;
                yield return FixedStep;
            }
            Stop();
        }
    }
}
