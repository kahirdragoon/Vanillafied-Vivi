using HarmonyLib;
using RPEF;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using VVRace;

namespace VanillafiedVivi
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        public static readonly Texture2D ToggleLayEggTex = ContentFinder<Texture2D>.Get("UI/Commands/VV_LayEgg");

        // Returns the HumanlikeFlyExtension from the VVV_CosmeticWings gene def if the pawn
        // has that gene active — lets us read fly animation data from the gene rather than the race.
        internal static HumanlikeFlyExtension GetWingsFlyExtension(Pawn pawn)
        {
            if (pawn?.genes == null) return null;
            if (!pawn.genes.GenesListForReading.Any(g => g.def == VVVDefOf.VVV_CosmeticWings && g.Active))
                return null;
            return VVVDefOf.VVV_CosmeticWings.GetModExtension<HumanlikeFlyExtension>();
        }

        private static readonly MethodInfo ViviUtilityIsVivi = AccessTools.Method(typeof(ViviUtility), nameof(ViviUtility.IsVivi));
        private static readonly MethodInfo IsTrueViviMethod = AccessTools.Method(typeof(HarmonyPatches), nameof(IsTrueVivi));

        // VVRace's IsVivi() only checks for CompVivi, which is now on the Human ThingDef, so it is true for
        // every human. A real Vivi is the original VV_Vivi race or a pawn carrying VVV_ViviPhysiology.
        // CompVivi is still required because VVRace callers dereference it after an IsVivi() check.
        public static bool IsTrueVivi(Pawn pawn)
        {
            if (pawn == null || !pawn.IsVivi())
                return false;
            if (pawn.def == VVThingDefOf.VV_Vivi)
                return true;
            return pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().Any() == true;
        }

        // The Vivi hatchery is a crib sized for the VV_Vivi race: its children (0.8 base body size × 0.35)
        // fit under its bed_maxBodySize of 0.3, Human ones (1.0 × 0.35) don't. Size Human Vivis as if they
        // were VV_Vivi, so the same life stages fit. Body size is the only bed check this has to replace.
        public static bool FitsViviHatchery(Pawn pawn, ThingDef bedDef)
        {
            if (bedDef != VVThingDefOf.VV_ViviHatchery || pawn.def == VVThingDefOf.VV_Vivi || !IsTrueVivi(pawn))
                return false;
            var viviBodySize = pawn.ageTracker.CurLifeStage.bodySizeFactor * VVThingDefOf.VV_Vivi.race.baseBodySize;
            return viviBodySize <= bedDef.building.bed_maxBodySize;
        }

        static HarmonyPatches()
        {
            var harmony = new Harmony("kahirDragoon.vanillafiedVivi");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // VVRace gates VV_RoyalJelly on IsVivi() (CompVivi presence), but CompVivi is now on
            // the base Human ThingDef for the Everflower system, so every Human pawn passed that
            // check. We gate the need on the VVV_ViviPhysiology gene instead (via NeedDef's
            // onlyIfCausedByGene, see ViviPatches.xml), so any race carrying the gene qualifies —
            // remove VVRace's postfix so it can't override that with a race-specific false.
            var viviRacePatchType = AccessTools.TypeByName("VVRace.HarmonyPatches.ViviRacePatch");
            var shouldHaveNeedPostfix = AccessTools.Method(viviRacePatchType, "Pawn_NeedsTracker_ShouldHaveNeed_Postfix");
            if (shouldHaveNeedPostfix != null)
            {
                harmony.Unpatch(AccessTools.Method(typeof(Pawn_NeedsTracker), "ShouldHaveNeed"), shouldHaveNeedPostfix);
            }

            RedirectIsViviCallSites(harmony);
        }

        // IsVivi() is too small to patch reliably (Mono inlines it into its callers), so rewrite the call
        // sites instead: every VVRace method that references it — lambdas and VVRace's own Harmony patches
        // included — plus the vanilla method VVRace's transpiler injects an IsVivi() call into.
        private static void RedirectIsViviCallSites(Harmony harmony)
        {
            var targets = new List<MethodBase>();
            var token = System.BitConverter.GetBytes(ViviUtilityIsVivi.MetadataToken);
            foreach (var type in AccessTools.GetTypesFromAssembly(ViviUtilityIsVivi.DeclaringType.Assembly))
            {
                if (type.ContainsGenericParameters) continue;
                foreach (var method in type.GetMethods(AccessTools.allDeclared))
                {
                    if (method == ViviUtilityIsVivi || method.IsAbstract || method.ContainsGenericParameters) continue;
                    var il = method.GetMethodBody()?.GetILAsByteArray();
                    if (il != null && ContainsBytes(il, token))
                        targets.Add(method);
                }
            }

            if (targets.Count == 0)
                Log.Warning("[VanillafiedVivi] Found no ViviUtility.IsVivi call sites in VVRace — non-Vivi humans may be treated as Vivis.");

            // VVRace transpiles an IsVivi() skip into this (Vivi xenogenes don't count as body modifications).
            targets.Add(AccessTools.Method(typeof(GeneUtility), nameof(GeneUtility.AddedAndImplantedPartsWithXenogenesCount)));

            // Priority.Last so ours runs after VVRace's transpiler has inserted its call.
            var transpiler = new HarmonyMethod(typeof(HarmonyPatches), nameof(RedirectIsVivi)) { priority = Priority.Last };
            foreach (var method in targets)
                harmony.Patch(method, transpiler: transpiler);
        }

        public static IEnumerable<CodeInstruction> RedirectIsVivi(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.operand is MethodInfo method && method == ViviUtilityIsVivi)
                    instruction.operand = IsTrueViviMethod;
                yield return instruction;
            }
        }

        private static bool ContainsBytes(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }
    }

    // Skip Vivi-specific hair colour lerp for non-Vivi Human pawns that now carry CompVivi.
    [HarmonyPatch(typeof(CompVivi), "RefreshHairColor")]
    public static class Patch_CompVivi_RefreshHairColor
    {
        public static bool Prefix(CompVivi __instance) => HarmonyPatches.IsTrueVivi((Pawn)__instance.parent);
    }

    // VVRace calls this on every pawn with CompVivi reaching adulthood; for non-royals it forces the Thin
    // body type and drops apparel. Without this, every human child turning 13 becomes Thin.
    [HarmonyPatch(typeof(CompVivi), nameof(CompVivi.Notify_AdultLifeStageStart))]
    public static class Patch_CompVivi_Notify_AdultLifeStageStart
    {
        public static bool Prefix(CompVivi __instance) => HarmonyPatches.IsTrueVivi((Pawn)__instance.parent);
    }

    // The Vivi-food learning boost stays on children with CompVivi and is removed from everyone else.
    // CompVivi is on every Human, so without this any human child who eats Vivi food keeps it.
    [HarmonyPatch(typeof(HediffComp_GrowthBoost), nameof(HediffComp_GrowthBoost.CompShouldRemove), MethodType.Getter)]
    public static class Patch_HediffComp_GrowthBoost_CompShouldRemove
    {
        public static bool Prefix(HediffComp_GrowthBoost __instance, ref bool __result)
        {
            if (HarmonyPatches.IsTrueVivi(__instance.Pawn))
                return true;
            __result = true;
            return false;
        }
    }

    // CompVivi raises MaxNutrition from the pawn's total gene metabolism (GetStatFactor and its stat
    // explanation both read this). Without the gate every human with negative-metabolism genes gets it.
    [HarmonyPatch(typeof(CompVivi), nameof(CompVivi.MetabolismFoodConsumptionFactor), MethodType.Getter)]
    public static class Patch_CompVivi_MetabolismFoodConsumptionFactor
    {
        public static bool Prefix(CompVivi __instance, ref float __result)
        {
            if (HarmonyPatches.IsTrueVivi((Pawn)__instance.parent))
                return true;
            __result = 1f;
            return false;
        }
    }

    // Child Vivis can sleep in an empty hatchery, but a Human Vivi child is too big for it (see FitsViviHatchery).
    [HarmonyPatch(typeof(RestUtility), nameof(RestUtility.CanUseBedEver))]
    public static class Patch_RestUtility_CanUseBedEver
    {
        public static void Postfix(Pawn p, ThingDef bedDef, ref bool __result)
        {
            if (!__result && HarmonyPatches.FitsViviHatchery(p, bedDef))
                __result = true;
        }
    }

    // Same size check for assigning the hatchery to a child. The rest mirrors the vanilla slave/colonist rules,
    // which vanilla skips once the size check fails.
    [HarmonyPatch(typeof(CompAssignableToPawn_Bed), nameof(CompAssignableToPawn_Bed.CanAssignTo))]
    public static class Patch_CompAssignableToPawn_Bed_CanAssignTo
    {
        public static void Postfix(CompAssignableToPawn_Bed __instance, Pawn pawn, ref AcceptanceReport __result)
        {
            if (__result.Accepted || !HarmonyPatches.FitsViviHatchery(pawn, __instance.parent.def))
                return;

            var bed = (Building_Bed)__instance.parent;
            if (!pawn.DevelopmentalStage.Baby())
            {
                if (bed.ForSlaves && !pawn.IsSlave)
                {
                    __result = "CannotAssignBedToColonist".Translate();
                    return;
                }
                if (bed.ForColonists && pawn.IsSlave)
                {
                    __result = "CannotAssignBedToSlave".Translate();
                    return;
                }
            }
            __result = AcceptanceReport.WasAccepted;
        }
    }
    [HarmonyPatch(typeof(LifeStageWorker), nameof(LifeStageWorker.Notify_LifeStageStarted))]
    public static class Patch_LifeStageWorker_Notify_LifeStageStarted
    {
        public static void Postfix(Pawn pawn, LifeStageDef previousLifeStage)
        {
            if (pawn.genes == null) return;
            if (!pawn.ageTracker.CurLifeStage.developmentalStage.Adult()) return;
            if (previousLifeStage?.developmentalStage.Adult() == true) return;

            pawn.genes.GenesListForReading
                .OfType<Gene_ViviPhysiology>()
                .FirstOrDefault()
                ?.OnBecameAdult();
        }
    }

    // VVRace gives non-player Vivis (and starting Vivis, if its mod setting is on) a random xenogene, but only
    // when the pawnkind's race is VV_Vivi, which no Vivi pawnkind is any more. Mirrors
    // ViviRacePatch.PawnGenerator_GenerateGenes_Postfix for the other races; VVRace's own covers VV_Vivi.
    [HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]
    public static class Patch_PawnGenerator_GenerateGenes
    {
        public static void Postfix(Pawn pawn, XenotypeDef xenotype, PawnGenerationRequest request)
        {
            var kindDef = request.KindDef;
            var factionDef = kindDef?.defaultFactionDef;
            if (xenotype != VVXenotypeDefOf.VV_Vivi || factionDef == null || kindDef.race == VVThingDefOf.VV_Vivi
                || factionDef.allowedCultures?.Contains(VVCultureDefOf.VV_ViviCulture) != true
                || pawn.genes.Xenogenes.Count != 0)
                return;

            var startingVivi = request.Context == PawnGenerationContext.PlayerStarter
                && VVRaceMod.Settings?.randomGenesForStartingVivi == true;
            if (factionDef.isPlayer && !startingVivi)
                return;

            // Same count as VVRace: the int overload excludes the max, so this is always one gene.
            foreach (var gene in ViviUtility.SelectRandomGeneForVivi(Rand.Range(1, 2)))
                pawn.genes.AddGene(gene, xenogene: true);
        }
    }

    // VV_Bulidings must stay in the DefDatabase (VVRace [DefOf] binds to it), but we don't
    // want an empty tab cluttering the architect menu. Strip it from the cached panel list.
    [HarmonyPatch(typeof(MainTabWindow_Architect), "CacheDesPanels")]
    public static class Patch_MainTabWindow_Architect_CacheDesPanels
    {
        private static readonly FieldInfo DesPanelsCachedField =
            AccessTools.Field(typeof(MainTabWindow_Architect), "desPanelsCached");

        public static void Postfix(MainTabWindow_Architect __instance)
        {
            var panels = (List<ArchitectCategoryTab>)DesPanelsCachedField.GetValue(__instance);
            panels.RemoveAll(tab => tab.def.defName == "VV_Bulidings");
        }
    }

    [HarmonyPatch(typeof(SymbolResolver_ViviEggSpawn), "Resolve")]
    public static class Patch_SymbolResolver_ViviEggSpawn_Resolve
    {
        public static bool Prefix(ResolveParams resolveParams)
        {
            var map = BaseGen.globalSettings.map;
            var hatcheries = map.listerBuildings
                .AllBuildingsNonColonistOfDef(VVThingDefOf.VV_ViviHatchery)
                .Cast<ViviEggHatchery>().ToList();
            if (!hatcheries.Any()) return false;

            var royalVivis = map.mapPawns.AllPawns
                .Where(p => p.Faction == resolveParams.faction && p.IsRoyalVivi())
                .ToList();
            if (!royalVivis.Any()) return false;

            var vivi = royalVivis.RandomElement();
            foreach (var hatchery in hatcheries)
            {
                if (!Rand.Bool) continue;

                Thing egg;
                var eggLayer = vivi.GetCompViviEggLayer();
                if (eggLayer != null)
                {
                    egg = eggLayer.ProduceEgg(force: true);
                }
                else
                {
                    var physGene = vivi.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrDefault();
                    egg = physGene?.ProduceEgg(force: true);
                }

                if (egg is ViviEgg viviEgg)
                {
                    viviEgg.CompViviHatcher.hatchProgress = Rand.Range(0.2f, 0.7f);
                    hatchery.ViviEgg = viviEgg;
                }
            }

            return false;
        }
    }

    // GetBestFlyAnimation is static: takes (Pawn pawn, Rot4? facingOverride).
    // RPEF's transpiler intercepts the humanlike early-return and calls pawn.def.GetModExtension —
    // which is null for Human. Our postfix catches the null and reads the extension from the gene def instead.
    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.GetBestFlyAnimation))]
    public static class Patch_Pawn_FlightTracker_GetBestFlyAnimation
    {
        public static void Postfix(Pawn pawn, Rot4? facingOverride, ref AnimationDef __result)
        {
            if (__result != null) return;
            var ext = HarmonyPatches.GetWingsFlyExtension(pawn);
            if (ext == null) return;
            var data = ext.GetAnimationData(pawn.ageTracker.CurLifeStage);
            if (data == null) return;
            var rot = facingOverride ?? pawn.Rotation;
            __result = rot == Rot4.South ? data.animationSouth
                     : rot == Rot4.North ? data.animationNorth
                     : rot == Rot4.East  ? data.animationEast
                     : data.animationWest;
        }
    }

    // Vanilla Notify_JobStarted calls ForceLand() for any job without tryStartFlying=true,
    // which would ground drafted wing-gene Vivis on every action. The prefix intercepts first:
    // for drafted Vivis with alwaysFlyIfDrafted, we manage flight ourselves and skip vanilla.
    // Pawns that can't fly at all right now (vacuum biome, mutants) are left to vanilla.
    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.Notify_JobStarted))]
    public static class Patch_Pawn_FlightTracker_Notify_JobStarted
    {
        public static bool Prefix(Pawn_FlightTracker __instance, Job job, Pawn ___pawn)
        {
            var ext = HarmonyPatches.GetWingsFlyExtension(___pawn);
            if (ext == null || !ext.alwaysFlyIfDrafted || !___pawn.Drafted || !__instance.CanEverFly)
                return true;

            __instance.StartFlying(); // only takes off if grounded and off cooldown (CanFlyNow)
            job.flying = __instance.Flying;
            return false;
        }
    }

    // VVV_HiveMind carriers still grieve a daughter or a close friend, but the hive's instincts
    // pull them back to work faster than a baseliner — shortens duration instead of nullifying.
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory), new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_MemoryThoughtHandler_TryGainMemory_HiveMindGrief
    {
        public static void Postfix(Thought_Memory newThought)
        {
            var pawn = newThought?.pawn;
            if (pawn?.genes == null) return;
            if (!pawn.genes.GenesListForReading.Any(g => g.def == VVVDefOf.VVV_HiveMind && g.Active)) return;

            int shortenedDays;
            if (newThought.def == VVVDefOf.MyDaughterDied || newThought.def == VVVDefOf.MyDaughterLost)
                shortenedDays = 15;
            else if (newThought.def == VVVDefOf.PawnWithGoodOpinionDied || newThought.def == VVVDefOf.PawnWithGoodOpinionLost)
                shortenedDays = 10;
            else
                return;

            newThought.durationTicksOverride = shortenedDays * 60000;
        }
    }
}
