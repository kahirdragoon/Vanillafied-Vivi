using RimWorld;
using Verse;

namespace VanillafiedVivi
{
    [DefOf]
    public static class VVVDefOf
    {
        // Jobs
        public static JobDef VVV_LayViviEgg;

        // Hediffs (VVRace)
        public static HediffDef VV_RoyalVivi;
        public static HediffDef VV_GeneticUnstability;

        // Hediffs (vanilla — xenogerm side effects)
        public static HediffDef XenogerminationComa;
        public static HediffDef XenogermLossShock;
        public static HediffDef XenogermReplicating;

        // Genes (vanilla)
        public static GeneDef Body_Standard;

        // Genes (our mod)
        public static GeneDef VVV_CosmeticWings;
        public static GeneDef VVV_HiveMind;

        // Needs (VVRace)
        public static NeedDef VV_RoyalJelly;

        // Thoughts (vanilla — shortened grief for VVV_HiveMind carriers)
        public static ThoughtDef MyDaughterDied;
        public static ThoughtDef MyDaughterLost;
        public static ThoughtDef PawnWithGoodOpinionDied;
        public static ThoughtDef PawnWithGoodOpinionLost;

        static VVVDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(VVVDefOf)); }
    }
}
