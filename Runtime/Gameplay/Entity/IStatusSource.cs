using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // One status icon slot above a mob's head
    public interface IStatusIcon
    {
        Sprite Icon { get; }
        int Stack { get; }
        // Tooltip text shown when hovering over the icon
        string Header { get; }
        string Description { get; }
        float FillRatio { get; }   // Fill ratio of the cooldown overlay. Fills 0→1 as the duration passes; 0 (no overlay) without a duration
        event Action Changed;
    }

    // Emits status icons. The icon UI subscribes and draws them
    public interface IStatusSource
    {
        event Action<IStatusIcon> OnShown;
        event Action<IStatusIcon> OnHidden;
        IEnumerable<IStatusIcon> Shown { get; }
    }
}
