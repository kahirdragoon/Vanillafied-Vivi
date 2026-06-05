using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using VVRace;

namespace VanillafiedVivi
{
    public class JobGiver_VVVLayViviEgg : ThinkNode_JobGiver
    {
        public override float GetPriority(Pawn pawn)
        {
            var gene = pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrFallback();
            return gene != null && gene.CanLayEgg ? 10f : 0f;
        }

        public override Job TryGiveJob(Pawn pawn)
        {
            var gene = pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrFallback();
            if (gene == null || !gene.CanLayEgg)
                return null;

            if (pawn.Faction != Faction.OfPlayerSilentFail)
                return null;

            var hatchery = GenClosest.ClosestThing_Regionwise_ReachablePrioritized(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(VVThingDefOf.VV_ViviHatchery),
                PathEndMode.OnCell,
                TraverseParms.For(pawn, Danger.Some),
                validator: thing =>
                {
                    if (!thing.Spawned || thing.IsForbidden(pawn) || !pawn.CanReserve(thing))
                        return false;
                    return thing is ViviEggHatchery h && h.CanLayHere;
                },
                priorityGetter: thing => -pawn.Position.DistanceToSquared(thing.Position),
                minRegions: 10);

            if (hatchery == null)
                return null;

            return JobMaker.MakeJob(VVVDefOf.VVV_LayViviEgg, hatchery);
        }
    }
}
