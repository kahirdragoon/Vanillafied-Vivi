using HarmonyLib;
using RPEF;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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

        static HarmonyPatches()
        {
            var harmony = new Harmony("kahirDragoon.vanillafiedVivi");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Private method — must patch by name
            harmony.Patch(
                AccessTools.Method(typeof(CompViviHatcher), "Hatch"),
                prefix: new HarmonyMethod(typeof(Patch_CompViviHatcher_Hatch), nameof(Patch_CompViviHatcher_Hatch.Prefix)));
        }
    }

    // Skip Vivi-specific hair colour lerp for non-Vivi Human pawns that now carry CompVivi.
    [HarmonyPatch(typeof(CompVivi), "RefreshHairColor")]
    public static class Patch_CompVivi_RefreshHairColor
    {
        public static bool Prefix(CompVivi __instance)
        {
            var pawn = (Pawn)__instance.parent;
            return pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().Any() == true;
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
    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.Notify_JobStarted))]
    public static class Patch_Pawn_FlightTracker_Notify_JobStarted
    {
        private static readonly MethodInfo StartFlyingInternal =
            AccessTools.Method(typeof(Pawn_FlightTracker), "StartFlyingInternal");

        public static bool Prefix(Pawn_FlightTracker __instance, Job job, Pawn ___pawn, int ___flightCooldownTicks)
        {
            var ext = HarmonyPatches.GetWingsFlyExtension(___pawn);
            if (ext == null || !ext.alwaysFlyIfDrafted || !___pawn.Drafted)
                return true;

            if (!__instance.Flying && ___flightCooldownTicks <= 0)
                StartFlyingInternal.Invoke(__instance, null);

            job.flying = __instance.Flying;
            return false;
        }
    }

    public static class Patch_CompViviHatcher_Hatch
    {
        public static bool Prefix(CompViviHatcher __instance)
        {
            var hatcheeParent = __instance.hatcheeParent;
            if (hatcheeParent?.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrFallback() == null)
                return true; // not our egg — let original handle it

            // Egg was laid by a normalized Vivi — spawn Human pawn
            try
            {
                var hatchery = __instance.parent.ParentHolder as ViviEggHatchery;
                if (hatchery == null) return false;

                var faction = hatchery.Faction;
                var pawnKindDef = (faction?.IsPlayer ?? false) ? VVPawnKindDefOf.VV_PlayerVivi : null;
                if (pawnKindDef == null) return false;

                try
                {
                    Rand.PushState(__instance.randomSeed);

                    var randomGeneCount = Mathf.FloorToInt(__instance.Props.geneCountCurve.Evaluate(Rand.Range(0, 10000)));
                    var xenogenes = ViviUtility.SelectRandomGeneForVivi(randomGeneCount, __instance.parentXenogenes);

                    // Ensure the hatched pawn carries the physiology gene
                    var physiologyDef = DefDatabase<GeneDef>.GetNamed("VVV_ViviPhysiology");
                    if (!xenogenes.Contains(physiologyDef))
                        xenogenes.Add(physiologyDef);

                    var request = new PawnGenerationRequest(
                        pawnKindDef,
                        faction: faction,
                        allowDowned: true,
                        developmentalStages: DevelopmentalStage.Newborn,
                        forcedXenotype: VVXenotypeDefOf.VV_Vivi,
                        forcedXenogenes: xenogenes);

                    if (hatcheeParent.Name is NameTriple nameTriple)
                        request.SetFixedLastName(nameTriple.Last);

                    var pawn = PawnGenerator.GeneratePawn(request);
                    if (GenSpawn.Spawn(pawn, hatchery.Position, hatchery.Map) != null)
                    {
                        if (pawn.IsColonist)
                        {
                            var sb = new StringBuilder();
                            sb.Append(
                                LocalizeString_Letter.VV_Letter_ViviEggHatched.Translate(hatcheeParent.Named("PARENT")));

                            if (pawn.genes?.Xenogenes.Count > 0)
                            {
                                sb.AppendInNewLine(LocalizeString_Letter.VV_Letter_ViviEggHatchedWithGene.Translate());
                                sb.AppendInNewLine(string.Join(", ", pawn.genes.Xenogenes.Select(g => g.def.LabelCap)));
                            }

                            Find.LetterStack.ReceiveLetter(
                                LocalizeString_Letter.VV_Letter_ViviEggHatchedLabel.Translate(),
                                sb.ToString(),
                                LetterDefOf.PositiveEvent,
                                pawn);
                        }

                        if (pawn.playerSettings != null && hatcheeParent.playerSettings != null
                            && hatcheeParent.Faction == faction)
                        {
                            pawn.playerSettings.AreaRestrictionInPawnCurrentMap =
                                hatcheeParent.playerSettings.AreaRestrictionInPawnCurrentMap;
                        }

                        if (pawn.RaceProps.IsFlesh)
                            pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, hatcheeParent);

                        if (__instance.parent.Spawned)
                            FilthMaker.TryMakeFilth(hatchery.Position, hatchery.Map, ThingDefOf.Filth_AmnioticFluid, count: 3);
                    }
                    else
                    {
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
                    }
                }
                finally
                {
                    Rand.PopState();
                }
            }
            finally
            {
                __instance.parent.Destroy();
            }

            return false; // skip original Hatch()
        }
    }
}
