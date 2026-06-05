using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using VVRace;

namespace VanillafiedVivi
{
    public class JobDriver_VVVLayViviEgg : JobDriver
    {
        private const int LayingEggTicks = 500;
        private const TargetIndex HatcheryIdx = TargetIndex.A;

        private LocalTargetInfo Hatchery => job.GetTarget(HatcheryIdx);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Hatchery, job, errorOnFailed: errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(HatcheryIdx);
            this.FailOn(() =>
            {
                var gene = pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrFallback();
                return gene == null || !gene.CanLayEgg;
            });

            yield return Toils_Goto.GotoCell(HatcheryIdx, PathEndMode.OnCell);
            yield return Toils_General.Wait(LayingEggTicks);
            yield return Toils_General.Do(() =>
            {
                var gene = pawn.genes?.GenesListForReading.OfType<Gene_ViviPhysiology>().FirstOrFallback();
                if (gene == null || !gene.CanLayEgg)
                {
                    Log.Warning($"[VanillafiedVivi] {pawn} tried to lay egg but gene is missing or not ready");
                    return;
                }

                var egg = gene.ProduceEgg();
                if (egg == null)
                    return;

                var hatchery = Hatchery.Thing as ViviEggHatchery;
                if (hatchery == null || !hatchery.CanLayHere)
                    GenPlace.TryPlaceThing(egg, hatchery?.PositionHeld ?? pawn.Position, hatchery?.MapHeld ?? pawn.Map, ThingPlaceMode.Near);
                else
                    hatchery.ViviEgg = egg;
            });
        }
    }
}
