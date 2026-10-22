# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Mod Does

"Vanillafied Vivi" (`kahirDragoon.vanillafiedVivi`) patches the [Vivi Race mod](https://steamcommunity.com/sharedfiles/filedetails/?id=3241577976) (`gguake.race.vivi`) to make Vivis use the vanilla Human race instead of a custom race. This improves compatibility and visual consistency. The mod:
- Replaces the Vivi pawnkind race with `Human`
- Removes Vivi-specific hair, head types, and RPEF pawn constraints
- Patches the Vivi xenotype with vanilla-compatible genes plus custom genes defined here
- Moves Vivi buildings into standard architect menu categories (removing the custom `VV_Bulidings` category)
- Restricts the Vivi Cream recipe to pawns with the `VVV_CreamMaker` gene by swapping the recipe's RPEF constraint for a gene constraint

## Building

```bash
dotnet build Source/VanillafiedVivi/VanillafiedVivi.csproj -c Release
```

The project multi-targets: `net481` builds `1.6/Assemblies/VanillafiedVivi.dll` against `Krafs.Rimworld.Ref` 1.6, and `net10.0` builds `1.7/Assemblies/VanillafiedVivi.dll` against `Krafs.Rimworld.Ref` 1.7 (RimWorld 1.7 runs on .NET 10, with Unity types provided by `UnityStubs`). One build produces both. The `Release` configuration optimizes and suppresses debug symbols; the `Debug` config also exists. The copy step fails while RimWorld is running, because the game locks the DLL it loaded.

**Required external DLLs** (`RPEF.dll`, `VVRace.dll` from the Vivi Race mod): always referenced from the Workshop copy, `{Steam}/workshop/content/294100/3241577976/v1.6/Assemblies/` and `.../v1.7/Assemblies/` for the matching target. Never compile against the local Vivi copy in `d:\Rimworld Modding\RWMod-Vivi-master` — it is for code lookup only and can be ahead of the Workshop. If code needs Vivi API that isn't on the Workshop yet, it can't be built until Steam delivers the update. Vivi only updates its 1.7 build from October 2026 on, so new Vivi API may exist only in `v1.7`.

**Additional runtime dependency:** Vanilla Expanded Framework (VEF) — used for `VEF.Genes.GeneExtension` on `VVV_ViviPhysiology` to force female gender.

The `.csproj` uses `Krafs.Rimworld.Ref` (NuGet) for RimWorld API stubs and `Lib.Harmony` (NuGet) for patching — both excluded from runtime output.

## Architecture

### C# Assembly (`Source/VanillafiedVivi/`)

- **`GeneConstraint`** — Subclasses `RPEF.Constraint<GeneDef>`. Checks pawn for any of a configured gene list. Used in `Defs/ConstraintDefs.xml` → `VVV_CreamMakerConstraint`, which gates cream-making: RPEF's `Bill.PawnAllowedToStartAnew` postfix evaluates the recipe's constraint.
- **`Gene_ViviPhysiology`** — Core gene for royal Vivis (`VVV_ViviPhysiology` def). Tracks egg progress (15-day cycle via `EggProgressPerDay`), applies `VV_GeneticUnstability` hediff on xenogerm side effects, provides `Gizmo_EggProgress` and a toggle gizmo, calls `SetRoyal()` on adulthood via `OnBecameAdult()`.
- **`Gizmo_EggProgress`** — Progress-bar UI gizmo displaying egg readiness % and per-day rate. Only shown when a single pawn is selected.
- **`JobGiver_VVVLayViviEgg`** — Think node; returns priority 10 when `Gene_ViviPhysiology.CanLayEgg`, finds nearest available `ViviEggHatchery`, issues `VVV_LayViviEgg` job.
- **`JobDriver_VVVLayViviEgg`** — Walks to hatchery, waits 500 ticks, calls `gene.ProduceEgg()` and deposits into the hatchery (or drops nearby if full).
- **`VVVDefOf`** — `[DefOf]` class: `VVV_LayViviEgg` (JobDef), VVRace hediffs (`VV_RoyalVivi`, `VV_GeneticUnstability`), vanilla xenogerm hediffs, `Body_Female` GeneDef, `VV_RoyalJelly` NeedDef.
- **`HarmonyPatches`** — Harmony patches and the `IsTrueVivi` / `FitsViviHatchery` helpers (see Key Design Notes).

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
  - Removes RPEF `ConstraintModExtension` from backstories and the `VV_Vivi` xenotype, and strips the `VV_Vivi` race requirement from the shared `VV_RaceConstraint` and `VV_HeartAttackBlockRaceConstraint` defs (unlocks Vivi weapons and consumables; royals keep the heart-attack block). RPEF enforces constraints on equipping and in a `Pawn_HealthTracker.AddHediff` prefix, so a race-locked hediff can't be added to Human pawns at all.
  - Adds `Human` to the `VV_InstallGliderWing` recipe users
  - Adds vanilla-compatible genes to the `VV_Vivi` xenotype (speed, temp, pain, melee penalty, body type, plus the three custom genes)
  - Removes Vivi-specific hair defs, head type defs, and the head type constraint from `VV_PawnConstraint`
  - Adds backstory filters to `VV_ViviFederation` faction and vanilla hair tags to `VV_ViviCulture`
  - Swaps the cream recipe's constraint to `VVV_CreamMakerConstraint`
  - Adds `Human` to the `VV_ApplyArchiteGermlineImprint` recipe users (its worker already limits it to royal Vivis)

- **`ArchitectMenuPatches.xml`** — Moves all Vivi buildings/furniture/floors into standard vanilla architect categories and removes the custom `VV_Bulidings` designation category. Also adds `VVRace.Designator_FortifyHoneycombWall` to the Orders and Structure menus.

- **`ThinkTreePatch.xml`** — Injects `JobGiver_VVVLayViviEgg` and `VVRace.JobGiver_TryTeleportEverflower` into `MainColonistBehaviorCore` before `JobGiver_Work` (VVRace only adds the latter under a VV_Vivi race-only node). Both return priority 0 unless applicable, so the priority sorter skips them for all other pawns.

### Language (`Languages/English/`)

RimWorld only loads `Languages/<LanguageName>/…`, and DefInjected subfolders must be named after the def type (`GeneDef`, not `GenesDef`).

- `DefInjected/GeneDef/Language_Genes.xml` — Overrides the description of `VV_ViviMetabolism` (a gene from the base Vivi mod)
- `Keyed/KeyedLanguage.xml` — Letter strings for the genetic-instability event (`VVV_GeneticUnstableLabel`, `VVV_GeneticUnstableLetter`)

## Key Design Notes

- **CompVivi is on the Human ThingDef**, so VVRace's `ViviUtility.IsVivi()` (which only checks for CompVivi) is true for *every* human. `HarmonyPatches.IsTrueVivi()` is the real check (VV_Vivi race or `Gene_ViviPhysiology`). `RedirectIsViviCallSites` rewrites every VVRace call to `IsVivi()` — found by scanning VVRace's IL, plus the call VVRace's transpiler injects into `GeneUtility.AddedAndImplantedPartsWithXenogenesCount` — to `IsTrueVivi()`. `IsVivi()` itself is not patched because the JIT (Mono on 1.6, CoreCLR on 1.7) inlines such small methods. Anything VVRace reaches via `GetCompVivi()` instead must be gated separately.
- **Harmony patches** — initialized via `[StaticConstructorOnStartup]` in `HarmonyPatches.cs`. Notable patches:
  - `Patch_CompVivi_RefreshHairColor` / `Patch_CompVivi_Notify_AdultLifeStageStart` / `Patch_HediffComp_GrowthBoost_CompShouldRemove` (prefixes) — skip VVRace's hair-colour lerp and adult body-type/apparel reset, and drop the Vivi-food learning boost, for pawns that aren't `IsTrueVivi`.
  - `Patch_CompVivi_MetabolismFoodConsumptionFactor` (prefix) — CompVivi raises MaxNutrition from total gene metabolism; limit that to `IsTrueVivi` pawns.
  - `Patch_LifeStageWorker_Notify_LifeStageStarted` (postfix) — on adult transition, calls `Gene_ViviPhysiology.OnBecameAdult()` which triggers `SetRoyal()`.
  - `Patch_RestUtility_CanUseBedEver` / `Patch_CompAssignableToPawn_Bed_CanAssignTo` (postfixes) — the Vivi hatchery is a crib (`bed_maxBodySize` 0.3) that fits VV_Vivi-race children but not Human ones; `FitsViviHatchery` sizes true Vivis at the VV_Vivi race's base body size instead.
  - `Patch_PawnGenerator_GenerateGenes` (postfix) — mirrors VVRace's random xenogene for non-player (and, per its setting, starting) Vivis, which VVRace only applies when the pawnkind's race is VV_Vivi. Re-diff against `ViviRacePatch.PawnGenerator_GenerateGenes_Postfix` after Vivi updates.
- **`Gene_ViviPhysiology` mirrors `CompViviEggLayer`** (which only exists on the VV_Vivi race): `ProduceEgg` (including gene-inheritance generations and the archite germline imprint), `EggProgressPerDay` and `ApplyGeneticInstability` copy VVRace's egg-layer / `HediffGiver_GeneticUnstablity` logic. Re-diff them against VVRace after every Vivi update.
- Hatching is left to VVRace's own `CompViviHatcher.Hatch()`: it generates `VV_PlayerVivi` (race now Human) with the forced `VV_Vivi` xenotype, which is inheritable and already includes `VVV_ViviPhysiology` as an endogene. Don't force the gene again as a xenogene — that duplicates it.
- **Commented-out sections** in `ViviPatches.xml` represent in-progress work: apparel removal and pawnkind apparel tag changes are disabled. The `differences vivi human race.txt` file tracks remaining stat differences to address via genes.
- The mod is acknowledged as incomplete in `About/About.xml` — royal Vivi xenotype, female-only constraint, and genetic instability hediff are listed as todos in the patch file comments.
- `VVV_CosmeticWings` grants `MaxFlightTime` and `FlightCooldown` stat offsets but the gene description says flight is cosmetic only — this stat offset enables the VVRace flight system on human pawns.
