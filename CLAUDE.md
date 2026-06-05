# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Mod Does

"Vanillafied Vivi" (`kahirDragoon.vanillafiedVivi`) patches the [Vivi Race mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3241577976) (`gguake.race.vivi`) to make Vivis use the vanilla Human race instead of a custom race. This improves compatibility and visual consistency. The mod:
- Replaces the Vivi pawnkind race with `Human`
- Removes Vivi-specific hair, head types, and RPEF pawn constraints
- Patches the Vivi xenotype with vanilla-compatible genes plus custom genes defined here
- Moves Vivi buildings into standard architect menu categories (removing the custom `VV_Bulidings` category)
- Restricts the Vivi Cream recipe to pawns with the `VVV_CreamMaker` gene via a custom `Bill` class

## Building

```bash
dotnet build Source/VanillafiedVivi/VanillafiedVivi.csproj -c Release
```

The `Release` configuration optimizes and suppresses debug symbols, outputting to `1.6/Assemblies/VanillafiedVivi.dll`. The `Debug` config also exists.

**Required external DLLs** (from the Vivi Race Workshop mod):
- `RPEF.dll` — bundled in `1.6/Assemblies/`; also referenced for build from `{Steam}/workshop/content/294100/3241577976/v1.6/Assemblies/`
- `VVRace.dll` — referenced for build from `{Steam}/workshop/content/294100/3241577976/v1.6/Assemblies/`

**Additional runtime dependency:** Vanilla Expanded Framework (VEF) — used for `VEF.Genes.GeneExtension` on `VVV_ViviPhysiology` to force female gender.

The `.csproj` uses `Krafs.Rimworld.Ref` (NuGet) for RimWorld API stubs and `Lib.Harmony` (NuGet) for patching — both excluded from runtime output.

## Architecture

### C# Assembly (`Source/VanillafiedVivi/`)

- **`Bill_ViviCreamMakerOnly`** — Subclasses `Bill_ProductionWithFoodDrain` (VVRace). Overrides `PawnAllowedToStartAnew` to gate cream-making on `VVV_CreamMaker` gene. Assigned via XML (`Patches/ViviPatches.xml`).
- **`GeneConstraint`** — Subclasses `RPEF.Constraint<GeneDef>`. Checks pawn for any of a configured gene list. Used in `Defs/ConstraintDefs.xml` → `VVV_CreamMakerConstraint`.
- **`Gene_ViviPhysiology`** — Core gene for royal Vivis (`VVV_ViviPhysiology` def). Tracks egg progress (15-day cycle via `EggProgressPerDay`), applies `VV_GeneticUnstability` hediff on xenogerm side effects, provides `Gizmo_EggProgress` and a toggle gizmo, calls `SetRoyal()` on adulthood via `OnBecameAdult()`.
- **`Gizmo_EggProgress`** — Progress-bar UI gizmo displaying egg readiness % and per-day rate. Only shown when a single pawn is selected.
- **`JobGiver_VVVLayViviEgg`** — Think node; returns priority 10 when `Gene_ViviPhysiology.CanLayEgg`, finds nearest available `ViviEggHatchery`, issues `VVV_LayViviEgg` job.
- **`JobDriver_VVVLayViviEgg`** — Walks to hatchery, waits 500 ticks, calls `gene.ProduceEgg()` and deposits into the hatchery (or drops nearby if full).
- **`VVVDefOf`** — `[DefOf]` class: `VVV_LayViviEgg` (JobDef), VVRace hediffs (`VV_RoyalVivi`, `VV_GeneticUnstability`), vanilla xenogerm hediffs, `Body_Female` GeneDef, `VV_RoyalJelly` NeedDef.
- **`HarmonyPatches`** — Three Harmony patches (see Key Design Notes).

### XML Defs (`Defs/`)

- **`Jobs/JobDefs.xml`** — Defines `VVV_LayViviEgg` (non-interruptible, `overrideFlyChance: 0`).

