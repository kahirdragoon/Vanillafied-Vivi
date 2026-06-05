using RimWorld;
using UnityEngine;
using Verse;

namespace VanillafiedVivi
{
    public class Gizmo_EggProgress : Gizmo
    {
        private const float GizmoWidth = 136f;
        private const int HeaderHeight = 20;
        private static readonly Color EmptyBlockColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color FilledBlockColor = Color.grey;

        private readonly Gene_ViviPhysiology _gene;

        public Gizmo_EggProgress(Gene_ViviPhysiology gene)
        {
            _gene = gene;
        }

        public override float GetWidth(float maxWidth) => GizmoWidth;

        public override bool Visible => Find.Selector.SelectedPawns.Count == 1;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Height);
            Widgets.DrawWindowBackground(rect);
            var inner = rect.ContractedBy(6f);

            var headerRect = new Rect(inner.x, inner.y, inner.width, HeaderHeight);
            using (new TextBlock(GameFont.Tiny))
                Widgets.Label(headerRect, "VV_Gizmo_ViviEggProgressHeader".Translate());

            var descRect = new Rect(inner.x, headerRect.yMax + 2f, inner.width, HeaderHeight);
            using (new TextBlock(GameFont.Tiny))
            {
                Widgets.Label(descRect, _gene.eggProgress.ToStringPercent("F1"));
                using (new TextBlock(TextAnchor.UpperRight))
                    Widgets.Label(descRect, $"(+{_gene.EggProgressPerDay.ToStringPercent()}/day)");
            }

            var barBackRect = new Rect(
                inner.x,
                descRect.yMax + 2f,
                inner.width,
                inner.height - headerRect.height - descRect.height - 4f);
            Widgets.DrawBoxSolid(barBackRect, EmptyBlockColor);

            var barFrontRect = barBackRect.ContractedBy(3f);
            barFrontRect.width *= _gene.eggProgress;
            Widgets.DrawBoxSolid(barFrontRect, FilledBlockColor);

            TooltipHandler.TipRegion(rect, "VV_Gizmo_ViviEggProgressTooltip".Translate());
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
