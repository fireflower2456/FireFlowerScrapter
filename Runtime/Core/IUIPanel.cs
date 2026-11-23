using UnityEngine;

namespace FireFlower
{
    // A screen opened and closed as UI states enter and exit. Default is enable/disable — implement it yourself for fades or close-time work
    public interface IUIPanel
    {
        GameObject gameObject { get; }
        void SetOpen(bool open) => gameObject.SetActive(open);
    }
}