- **`GeneDefs/Genes.xml`** — Defines four custom genes:
  - `VVV_HoneyGatherer`: honey/plant gathering stat offsets (hooks into VVRace stats)
  - `VVV_CosmeticWings`: cosmetic wings using VVRace render workers and fly animations; uses `RPEF.HumanlikeFlyExtension` for flight animation data
  - `VVV_ViviPhysiology`: backed by `Gene_ViviPhysiology`; enables `VV_RoyalJelly` need; forces female via `VEF.Genes.GeneExtension`
  - `VVV_CreamMaker`: marker gene that unlocks the cream-making recipe

- **`ConstraintDefs.xml`** — Defines `VVV_CreamMakerConstraint` using the custom `GeneConstraint` class. This is swapped in by `ViviPatches.xml` to replace the original constraint on `VV_MakeVivicream`.

### XML Patches (`Patches/`)

- **`ViviPatches.xml`** — The core patch file. Key operations:
  - Replaces `ViviBase` PawnKindDef race → `Human`
  - Removes RPEF `ConstraintModExtension` from backstories and the `VV_Vivi` xenotype
  - Adds vanilla-compatible genes to the `VV_Vivi` xenotype (speed, temp, pain, melee penalty, body type, plus the three custom genes)
  - Removes Vivi-specific hair defs, head type defs, and the head type constraint from `VV_PawnConstraint`
  - Adds backstory filters to `VV_ViviFederation` faction and vanilla hair tags to `VV_ViviCulture`
  - Swaps the cream recipe's constraint to `VVV_CreamMakerConstraint`

- **`ArchitectMenuPatches.xml`** — Moves all Vivi buildings/furniture/floors into standard vanilla architect categories and removes the custom `VV_Bulidings` designation category. Also adds `VVRace.Designator_FortifyHoneycombWall` to the Orders and Structure menus.

- **`ThinkTreePatch.xml`** — Injects `JobGiver_VVVLayViviEgg` into `MainColonistBehaviorCore` before `JobGiver_Work`. The giver gates itself to pawns with a ready egg, so it's a cheap no-op for all others.

### Language (`Language/`)

- `DefInjected/GenesDef/Language_Genes.xml` — Overrides the description of `VV_ViviMetabolism` (a gene from the base Vivi mod)
- `Keyed/KeyedLanguage.xml` — Defines `CannotDoBillMissingCreamGene` string (used as the failure reason when a pawn lacks the cream maker gene)

## Key Design Notes

- **Harmony patches** — initialized via `[StaticConstructorOnStartup]` in `HarmonyPatches.cs`. Three patches:
  - `Patch_CompVivi_RefreshHairColor` (prefix) — skips VVRace hair-colour lerp for Human pawns that carry `CompVivi` but lack `Gene_ViviPhysiology` (avoids wrong-race errors).
  - `Patch_LifeStageWorker_Notify_LifeStageStarted` (postfix) — on adult transition, calls `Gene_ViviPhysiology.OnBecameAdult()` which triggers `SetRoyal()`.
  - `Patch_CompViviHatcher_Hatch` (prefix, patched by name) — replaces hatch logic for eggs produced by normalized Vivis: spawns a Human pawn with `VV_Vivi` xenotype, inherits filtered xenogenes from parent, ensures `VVV_ViviPhysiology` is included.
- **Commented-out sections** in `ViviPatches.xml` represent in-progress work: apparel removal and pawnkind apparel tag changes are disabled. The `differences vivi human race.txt` file tracks remaining stat differences to address via genes.
- The mod is acknowledged as incomplete in `About/About.xml` — royal Vivi xenotype, female-only constraint, and genetic instability hediff are listed as todos in the patch file comments.
- `VVV_CosmeticWings` grants `MaxFlightTime` and `FlightCooldown` stat offsets but the gene description says flight is cosmetic only — this stat offset enables the VVRace flight system on human pawns.
