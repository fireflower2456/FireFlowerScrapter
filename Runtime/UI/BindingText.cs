using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FireFlower
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class BindingText : MonoBehaviour
    {
        [SerializeField] InputActionReference action;
        [SerializeField] int bindingIndex;
        TextMeshProUGUI text;
        public InputAction Action => action != null ? action.action : null;

        void Awake() => text = GetComponent<TextMeshProUGUI>();

        void OnEnable()
        {
            InputSystem.onActionChange += OnActionChange;
            Refresh();
        }

        void OnDisable()
        {
            InputSystem.onActionChange -= OnActionChange;
        }

        // Rebinding and restoring defaults both re-resolve controls, so BoundControlsChanged catches both
        void OnActionChange(object _, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
                Refresh();
        }

        void Refresh()
        {
            if (Action == null) return;
            // onActionChange is shared by all subscribers, so throwing here breaks other subscribers like PlayerInput
            if (bindingIndex < 0 || bindingIndex >= Action.bindings.Count)
            {
                Debug.LogError($"BindingText: {Action.name} has {Action.bindings.Count} bindings, so index {bindingIndex} does not exist", this);
                return;
            }
            text.text = Action.GetBindingDisplayString(bindingIndex);
        }
    }
}
