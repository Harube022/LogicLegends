using System;
using System.Collections.Generic;
using LogicLegends.Inference;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    [Serializable] public sealed class ArchiveLogicBank { public string title; public List<LogicQuestion> questions = new List<LogicQuestion>(); }
    [Serializable] public sealed class ArchiveSetCase { public string[] a, b, universe; public string setName = "A"; }
    [Serializable] public sealed class ArchiveSetBank { public List<ArchiveSetCase> questions = new List<ArchiveSetCase>(); }

    [CreateAssetMenu(menuName = "Logic Legends/Open Archive/Topic Catalog")]
    public sealed class ArchiveTopicCatalog : ScriptableObject
    {
        public List<ArchiveLogicBank> propositions = new List<ArchiveLogicBank>();
        public InferenceRule[] inferenceRules;
        public ProofChallengeCatalog formalProofs;
        public DirectIndirectProofCatalog proofMethods;
        public MappingChallengeCatalog mappings;
        public GraphChallengeCatalog graphs;
        public TreeChallengeCatalog trees;
        public List<ArchiveSetBank> sets = new List<ArchiveSetBank>();
        public static readonly string[] Names = { "Propositional Logic", "Truth Table", "Rules of Inference", "Formal vs. Informal Proofs", "Direct and Indirect Proofs", "Mathematical Induction", "Sets", "Functions and Relations", "Graphs", "Trees" };
        public static readonly string[] Arenas = { "Rune Choice Circle", "Truth Mosaic", "Inference Constellation", "Scribes' Court", "Proof Forge", "Induction Court", "Garden of Sets", "Mapping Conduit", "Path of Connections", "Tree of Connections" };
        public const int SetChallengeCount=5,SetProblemsPerChallenge=2;
        public static int RoundCount(int topic) => topic == 1 ? 9 : topic == 6 ? SetChallengeCount*SetProblemsPerChallenge : 5;
    }

    public enum ArchiveActivity { Choice, Truth, Premises, Sort, Order, Method, Induction, Sets, Mapping, Graph, Tree }
    public enum ArchiveAction { Start, Pick, Place, Select, Submit, Connect, Remove, Undo, RetryRoute, BeginChoice, ReturnItem, ClearSelection, AdminCompleteTopic, AdminResetTopic, RequestHint, BarrierHint, BarrierAnswer }
    [Serializable] public sealed class ArchiveCarry { public int actor, item; }
    [Serializable] public sealed class ArchiveMistake { public int actor, topic, round, revision, sequence; public double time; public string reason; }
    [Serializable] public sealed class ArchiveActor { public int actor, mistakes, lastSequence, mappingInput = -1, mappingRound = -1, treeFirst=-1, treeSecond=-1, treeRound=-1; public string displayName; }
    [Serializable] public sealed class ArchiveStationState
    {
        public int topic, round, revision, phase, subphase, selection = -1, flags;
        public int testResetSerial;
        public int[] placements = Array.Empty<int>();
        // Last submitted Sets block grades. Moving a block clears only its own receipt.
        public int[] setGrades = Array.Empty<int>();
        public List<int> links = new List<int>(), route = new List<int>();
        public List<ArchiveCarry> carries = new List<ArchiveCarry>();
        public int routeActor, channelActor, channelChoice = -1, channelSequence;
        public double channelStarted;
        // Authoritative answer receipt. Presentation can animate it without owning progress.
        public int answerSerial, lastAnswerActor, lastAnswerChoice = -1, lastAnswerRound = -1, lastAnswerResult;
        public double lastAnswerAt;
        // Shared topic deadline is checkpointed with progress; readers never own a clock.
        public double timerDeadline;
        public float timerLimit, timerSavedRemaining;
        public bool timerRunning, timerExpired;
        public int timerActor, timerAttempt, timerTimeouts;
        public bool continuous = true, routeSampleValid;
        // Checkpoint the current bridge transaction with the authoritative route.
        public int routeEdge=-1;
        public bool routeCrossedMiddle,routeAtVertex;
        public double routeSampleAt;
        public string routeReason="";
        public Vector3 lastRoutePosition;
        public string feedback = "Use the book to begin.", reveal = "";
    }
    [Serializable] public sealed class ArchiveTeamState
    {
        public int version = 1, seed, revision;
        public ArchiveStationState[] stations;
        public List<ArchiveActor> actors = new List<ArchiveActor>();
        public List<ArchiveMistake> mistakes = new List<ArchiveMistake>();
        public ArchiveExitState exit = new ArchiveExitState();
        public ArchiveBarrierChallengeState barrier = new ArchiveBarrierChallengeState();
        public List<ArchiveLearningHint> learningHints = new List<ArchiveLearningHint>();
    }
    [Serializable] public sealed class ArchiveExitState
    {
        public bool opened, won;
        public double openedAt, wonAt;
        public List<ArchiveEscape> escaped = new List<ArchiveEscape>();
    }
    [Serializable] public sealed class ArchiveEscape
    {
        public int actor;
        public Vector3 anchor;
        public double time;
    }
    [Serializable] public sealed class ArchiveCommand
    {
        public int topic, revision, sequence, a = -1, b = -1;
        public int captureSerial, encounterHuntSerial;
        public ArchiveAction action;
    }

    // A presentation-neutral adapter. The original solo controllers and timers never run here.
    public sealed class ArchiveQuestion
    {
        public ArchiveActivity activity;
        public string title, prompt, instructions, successText;
        public string[] items = Array.Empty<string>(), details = Array.Empty<string>(), slots = Array.Empty<string>();
        public int[] expected = Array.Empty<int>(), itemOrder = Array.Empty<int>();
        public int setGroup=-1,setEntry=-1;
        public string[] setA=Array.Empty<string>(),setB=Array.Empty<string>(),setUniverse=Array.Empty<string>(),setSourceValues=Array.Empty<string>();
        public string setName="A";
        public int[] setInitialPlacements=Array.Empty<int>();
        public Vector3[][] setCells;
        public string[][] regionValues;
        public List<int[]> orders = new List<int[]>();
        public int correctChoice = -1, method = -1, statementCount;
        public bool reusableItems, regionPlacement;
        public ProofChallengeData proof;
        public TrialProofChallenge trial;
        public MappingChallengeData mapping;
        public GraphChallengeData graph;
        public TreeChallengeData tree;
        public string[] nodeIds = Array.Empty<string>();
        public Vector2[] nodes = Array.Empty<Vector2>();
        public string[] inputLabels = Array.Empty<string>(), outputLabels = Array.Empty<string>();
        public int PlacementCount => regionPlacement ? items.Length : slots.Length;
    }
}
