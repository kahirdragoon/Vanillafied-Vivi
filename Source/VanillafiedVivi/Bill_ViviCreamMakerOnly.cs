using RimWorld;
using Verse;
using VVRace;

namespace VanillafiedVivi
{
    public class Bill_ViviCreamMakerOnly : Bill_ProductionWithFoodDrain
    {
        private static GeneDef? _gene;
        private static GeneDef? Gene => _gene ??= DefDatabase<GeneDef>.GetNamed("VVV_CreamMaker");

        public Bill_ViviCreamMakerOnly()
        {
        }

        public Bill_ViviCreamMakerOnly(RecipeDef recipe, Precept_ThingStyle precept = null)
            : base(recipe, precept)
        {
        }

        public override bool PawnAllowedToStartAnew(Pawn pawn)
        {
            if (!base.PawnAllowedToStartAnew(pawn))
                return false;

            return Gene != null && pawn.genes?.HasActiveGene(Gene) == true;
        }
    }
}
