# FireFlowerScrapter

Block editor for Unity, kind of like Scratch. You put blocks together in an editor window, save them as an asset, and run that asset on a GameObject.

The blocks follow C# closely: variables, if/else, loops, waits, events, functions. There isn't a block for every Unity property. One dot block reads or writes any member instead, so `transform.position` or `rigidbody.linearVelocity` work the same way they do in code.

## Install

Unity 6000.3 or later. In the Package Manager, choose *Add package from git URL* and paste:

```
https://github.com/fireflower2456/FireFlowerScrapter.git#v0.5.0
```

Leave off `#v0.5.0` to follow the latest commit. The Unity packages it needs (Localization, Input System, Core RP, Cinemachine, uGUI) get installed along with it.

## Getting started

1. Open *Tools > FireFlower > Scraper* and press *New*. That creates a `ScrapAsset`.
2. Drag blocks from the palette under the start block. Ctrl+S saves.
3. Add a *Scrap Player* component to a GameObject and assign the asset.
4. Press Play. The script runs while the component is enabled. `(this)` in the blocks is that GameObject.

Spinning a cube:

```
when Spinner starts
forever
    (this).eulerAngles.z += 90 × Time.deltaTime
```

For other components, get one with `[Rigidbody2D] of (this)` or keep it in a component variable, then pick a member off it.

## Running from code

Scrap Player is basically this:

```csharp
using FireFlower;
using UnityEngine;

public class Trap : MonoBehaviour
{
    [SerializeField] ScrapAsset onEnter;
    ScrapRunner runner;

    void OnEnable() => runner = onEnter.Run(this);
    void Update() => runner?.Tick();
    void OnDisable() { runner?.Stop(); runner = null; }
}
```

`Tick` is what moves waits, loops and tweens forward, so call it every frame.

## Assemblies

| Assembly | What's in it |
|---|---|
| `FireFlower.Scrap` | Block runtime and the core blocks. No other dependencies. |
| `FireFlower.Scrapter.Editor` | The Scraper window, and `ScrapterWindow` if you want to build your own editor on top. |
| `FireFlower.Feedback` | Feedback tables, particles, sound, screen shake. `Feedback.Blocks` adds blocks that call them. |
| `FireFlower.Catalog` | ScriptableObject lists looked up by id or key. Assign them where they're used, e.g. `VFXManager.catalog`. |
| `FireFlower.Stat`, `Core`, `Common` | Stats, dev console, pooling, inspector attributes. |

## Docs

[Documentation~/Guide.md](Documentation~/Guide.md) goes into variables, dot blocks, writing your own blocks and editor windows, and what not to rename once assets are saved.

## License

MIT
