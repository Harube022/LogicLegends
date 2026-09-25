using UnityEngine;

public static class StudyLibraryCatalog
{
    public static StudyBookDefinition[] GetShelf(string period)
    {
        switch (period)
        {
            case "PRELIM": return new[]
            {
                Load("propositional-logic", "PROPOSITIONAL LOGIC", "Propositions\nLogical connectives", "Prelim Week 1 — Propositional Logic"),
                Load("truth-tables", "TRUTH TABLES", "Truth values\nCompound propositions", "Prelim Week 2 — Truth Tables"),
                Load("rules-of-inference", "RULES OF INFERENCE", "Valid arguments\nInference and equivalence rules", "Prelim Weeks 3–4 — Rules of Inference"),
            };
            case "MIDTERMS": return new[]
            {
                Load("formal-informal-proofs", "FORMAL AND INFORMAL PROOFS", "Proof structure\nRigor and explanation", "Midterm Week 6 — Formal and Informal Proofs"),
                Load("direct-indirect-proofs", "DIRECT AND INDIRECT PROOFS", "Deductive reasoning\nContradiction and contrapositive", "Midterm Week 7 — Direct and Indirect Proofs"),
            };
            case "PRE-FINALS": return new[]
            {
                Load("mathematical-induction", "MATHEMATICAL INDUCTION", "Base case\nHypothesis and induction step", "Midterm Weeks 8–9 — Proof by Induction"),
                Load("sets", "SETS", "Construction and types\nOperations, subsets, complements", "Pre-Final Weeks 11–12 — Sets"),
                Load("functions-relations", "FUNCTIONS AND RELATIONS", "Ordered pairs and mappings\nFunction types and representations", "Pre-Final Week 13 — Functions and Relations"),
            };
            case "FINALS": return new[]
            {
                Load("graphs", "GRAPHS", "Vertices and edges\nTypes, degree, paths, and cycles", "Final Week 16 — Graphs"),
                Load("trees", "TREES", "Tree terminology and properties\nTree types and spanning trees", "Final Weeks 17–18 — Trees"),
            };
            default: return new StudyBookDefinition[0];
        }
    }

    private static StudyBookDefinition Load(string id, string title, string preview, string source)
    {
        var book = new StudyBookDefinition(title, preview, string.Empty, source);
        book.topicId = id;
        TextAsset data = Resources.Load<TextAsset>("StudyLibrary/" + id);
        if (data == null)
        {
            Debug.LogError("Missing study lesson: " + id);
            return book;
        }
        book.lessonPages = JsonUtility.FromJson<StudyLesson>(data.text).pages;
        return book;
    }
}
