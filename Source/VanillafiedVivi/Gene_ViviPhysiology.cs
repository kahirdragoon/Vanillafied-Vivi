using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VVRace;

namespace VanillafiedVivi
{
    public class Gene_ViviPhysiology : Gene
    {
        private const int GeneticInstabilityCheckInterval = 6000;
        private const float EggProgressDays = 15f;

        // --- Egg laying ---

        public float eggProgress;
        public bool canLayEgg = true;

        public bool CanLayEgg => Active && eggProgress >= 1f && canLayEgg;

        public float EggProgressPerDay
        {
            get
            {
                var speed = PawnUtility.BodyResourceGrowthSpeed(pawn)
                    * pawn.health.capacities.GetLevel(PawnCapacityDefOf.BloodPumping);
                return Mathf.Clamp01(speed / EggProgressDays);
            }
        }

        public override void PostAdd()
        {
            base.PostAdd();
            if (pawn.ageTracker?.CurLifeStage?.developmentalStage.Adult() != true)
                return;
            
            if (pawn.kindDef is PawnKindDef_Vivi kindDef && kindDef.isRoyal)
                MakeRoyal();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eggProgress, "eggProgress");
            Scribe_Values.Look(ref canLayEgg, "canLayEgg", defaultValue: true, forceSave: true);
        }

        public override void Tick()
        {
            base.Tick();

            if (pawn.IsHashIntervalTick(2000) && Active && pawn.health.hediffSet.HasHediff(VVVDefOf.VV_RoyalVivi))
            {
                if (eggProgress < 1f)
                    eggProgress = Mathf.Clamp01(eggProgress + EggProgressPerDay / 60000f * 2000f);
            }

            ApplyGeneticInstability();
        }

        private void ApplyGeneticInstability()
        {
            if (!pawn.IsNestedHashIntervalTick(60, GeneticInstabilityCheckInterval))
                return;
            if (!Rand.Chance(0.02f))
                return;

            if (pawn.health.hediffSet.HasHediff(VVVDefOf.XenogerminationComa) ||
                pawn.health.hediffSet.HasHediff(VVVDefOf.XenogermLossShock) ||
                pawn.health.hediffSet.HasHediff(VVVDefOf.XenogermReplicating))
            {
                var part = pawn.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Torso).FirstOrFallback();
                if (part != null && !pawn.health.hediffSet.HasHediff(VVVDefOf.VV_GeneticUnstability))
                {
                    pawn.health.AddHediff(HediffMaker.MakeHediff(VVVDefOf.VV_GeneticUnstability, pawn, part));
                    Find.LetterStack.ReceiveLetter(
                        "VVV_GeneticUnstableLabel".Translate(),
                        "VVV_GeneticUnstableLetter".Translate(pawn.Named("PAWN")).AdjustedFor(pawn),
                        LetterDefOf.NegativeEvent, pawn);
                }
            }
        }

        public Thing ProduceEgg(bool force = false)
        {
            if (!CanLayEgg && !force) return null;
            try
            {
                var egg = ThingMaker.MakeThing(VVThingDefOf.VV_ViviEgg);
                var hatcher = egg.TryGetComp<CompViviHatcher>();
                hatcher.hatcheeParent = pawn;
                hatcher.parentXenogenes = pawn.genes.Xenogenes
                    .Where(g => g.def.biostatArc == 0)
                    .Select(g => g.def)
                    .Where(d =>
                        d.endogeneCategory != EndogeneCategory.BodyType &&
                        d.endogeneCategory != EndogeneCategory.Melanin &&
                        d.endogeneCategory != EndogeneCategory.HairColor &&
                        d.endogeneCategory != EndogeneCategory.Head &&
                        d.endogeneCategory != EndogeneCategory.Jaw &&
                        d.defName != "VV_ViviPhysiology")
                    .ToList();
                return egg;
            }
            finally
            {
                eggProgress = 0f;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (!pawn.IsColonistPlayerControlled || !pawn.health.hediffSet.HasHediff(VVVDefOf.VV_RoyalVivi))
                yield break;

            yield return new Gizmo_EggProgress(this);

            yield return new Command_Toggle
            {
                icon = HarmonyPatches.ToggleLayEggTex,
                defaultLabel = "VV_Command_ToggleLayEgg".Translate(),
                defaultDesc = "VV_Command_ToggleLayEggDesc".Translate(),
                isActive = () => canLayEgg,
                toggleAction = () => canLayEgg = !canLayEgg
            };

            if (DebugSettings.godMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Add Egg Progress 10%",
                    action = () => eggProgress = Mathf.Clamp01(eggProgress + 0.1f)
                };
            }
        }

        public void OnBecameAdult()
        {
            if (pawn.needs?.TryGetNeed(VVVDefOf.VV_RoyalJelly) is not Need_RoyalJelly need || !need.ShouldBeRoyalIfMature)
                return;

            MakeRoyal();
        }

        public void MakeRoyal()
        {
            pawn.genes.AddGene(VVVDefOf.Body_Standard, xenogene: true);
            pawn.GetCompVivi()?.SetRoyal();
        }
    }
}
