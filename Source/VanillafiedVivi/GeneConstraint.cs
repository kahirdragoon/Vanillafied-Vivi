using RimWorld;
using RPEF;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VanillafiedVivi
{
    public class GeneConstraint : Constraint<GeneDef>
    {
        public HashSet<GeneDef> GeneDefs
        {
            get
            {
                if (_geneDefCache == null)
                {
                    _geneDefCache = [];

                    if (genes != null)
                    {
                        _geneDefCache.AddRange(genes);
                    }
                }

                return _geneDefCache;
            }
        }
        private HashSet<GeneDef> _geneDefCache;
        private List<GeneDef> genes;

        protected override bool Match(GeneDef def) => def != null && GeneDefs.Contains(def);

        protected override bool Match(Pawn pawn)
        {
            if (pawn?.genes == null)
                return false;
            foreach (var gene in GeneDefs)
            {
                if (pawn.genes.HasActiveGene(gene))
                    return true;
            }
            return false;
        }
    }
}
