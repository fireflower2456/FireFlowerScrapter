using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FireFlower
{
    // Keyboard keys show as text on a keycap; mouse buttons show as a button image without a keycap
    public class ReBindingUI : MonoBehaviour
    {
        public TextMeshProUGUI label;
        public Button button;
        [SerializeField] GameObject keyCap;
        [SerializeField] TextMeshProUGUI keyText;
        [SerializeField] Image mouse;
        [Header("Mouse")]
        [SerializeField] Sprite leftClick;
        [SerializeField] Sprite scrollClick;
        [SerializeField] Sprite rightClick;
        [SerializeField] Sprite otherButton;

        Sprite idleSlot;

        public void Show(InputAction action, int bindingIndex)
        {
            string text = action.GetBindingDisplayString(bindingIndex, out string device, out string control);
            bool isMouse = !string.IsNullOrEmpty(device) && InputSystem.IsFirstLayoutBasedOnSecond(device, "Mouse");

            SetSlot(false);
            keyCap.SetActive(!isMouse);
            mouse.gameObject.SetActive(isMouse);
            if (isMouse) mouse.sprite = MouseSprite(control);
            else keyText.text = text;
        }

        // Pressed slot without a keycap while waiting for new input
        public void ShowWaiting()
        {
            SetSlot(true);
            keyCap.SetActive(false);
            mouse.gameObject.SetActive(false);
        }

        void SetSlot(bool waiting)
        {
            if (idleSlot == null) idleSlot = button.image.sprite;
            button.image.sprite = waiting ? button.spriteState.pressedSprite : idleSlot;
        }

        Sprite MouseSprite(string control) => control switch
        {
            "leftButton" => leftClick,
            "middleButton" => scrollClick,
            "rightButton" => rightClick,
            _ => otherButton,
        };
    }
}
