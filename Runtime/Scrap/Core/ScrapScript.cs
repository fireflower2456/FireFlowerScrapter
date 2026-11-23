using System;

namespace FireFlower
{
    // Writes a special effect used only by that item/buff as one whole C# class, without blocks. Picked from the script list on the ScrapWeaver canvas.
    // Leave what to revert on stop to Hold(ctx, onStop), and create runtime state fresh in OnEnabled
    [Serializable]
    public abstract class ScrapScript : ScrapPart { }
}
