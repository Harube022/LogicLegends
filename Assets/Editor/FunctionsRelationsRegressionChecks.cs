using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FunctionsRelationsRegressionChecks
{
    [MenuItem("Logic Legends/Validate Functions and Relations Stage")]
    public static void Run()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MappingChallengeCatalog>("Assets/Resources/FunctionsRelationsChallenges.asset");
        Check(catalog != null && catalog.ChallengeCount == 5, "Five challenge groups are required.");
        int functionVariants = 0;
        int relationVariants = 0;
        int bijectiveVariants = 0;
        int surjectiveOnlyVariants = 0;
        for (int stage = 0; stage < catalog.ChallengeCount; stage++)
        {
            var group = catalog.ChallengeGroups[stage];
            Check(group != null && group.variants != null && group.variants.Count == 10,
                "Challenge " + (stage + 1) + " needs ten variants.");
            var signatures = new HashSet<string>();
            for (int variant = 0; variant < group.variants.Count; variant++)
            {
                var challenge = group.variants[variant];
                Check(challenge != null && challenge.domain.Length == 4 && challenge.codomain.Length == 4,
                    "Every variant needs four Domain and four Range diamonds.");
                for (int i = 0; i < 4; i++)
                    Check(challenge.domain[i] == (i + 1).ToString() && challenge.codomain[i] == ((char)('A' + i)).ToString(),
                        "Diamond labels must be 1–4 and A–D in physical order.");
                Check(MappingRuleEvaluator.HasValidEndpoints(challenge.initialPairs, challenge.domain, challenge.codomain),
                    "Invalid initial relation in challenge " + (stage + 1) + " variant " + (variant + 1));
                Check(MappingRuleEvaluator.HasValidEndpoints(challenge.requiredPairs, challenge.domain, challenge.codomain),
                    "Invalid required relation in challenge " + (stage + 1) + " variant " + (variant + 1));
                Check(challenge.questionKind == (stage < 3 ? MappingQuestionKind.BuildExactRelation :
                    stage == 3 ? MappingQuestionKind.ClassifyFunction : MappingQuestionKind.SelectProperties),
                    "Variant has the wrong question type for its stage.");
                var solution = stage < 3 ? challenge.requiredPairs : challenge.initialPairs;
                var sources = new HashSet<string>();
                var targets = new HashSet<string>();
                var signatureParts = new List<string>();
                foreach (var pair in solution)
                {
                    sources.Add(pair.domain);
                    targets.Add(pair.codomain);
                    signatureParts.Add(pair.domain + pair.codomain);
                }
                signatureParts.Sort(StringComparer.Ordinal);
                Check(signatures.Add(string.Join("/", signatureParts.ToArray())),
                    "Duplicate mapping in challenge " + (stage + 1));
                Check(targets.Count == 4 && sources.Contains("4"),
                    "Every solution must use all four Range diamonds and Domain 4.");
                if (stage != 3) Check(sources.Count == 4,
                    "These solutions must use all four Domain diamonds.");
                if (stage == 0) Check(challenge.initialPairs.Count == 0 && solution.Count == 4,
                    "Challenge 1 variants must each require four connections.");
                if (stage == 1) Check(challenge.initialPairs.Count == 0 && solution.Count == 5 &&
                    !MappingRuleEvaluator.IsFunction(solution, challenge.domain),
                    "Challenge 2 variants must each require five relation connections.");
                if (stage == 2)
                {
                    Check(challenge.initialPairs.Count == 1 && solution.Count == 4 &&
                        challenge.initialPairsLocked && challenge.requireFunction &&
                        challenge.requireInjective && challenge.requireSurjective &&
                        MappingRuleEvaluator.IsBijective(solution, challenge.domain, challenge.codomain),
                        "Challenge 3 variants need one fixed connection and three inferred bijection connections.");
                    Check(solution.Contains(challenge.initialPairs[0]), "Challenge 3 fixed connection is absent from its solution.");
                    var inferred = new List<MappingPair>(solution);
                    inferred.Remove(challenge.initialPairs[0]);
                    inferred.Sort((a, b) => string.CompareOrdinal(a.codomain, b.codomain));
                    string orderedInputs = "inputs " + inferred[0].domain + ", " + inferred[1].domain + ", " + inferred[2].domain;
                    Check(challenge.instruction.Contains(orderedInputs) &&
                        challenge.instruction.Contains(challenge.initialPairs[0].ToString()),
                        "Challenge 3 order clue does not match the unique expected bijection.");
                    foreach (var pair in inferred)
                        Check(!challenge.instruction.Contains(pair.ToString()),
                            "Challenge 3 instruction reveals an unfixed connection.");
                }
                if (stage == 3)
                {
                    bool isFunction = MappingRuleEvaluator.IsFunction(solution, challenge.domain);
                    Check(challenge.initialPairsLocked && solution.Count == 4 &&
                        sources.Count == (isFunction ? 4 : 3) &&
                        challenge.correctClassificationIndex == (isFunction ? 0 : 2),
                        "Challenge 4 classification answer is incorrect.");
                    if (isFunction) functionVariants++; else relationVariants++;
                }
                if (stage == 4)
                {
                    MappingProperty actual = MappingRuleEvaluator.Properties(solution, challenge.domain, challenge.codomain) &
                        (MappingProperty.Injective | MappingProperty.Surjective | MappingProperty.Bijective);
                    Check(challenge.initialPairsLocked && challenge.correctProperties == actual,
                        "Challenge 5 property answer is incorrect.");
                    if (actual == (MappingProperty.Injective | MappingProperty.Surjective | MappingProperty.Bijective))
                        bijectiveVariants++;
                    else if (actual == MappingProperty.Surjective) surjectiveOnlyVariants++;
                    else Check(false, "Challenge 5 contains an unexpected property combination.");
                }
            }
        }
        Check(functionVariants == 5 && relationVariants == 5,
            "Challenge 4 needs five functions and five relations.");
        Check(bijectiveVariants == 5 && surjectiveOnlyVariants == 5,
            "Challenge 5 needs five bijections and five surjective relations.");

        var domain = new[] { "1", "2", "3" };
        var codomain = new[] { "A", "B", "C" };
        var relation = new[] { new MappingPair("1", "A"), new MappingPair("1", "B"), new MappingPair("2", "C"), new MappingPair("3", "C") };
        var manyToOne = new[] { new MappingPair("1", "A"), new MappingPair("2", "A"), new MappingPair("3", "B") };
        var bijection = new[] { new MappingPair("1", "A"), new MappingPair("2", "B"), new MappingPair("3", "C") };
        Check(!MappingRuleEvaluator.IsFunction(relation, domain), "One input with two outputs was accepted as a function.");
        Check(MappingRuleEvaluator.IsFunction(manyToOne, domain) && MappingRuleEvaluator.IsManyToOne(manyToOne, domain), "Many-to-one function rejected.");
        Check(!MappingRuleEvaluator.IsInjective(manyToOne, domain), "Many-to-one function marked injective.");
        Check(MappingRuleEvaluator.IsBijective(bijection, domain, codomain), "Bijection rejected.");
        Check(!MappingRuleEvaluator.IsFunction(new[] { new MappingPair("1", "A") }, domain), "Missing outputs accepted as a function.");
        Check(!MappingRuleEvaluator.HasValidEndpoints(new[] { new MappingPair("1", "Z") }, domain, codomain), "Invalid codomain accepted.");
        Check(!MappingRuleEvaluator.IsSurjective(new[] { new MappingPair("1", "A") }, codomain), "Uncovered codomain accepted as surjective.");
        Check(MappingRuleEvaluator.ExactPairs(new[] { new MappingPair("1", "A"), new MappingPair("2", "B") },
            new[] { new MappingPair("2", "B"), new MappingPair("1", "A") }), "Pair equality depends on order.");

        var emitter = AssetDatabase.LoadAssetAtPath<DomainNode>("Assets/Prefabs/PREFABS/FunctionsRelations/DomainNode.prefab");
        var receiver = AssetDatabase.LoadAssetAtPath<CodomainNode>("Assets/Prefabs/PREFABS/FunctionsRelations/CodomainNode.prefab");
        Check(emitter != null && emitter.GetComponent<LaserEmitter>() != null, "Domain prefab is missing its emitter.");
        Check(receiver != null && receiver.GetComponent<LaserReceiver>() != null, "Codomain prefab is missing its receiver.");
        foreach (string path in new[] { "Assets/Prefabs/PREFABS/FunctionsRelations/DomainNode.prefab", "Assets/Prefabs/PREFABS/FunctionsRelations/CodomainNode.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root) == 0, "Prefab has a missing script: " + path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var crystal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FunctionsRelations/CrystalDiamond.mat");
        Check(crystal != null && crystal.shader.name == "Universal Render Pipeline/Lit" && crystal.GetFloat("_Surface") == 1f && crystal.GetTexture("_BaseMap") != null, "Crystal needs its URP transparent shader and texture.");
        var laser = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FunctionsRelations/LaserUnlit.mat");
        Check(laser != null && laser.shader.name == "Universal Render Pipeline/Unlit", "Laser material must use URP Unlit.");
        foreach (string color in new[] { "LaserGreen", "LaserRed" })
        {
            var stateMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FunctionsRelations/" + color + ".mat");
            Check(stateMaterial != null && stateMaterial.shader.name == "Universal Render Pipeline/Unlit",
                "Missing URP laser state material: " + color);
        }

        if (SceneManager.GetActiveScene().name == "Functions and Relations")
        {
            var manager = GameObject.Find("FunctionsRelationsStageManager");
            var board = GameObject.Find("FR_GameplayCanvas/FR_MappingBoard");
            Check(manager != null && manager.GetComponent<FunctionsRelationsStageManager>() != null, "Stage manager is absent.");
            Check(board != null && board.GetComponent<LaserMappingController>() != null, "Mapping board is absent.");
            var zone = GameObject.Find("Book_Statue/FR_BookInteractionZone");
            Check(zone != null && zone.GetComponent<SphereCollider>() != null &&
                zone.GetComponent<SphereCollider>().isTrigger, "Book Statue is missing its interaction collider.");
            var blocker = GameObject.Find("Book_Statue").GetComponent<BoxCollider>();
            Check(blocker != null && !blocker.isTrigger && blocker.bounds.min.y < 1.6f &&
                blocker.bounds.max.y > 5.4f, "Book Statue needs a solid collider from floor to book.");
            Check(GameObject.Find("FR_WorldMapping/Domain Nodes") == null &&
                GameObject.Find("FR_WorldMapping/Codomain Nodes") == null, "Temporary sphere node roots remain.");
            var controller = board.GetComponent<LaserMappingController>();
            Check(controller.DomainDiamonds.Length == 4 && controller.RangeDiamonds.Length == 4,
                "The four numbered diamonds on each asset are not wired.");
            for (int i = 0; i < 4; i++)
            {
                var domainDiamond = controller.DomainDiamonds[i];
                var rangeDiamond = controller.RangeDiamonds[i];
                Check(domainDiamond != null && domainDiamond.name == (i + 1).ToString() &&
                    domainDiamond.GetComponent<LaserEmitter>() != null && domainDiamond.GetComponent<Collider>() != null,
                    "Domain diamond " + (i + 1) + " is not interactive.");
                Check(rangeDiamond != null && rangeDiamond.name == (i + 1).ToString() &&
                    rangeDiamond.GetComponent<LaserReceiver>() != null && rangeDiamond.GetComponent<Collider>() != null,
                    "Range diamond " + (i + 1) + " is not interactive.");
                Check(domainDiamond.GetComponent<LaserEmitter>().BeamAnchor == domainDiamond.Find("FR_BeamEmitter") &&
                    rangeDiamond.GetComponent<LaserReceiver>().BeamAnchor == rangeDiamond.Find("FR_BeamReceiver"),
                    "Beam anchors are missing on diamond " + (i + 1));
                Check(domainDiamond.GetComponent<MeshRenderer>().sharedMaterial == crystal &&
                    rangeDiamond.GetComponent<MeshRenderer>().sharedMaterial == crystal,
                    "Crystal material is missing on diamond " + (i + 1));
            }
            Check(controller.DomainDiamonds[0].Find("FR_PurposeLabel") != null &&
                controller.RangeDiamonds[0].Find("FR_PurposeLabel") != null, "Diamond purpose labels are missing.");
            var informationBoard = GameObject.Find("InformationBoard");
            Check(informationBoard != null && PrefabUtility.IsPartOfPrefabInstance(informationBoard), "InformationBoard must retain its prefab link.");
            var problem = informationBoard.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .FirstOrDefault(t => t.name == "ChallengeInstructionsLabel");
            Check(problem != null, "InformationBoard instructions are missing.");
            var boardRenderer = informationBoard.GetComponentsInChildren<Renderer>()
                .First(r => r.name == "Info_ParchmentSurface");
            Check(problem.transform.position.x >= boardRenderer.bounds.min.x &&
                problem.transform.position.x <= boardRenderer.bounds.max.x &&
                problem.transform.position.y >= boardRenderer.bounds.min.y &&
                problem.transform.position.y <= boardRenderer.bounds.max.y &&
                problem.transform.position.z < boardRenderer.bounds.min.z,
                "InformationBoard instructions should sit within and just in front of the parchment.");
            Check(GameObject.Find("Board") == null, "The retired Board must not display duplicate instructions.");
            Check(GameObject.Find("Book_Statue").GetComponentsInChildren<Renderer>(true).Length > 0,
                "Book Statue needs renderers for incorrect-answer feedback.");
            var serialized = new SerializedObject(manager.GetComponent<FunctionsRelationsStageManager>());
            foreach (string field in new[] { "catalog", "mapping", "bookStatue", "bookInteractionCollider", "board", "informationBoardView", "gameplayControls", "interactionPromptText", "nearbyInteractionButton", "worldProblemText", "worldChallengeTitleText", "worldChallengeSetsText", "domainInteractionTarget", "rangeInteractionTarget", "neutralPedestalOrbMaterial", "neutralPedestalIconMaterial", "hudTimerText", "domainChoicesPanel", "rangeChoicesPanel", "undoConnectionButton", "submitButton", "openButton", "resultsPanel", "retryButton", "menuButton" })
                Check(serialized.FindProperty(field).objectReferenceValue != null, "Missing stage reference: " + field);
            var pedestals = serialized.FindProperty("classificationPedestals");
            Check(pedestals.arraySize == 3, "Challenges 4 and 5 need three answer pedestals.");
            Check(serialized.FindProperty("pedestalOrbMaterials").arraySize == 3 &&
                serialized.FindProperty("pedestalIconMaterials").arraySize == 3,
                "Pedestal color references are incomplete.");
            for (int i = 0; i < pedestals.arraySize; i++)
            {
                var pedestal = pedestals.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                Check(pedestal != null && pedestal.GetComponent<Collider>() != null &&
                    pedestal.Find("FR_AnswerUI/Emblem") != null &&
                    pedestal.Find("FR_AnswerUI/Label") != null &&
                    pedestal.Find("FR_AnswerUI/EmblemOrb") != null,
                    "Pedestal " + i + " needs its collider, symbol, label, and orb.");
            }
            Check(serialized.FindProperty("startingSeconds").floatValue == 600f, "Book timer must start at ten minutes.");
            foreach (string field in new[] { "domainChoiceButtons", "rangeChoiceButtons" })
            {
                var choices = serialized.FindProperty(field);
                Check(choices.arraySize == 4, field + " needs four numbered choices.");
                for (int i = 0; i < 4; i++)
                    Check(choices.GetArrayElementAtIndex(i).objectReferenceValue != null,
                        field + " is missing button " + (i + 1));
            }
            var mappingRefs = new SerializedObject(board.GetComponent<LaserMappingController>());
            foreach (string field in new[] { "domainDiamond", "rangeDiamond", "beamRoot", "laserMaterial", "greenLaserMaterial", "redLaserMaterial" })
                Check(mappingRefs.FindProperty(field).objectReferenceValue != null, "Missing world mapping reference: " + field);
        }
        Debug.Log("Functions and Relations regression checks passed: 50 unique four-diamond variants, balanced classification answers, URP lasers, Book collider, UI, and scene wiring.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Functions and Relations check failed: " + message);
    }
}
