# Third-party notices

## CS2PerformancePatcher / Cs2Saver

- Source: <https://github.com/NspxMiguel/CS2PerformancePatcher>
- Commit: `69015dd90cfc667c100beb16fd158a43a9e03a23`
- Copyright (C) 2026 NSPX (<https://nspx.dev>)
- License: GNU General Public License v3.0 (see [LICENSE](LICENSE))

Adapted parts (each file carries a notice at the top):

| This repository | Adapted from | What was taken |
|---|---|---|
| `src/CS2Optimization/Rendering/PrefabLodSystem.cs` | `src/Cs2Saver.Mod/PrefabLodFloorSystem.cs`, `RenderBudget.cs` | Raising `ObjectGeometryData.m_MinLod` on prefabs after `ObjectInitializeSystem`, remembering originals, the static/net prop selection with buildings, trees and plants excluded, the "+6 halves the distance" scale. |
| `src/CS2Optimization/Rendering/ShadowDistanceSystem.cs` | `src/Cs2Saver.Mod/CityLookSystem.cs` | A hidden global HDRP `Volume` with an `HDShadowSettings.maxShadowDistance` override. |
| `src/CS2Optimization/CS2Optimization.csproj` | `src/Cs2Saver.Mod/Cs2Saver.Mod.csproj` | Referencing game assemblies from the player's install with `Private=false`. |

Changes from the original: only small props are touched (no citizens, vehicles or trees); the
shadow limit follows the game's per-frame distance and never lengthens it, keeps the game's
cascade boundaries inside the limit, and does not force the sun's shadows, resolution or
cascade count; prefab originals are restored on Off, failure and unload; colour grading,
material changes, CSV logging and presets were not taken.

Because code under GPL-3.0 is included, this mod as a whole is distributed under GPL-3.0.

## Write Everywhere

<https://github.com/klyte45/CS2-WriteEverywhere> (commit `ac76064f67860d3421a565a25fff1917e15710c2`)
was only studied to understand its interaction with props. **No code from it is included.** This
mod only checks whether an assembly named `BelzontWE` is loaded.

## Game assemblies

Cities: Skylines II, Unity and HDRP assemblies are referenced at build time from the player's
own installation and are **not** included in the release ZIP (the build fails if one ends up in
the output).
