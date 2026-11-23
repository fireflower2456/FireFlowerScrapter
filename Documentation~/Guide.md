# FireFlowerScrapter Guide

1. [Concepts](#1-concepts)
2. [Using the Scraper](#2-using-the-scraper)
3. [Variables](#3-variables)
4. [Dot blocks — reaching any value](#4-dot-blocks--reaching-any-value)
5. [Running scripts](#5-running-scripts)
6. [Writing your own blocks](#6-writing-your-own-blocks)
7. [Your own editor window](#7-your-own-editor-window)
8. [Optional modules](#8-optional-modules)
9. [Saved data rules](#9-saved-data-rules)
10. [Performance](#10-performance)

## 1. Concepts

Blocks are **another way to write a C# script**. The thinking is the same; only the shape differs.

| C# | Blocks |
|---|---|
| a script (class) | `ScrapSet` — the main line of blocks (`parts`), definitions (My Blocks), variable declarations. `ScrapAsset` is a ScriptableObject holding one. |
| an instance, `this` | `ScrapRunner` and its **(this)**: the object running the script |
| an event handler's argument | **(other)**: the other side of the event that started the line |
| a `foreach` variable | the **(element)** pill on a *for each* block's header — drag it into the body, like a My Block input |
| an expression / assignable value | value blocks (`DataSource`: number, Vector2, Vector3, Component). Writable ones can be assigned. |
| a statement | action blocks (`ActionBlock`): change a value, tween, signal, … |
| `if`, loops, `await` | control blocks (`ControlBlock`), waiting blocks (`IScrapStep`) |
| an event handler | triggers (`FireBlock` + `TriggerEvent`) |
| a method | My Blocks (`FunctionBlock` / `CallBlock`) |
| `a.b.c` | dot blocks (`(value) . member.member`) |

**One block covers assignment.** `[value] += / -= / += % / *= % / = [value]` accepts numbers, vectors and components (components: `=` only). Position, velocity, rotation, stats, variables and dot blocks are all writable values, so there are no special "move" or "set velocity" blocks. To change something over time use the **tween** block (linear, ease, expo curves).

### How a line runs (same as Scratch)

- A line runs top to bottom. Waits, *repeat n*, *repeat until*, tweens and *if* hold the line until they finish.
- *Forever* is an **end block**: nothing can attach after it, and the line keeps looping there.
- To run something alongside, put it in *run separately { }*. It stops when the script stops.
- Each loop iteration yields one frame.

## 2. Using the Scraper

**Tools ▸ FireFlower ▸ Scraper** is a ready-to-use editor.

- **New** creates a `ScrapAsset` (also *Create ▸ FireFlower ▸ Scrap*). Double-clicking a `ScrapAsset` opens it here.
- The asset field opens **any ScriptableObject that has a `ScrapSet` field** (also right-click ▸ *Open as Blocks* in the Project window).
- Palette on the left, canvas in the middle. Drag blocks into sockets; right-click a block for its menu; **Ctrl+S** saves. Changes are only written to the asset when you save.
- Mouse wheel zooms, dragging empty space pans, Shift+drag box-selects, Alt+drag moves a single block out of a stack.

## 3. Variables

Create variables from the **Variables** category (*Make a Variable*).

| Scope | Shared by | C# analogy |
|---|---|---|
| Local | one script (or every runner that shares the same `locals` owner) | a field |
| Object | every script running on the same mob | the object's blackboard |
| Global | the whole game (keep it for things like currency) | `static` |
| Persistent | survives saves (float only). Your save system listens to `PersistentVariables.Changed` and saves `PersistentVariables.Values.Numbers()`; it loads with `Values.SetNumber(key, value)`. | save data |

Types: `float · int · bool · Vector2 · Vector3 · Transform · Component` (Rigidbody2D, Collider2D, SpriteRenderer, Animator, AudioSource). The type is shown next to the variable's name.

Built-in variables: **this**, **other**, Time.time, Time.deltaTime.

There is no hidden "target". Every block that acts on an object takes it in a socket, so you can always see where a value comes from:

- **(this)** never changes for a running script.
- **(other)** comes from the trigger hat above the line — the sender of a signal, the object that hit this one, the enemy that was killed …
- An **(element)** comes from the block that gives it: a *for each* block has a pill on its header (named after what it gives, e.g. *object*). Drag the pill into the block's body. Each block has its own pill, so inside nested loops you can still use the outer one. Outside its block it is empty.

**bool variables** are set like every other variable: `(flag) = <condition>`. Conditions are bool values.

## 4. Dot blocks — reaching any value

A dot block is `(value) . member.member`, like C#. **The type of the value on the left decides what you can pick**:

- `(this) . position` — (this) is a Transform, so Transform's members appear.
- `[Rigidbody2D of (this)] . linearVelocity` — the **GetComponent** block (`[component] of [object]`) turns an object into one of its components.
- `(my body) . linearVelocity` — a Component variable typed `Rigidbody2D`.

Details:

- Members you can reach follow C# and the Inspector: public fields and properties, and `[SerializeField]` fields.
- There is one dot block in the palette. Dropped into a number socket it reads/writes numbers (`float`, `int`, `bool`, enums); into a Vector2 or Vector3 socket it reads/writes vectors. The menu only lets you finish on a member that fits; other members open the next level (`position ›` → `x`).
- Writing into a struct (`color.a`, `position.x`) writes the struct back. Writing a Vector2 into a Vector3 keeps the old z.
- Turn on **revert** to restore the old value when the block that changed it stops.
- Enum values: use the enum value block (`RigidbodyType2D.Kinematic`).
- Invalid paths are shown in red with the reason in the tooltip.

Angles are **Unity angles**: 0 = right, 90 = up, increasing counter-clockwise — the same as `eulerAngles.z` and `Mathf.Atan2`.

Facing and turning are built from values, not a special block:

- face B: `(this) . eulerAngles.z = [direction from (this).position to B]`
- turn along a curve: tween `(this) . eulerAngles.z` to that direction — tweens on angle members (`eulerAngles`, `rotation`, `…angle`) take the short way (350° → 10° turns 20°)
- follow at a fixed speed: inside *forever*, `(this) . eulerAngles.z = [current] turned toward [direction] by at most [speed × Time.deltaTime]`
- With a dot path ending in an enum next to it (e.g. `(…) . bodyType = [enum]`), the enum value picker only offers that enum's values.

## 5. Running scripts

**Without code:** add **Scrap Player** to a GameObject and assign a `ScrapAsset`. It starts on enable and stops on disable. Changing `ScrapPlayer.Scrap` from code restarts it.

**From code:**

```csharp
ScrapRunner runner = scrapAsset.Run(this);   // (this) in the blocks; can be null
runner.Tick();                               // every frame — forgetting this freezes waits, loops and tweens
runner.Stop();                               // when done — anything held with Hold is reverted
```

- `ScrapSet.Run(self)` — `self` is any Component; it becomes **(this)**.
- `ScrapSet.RunWith(self, host)` — also passes whatever started the script (`ctx.host`) for blocks that need to talk back to it.
- `ScrapSet.RunWith(self, host, shared, locals)` — also enables `shared`'s definitions (My Blocks), and runners given the same `locals` share local variables (for example the states of a state machine).
- `runner.LineDone` — the main line reached its end (side work not counted). `runner.Finished` — nothing is waiting either.
- Signals: `ScrapSignal.Raise(key, sender, target, payload)`; the *when I receive signal [key]* hat listens, and (other) is the sender. A component implementing `IScrapSignalListener` on the sender reacts to every signal it sends.
- Your own events: make the payload implement `IScrapEvent` (or be a Component) and (other) reads it.

## 6. Writing your own blocks

Write a class and it appears in the palette. The editor needs no changes.

### Pick a base

| Kind | Base | Implement |
|---|---|---|
| action | `ActionBlock` | `OnEnabled(ctx)` |
| waiting action | `ActionBlock, IScrapStep` | `Run(ctx, resume)` → returns a cancel `Action` |
| control (inner line) | `ControlBlock` | start child lines with `Enable` / `Spawn` / `Disable` |
| trigger | `TriggerEvent<T>` / `TargetedEvent<T>` | `Listen(ctx, handler, on)` |
| number value | `ValueSource` | `TryRead`; `Add` if writable |
| vector / component value | `Vector2Source` / `Vector3Source` / `ObjectSource` / `TransformSource` | `TryRead`; `Write` if writable |
| condition | `ScrapCondition` | `Check(ctx)` |

### Example: an action

```csharp
using System;
using FireFlower;
using ScrapWeaver;
using UnityEngine;

// Number inputs are value sockets, not float fields, so any value block can go in.
[Serializable, Category("Action"), Display("launch", nameof(target), "with power", nameof(power))]
public class LaunchBlock : ActionBlock
{
    [SerializeReference, SubclassSelector] TransformSource target = new ThisValue();
    [SerializeReference, SubclassSelector] ValueSource power = new NumberValue(5f);

    public override void OnEnabled(ScrapContext ctx)
    {
        Transform t = TransformSource.ReadOrNull(ctx, target);
        if (t != null && t.TryGetComponent(out Rigidbody2D body))
            body.linearVelocity += Vector2.up * ValueSource.ReadOr(ctx, power, 0f);
    }
}
```

### Example: a waiting block

```csharp
[Serializable, Display("blink for", nameof(seconds), "s")]
public class BlinkBlock : ActionBlock, IScrapStep
{
    [SerializeReference, SubclassSelector] ValueSource seconds = new NumberValue(1f);

    public override void OnEnabled(ScrapContext ctx) { }

    // Call resume to move on to the next block. The returned Action is called if the line is stopped.
    public Action Run(ScrapContext ctx, Action resume) =>
        ctx.After(ValueSource.ReadOr(ctx, seconds, 0f), resume);
}
```

> To read or write a single component value, don't write a block — use a dot block.

### Attributes (`using ScrapWeaver;`)

| Attribute | Meaning |
|---|---|
| `[Display("text", nameof(field), …)]` | The block's sentence: text pieces and field sockets, left to right. |
| `[Category("name")]` | Palette category (inherited from the base). Built-in: Action, Looks, Sound, Events, Control, Sensing, Operators, Variables, My Blocks. |
| `[Module("name")]` | Only shown when the edited data has loaded that module (game-specific blocks). |
| `[Cap]` | End block: no bottom notch, nothing attaches after it. |
| `[Family("name", primary)]` | Type variants of one block. The palette shows the primary; dropping into a socket creates the variant that fits. |
| `[Reusable]` | The class fills all its runtime fields in `OnEnabled`/`Run` itself, so pooled copies skip the reset (faster). |
| `[VariableKey(scopeField, type)]` | Draws a string field as a variable picker. |
| `[MemberPath(rootField)]` / `[EnumName]` / `[ComponentType]` | Draws a string field as a member path / enum value / component type picker. |
| `[Branches]` | Array elements are branches of the parent block (+ / − buttons). |
| `[NotCommon]` | Hidden from shared palettes of other editors. |

A block that passes an object to its inner line implements `IElementSource` (`ElementId`, `ElementName`) and runs its children with `ctx.WithElement(ElementId, obj)`; the editor adds the pill.

### Runtime state rules (important)

1. Block data (the asset) is shared. A running script uses **copies**, pooled and reused (each loop iteration, trigger firing, My Block call).
2. **Classes that are not `ScrapPart` (values, conditions, trigger events) must not hold runtime state** — they are shared between copies.
3. Runtime fields of a `ScrapPart` (non-serialized fields) are **reset to their initial values automatically** when a copy is reused, so custom blocks are safe by default. Mark a class `[Reusable]` if it fills those fields itself.
4. Anything to undo when the script stops goes through `Hold(ctx, onStop)`. A held copy is not returned to the pool. If you override `OnDisabled`, call `base`.
5. Never change serialized (setting) fields while running — they are shared with the asset.

## 7. Your own editor window

Inherit `ScrapterWindow` to get the palette, canvas, drag & drop, copy/paste, undo and save. The data only needs a `ScrapSet` field. `ScraperWindow` is the shortest example.

```csharp
using FireFlower;
using UnityEditor;
using UnityEngine;

public sealed class SkillScraperWindow : ScrapterWindow
{
    protected override string WindowName => "Skill Scraper";

    [MenuItem("Tools/Skill Scraper")]
    static void Open()
    {
        var window = GetWindow<SkillScraperWindow>();
        if (Selection.activeObject is SkillData data) window.SelectItem(data);
    }

    protected override string EmptyHelp => "Pick a skill.";
}

[CreateAssetMenu] public class SkillData : ScriptableObject { public ScrapSet blocks; }
```

Common hooks:

| Hook | Use |
|---|---|
| `BuildToolbar(host)` | the top bar (pickers, buttons) |
| `ShowsCategory` / `ShowsBlock` | filter the palette |
| `Modules` | loaded module names (shows `[Module]` blocks) |
| `WorkingSet` / `SetPath` | the `ScrapSet` the canvas edits and its path (default: the data's first `ScrapSet` field) |
| `DefinitionSet` / `VariableSet` / `LocalVariableUsers` | where My Blocks and local variables are declared, and which blocks share them |
| `AddRootNodes()` | hats and nodes placed on the canvas |
| `RefreshSidePane()` | the right-hand pane |
| `RootTitle()` / `DecorateRootTitle` | the start hat's text |
| `SaveDirect()` / `OnVariableRenamed` | extra work on save (for example updating a prefab) |

## 8. Optional modules

The core (`FireFlower.Scrap`) knows nothing about any game. `FireFlower.Feedback.Blocks` adds optional blocks — use them, or ignore them and write your own the same way (game rules such as damage or stats belong in your project, built like the examples in section 6):

- **Feedback**: *play particle*, *set BGM*, *play feedback* (needs the package's VFX/Sound managers).

## 9. Saved data rules

Blocks are `[SerializeReference]`, so **class name, namespace, assembly and field names** are written into the asset YAML (`type: {class: …, ns: …, asm: …}`).

- Renaming a class, namespace or assembly: add `[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "old", sourceAssembly: "old", sourceClassName: "old")]`, re-save the assets, then remove it.
- Renaming a field: `[FormerlySerializedAs("old")]`, re-save, then remove it.
- Replacing a block with a different structure: keep the old type, convert the data with an editor script, save, then delete the old type.
- Afterwards check for missing types with `SerializationUtility.HasManagedReferencesWithMissingTypes`.
- This package's blocks live in `ns: FireFlower`, `asm: FireFlower.Scrap` (core) or `asm: FireFlower.Feedback.Blocks` (feedback blocks).

## 10. Performance

- Loops, triggers and calls reuse pooled copies. One iteration of a basic block costs about 2 µs with no allocation.
- To do work every frame inside a block use `ctx.SubscribeTick` (put the returned unsubscribe in `Hold`); for a one-off delay use `ctx.After`.
- `[Reusable]` removes the reset cost for blocks that fill their own runtime fields.
- Dot blocks cache path resolution, but reads and writes use reflection. For a value used heavily every frame, a dedicated value block is faster.
