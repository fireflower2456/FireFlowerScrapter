# FireFlowerScrapter

A Scratch-style **block editor for Unity**. Build behaviour out of blocks instead of C#, save it as an asset, and run it on any GameObject.

Blocks think like C#: variables, `if`, loops, `await`-style waits, events, functions, and `object.member.member` access to anything on a component. The goal is *few blocks, consistent structure* — read and write the values Unity already has instead of adding special-purpose blocks.

## Install

1. Unity **6000.3** or newer.
2. Install these two first (Package Manager ▸ **+** ▸ *Install package from git URL…*):
   - NaughtyAttributes — `https://github.com/dbrizov/NaughtyAttributes.git#upm`
   - SerializeReferenceExtensions — `https://github.com/mackysoft/Unity-SerializeReferenceExtensions.git?path=Assets/MackySoft/MackySoft.SerializeReferenceExtensions`
3. Install this package: Package Manager ▸ **+** ▸ *Install package from disk…* ▸ pick this folder's `package.json` (or *from git URL* if it is hosted).
   Unity dependencies (Addressables, Localization, Input System, URP, Cinemachine, uGUI) come in automatically.

## Quick start

1. **Make a script asset.** Open **Tools ▸ FireFlower ▸ Scraper** and press **New** (or *Create ▸ FireFlower ▸ Scrap*). This creates a `ScrapAsset`.
2. **Build it.** Drag blocks from the palette onto the canvas, under the start hat. Save with **Ctrl+S**.
3. **Attach it.** Add the **Scrap Player** component (*Add Component ▸ FireFlower ▸ Scrap Player*) to any GameObject and drop the asset into its slot.
4. **Press Play.** The script starts when the component is enabled and stops when it is disabled. In the blocks, **(this)** is that GameObject.

Example — spin a cube forever:

```
when Spinner starts
forever
    (this) . eulerAngles.z  +=  (90 × (Time.deltaTime))
```

`(this) . eulerAngles.z` is a *dot block*: pick a value, type `.`, and choose a member, exactly like `transform.eulerAngles.z` in C#. To reach another component, use the GetComponent block **[Rigidbody2D] of (this)** or a Component-typed variable, then pick its member (`linearVelocity`, `mass`, …).

## Running from code

```csharp
using FireFlower;
using UnityEngine;

public class Trap : MonoBehaviour
{
    [SerializeField] ScrapAsset onEnter;
    ScrapRunner runner;

    void OnEnable() => runner = onEnter.Run(this); // (this) in the blocks = this object
    void Update() => runner?.Tick();               // every frame — waits, loops and tweens advance here
    void OnDisable() { runner?.Stop(); runner = null; }
}
```

`ScrapPlayer` does exactly this for you.

## What's inside

| Assembly | Contents |
|---|---|
| `FireFlower.Scrap` | The block runtime: `ScrapSet`, `ScrapRunner`, `ScrapAsset`, `ScrapPlayer` and the core blocks. Depends on nothing game-specific. |
| `FireFlower.Gameplay` | Optional modules on top: a mob framework (`Entity/`), a state controller (`Composable/`), and feedback blocks (`Blocks/`: particles, BGM, feedback tables). |
| `FireFlower.Scrapter.Editor` | The Scraper window, the base `ScrapterWindow` for your own editors, block field drawers. |
| `FireFlower.Feedback` | Feedback tables, particles, sound, VFX. |
| `FireFlower.Catalog` (+Editor) | Addressables catalogs. |
| `FireFlower.Stat` / `Core` / `UI` | Stat sheet, dev console, time, pooling, UI helpers. |

## Documentation

- [Guide](Documentation~/Guide.md) — concepts, variables, dot blocks, running scripts, writing your own blocks, your own editor windows, saved-data rules, performance.
