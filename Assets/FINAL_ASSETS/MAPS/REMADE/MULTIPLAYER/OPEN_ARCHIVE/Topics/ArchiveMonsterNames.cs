namespace LogicLegends.OpenArchive
{
    /// <summary>Official display names; existing serialized monster IDs remain unchanged.</summary>
    public static class ArchiveMonsterNames
    {
        public const string PrimalAxiom = "The Primal Axiom";
        public const string ProofDevourer = "The Proof Devourer";

        public static string ForPrimalAxiom(bool primalAxiom) => primalAxiom ? PrimalAxiom : ProofDevourer;
    }
}
