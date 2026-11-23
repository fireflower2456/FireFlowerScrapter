using UnityEngine;
using UnityEngine.InputSystem;

namespace FireFlower
{
    // One PlayerInput with three action maps: gameplay, UI and always-on. Mode enables only one of gameplay and UI.
    // The game's subclass decides where input goes via PlayerInput event methods
    public abstract class InputManagerBase<T> : ManagerBase<T> where T : InputManagerBase<T>
    {
        public enum InputMode
        {
            RunTime,
            UI
        }

        protected virtual string RuntimeMapName => "Runtime";
        protected virtual string UIMapName => "UI";
        protected virtual string AlwaysMapName => "Both";

        InputActionMap runtimeMap;
        InputActionMap uiMap;
        InputActionMap alwaysMap;
        Camera worldCamera;
        InputMode mode;

        public PlayerInput Input { get; set; }
        protected InputActionMap RuntimeMap => runtimeMap;

        public InputMode Mode
        {
            get => mode;
            set
            {
                if (mode == value) return;
                GetMap(mode)?.Disable();
                mode = value;
                GetMap(mode)?.Enable();
            }
        }

        InputActionMap GetMap(InputMode m) => m switch
        {
            InputMode.RunTime => runtimeMap,
            InputMode.UI => uiMap,
            _ => null,
        };

        public override void Boot()
        {
            Input = GetComponent<PlayerInput>();
            worldCamera = Camera.main;
            runtimeMap = Input.actions.FindActionMap(RuntimeMapName);
            uiMap = Input.actions.FindActionMap(UIMapName);
            alwaysMap = Input.actions.FindActionMap(AlwaysMapName);
            alwaysMap?.Enable();
            Mode = InputMode.RunTime;
        }

        // World position under the mouse (z = 0)
        public Vector2 GetMousePos()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            Vector3 w = worldCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            w.z = 0;
            return w;
        }

        // Rotation that points the object's up (+Y) from pos toward the mouse
        public Quaternion GetDirToMouse(Vector2 pos)
        {
            Vector2 dirVec = GetMousePos() - pos;
            float angle = Mathf.Atan2(dirVec.y, dirVec.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0, 0, angle - 90f);
        }
    }
}
