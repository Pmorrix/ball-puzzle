# Circuit Collection 02

This folder is intentionally separate from `Assets/Prefabs/CircuitEditor` and the current gameplay collection.

## Included prefabs

- `Collection02_CompactCurve45`
- `Collection02_SCurve`
- `Collection02_InclineRamp`
- `Collection02_IntegratedOverpass`
- `Collection02_JumpModule`
- `Collection02_SwitchJunction`

## Structure

- `Prefabs`: the six Unity prefabs.
- `Meshes`: generated procedural meshes used by the new prefabs.
- `Previews`: Unity-rendered PNG previews.
- `Showcase`: an isolated Unity scene that displays the six pieces together.
- `Editor`: the isolated builder used to create or rebuild this collection.

The collection reuses the existing Classic wood and metal materials, the original curved rail-mount mesh, and the original rail dimensions and bend profile. Each generated track also includes the original-height wooden rim beneath the mounts so the guardrails are physically seated on the track. It is not wired into Level01, the palette, or the inventory system yet.

The switch junction includes a separate `Switch Pivot/Selector Tongue` hierarchy prepared for a future interaction component. No new runtime behaviour is added by this collection.
