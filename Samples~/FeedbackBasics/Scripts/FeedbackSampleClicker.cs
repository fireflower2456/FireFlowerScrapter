using UnityEngine;
using UnityEngine.InputSystem;
using FireFlower;

namespace FireFlower.Samples
{
    // Left click: plays "Click" from the feedback table at that spot (two particles at once).
    // Right click: calls catalog key "Ring" directly through VFXManager
    [RequireComponent(typeof(FeedbackBinder))]
    public class FeedbackSampleClicker : MonoBehaviour
    {
        [SerializeField] string feedbackKey = "Click";
        [SerializeField] string particleKey = "Ring";

        FeedbackBinder binder;
        Camera cam;

        void Awake()
        {
            binder = GetComponent<FeedbackBinder>();
            cam = Camera.main;
        }

        void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || cam == null) return;
            Vector3 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;
            if (mouse.leftButton.wasPressedThisFrame) binder.Play(feedbackKey, world);
            if (mouse.rightButton.wasPressedThisFrame) VFXManager.Instance.PlayParticle(particleKey, world);
        }
    }
}
