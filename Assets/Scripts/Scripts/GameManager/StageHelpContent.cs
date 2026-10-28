using System;
using System.Text;

/// <summary>Stage-specific reading copy for the shared, single-page field guide.</summary>
public static class StageHelpContent
{
    // Dark warm accents stay legible on the shared cream reading-paper surface.
    private static string Section(string heading, string body)
    {
        return "<color=#73451F><b>" + heading + "</b></color>\n" + body;
    }

    private static string Guide(params string[] sections) { return string.Join("\n\n", sections); }
    private static string Action(string text) { return "<b><u>" + text + "</u></b>"; }
    private static string Controls => Section("CONTROLS", "Joystick: move.\nJump button: jump.\n<b><u>Hand button</u></b>: interact nearby.\nHelp pauses play. <b><u>Let’s Begin</u></b> starts pending practice or returns to play.");

    /// <summary>Returns a stable key for actual scene names and their common display aliases.</summary>
    public static string KeyForScene(string scene)
    {
        var key = new StringBuilder();
        foreach (char c in scene ?? "") if (char.IsLetterOrDigit(c)) key.Append(char.ToLowerInvariant(c));
        switch (key.ToString())
        {
            case "prelim": case "propositionallogic": return "PRELIM";
            case "truthtable": return "TruthTable";
            case "rulesofinference": return "RulesOfInference";
            case "formalvsinformal": case "formalvsinformalproofs": return "Formal vs. Informal Proofs";
            case "directindirect": case "directandindirectproofs": return "Direct&Indirect";
            case "induction": case "mathematicalinduction": return "Induction";
            case "setssss": case "sets": return "Setssss";
            case "functionandrelations": case "functionsandrelations": return "Functions and Relations";
            case "graph": case "graphs": return "Graph";
            case "trees": return "Trees";
            case "logicgarden": return "LogicGarden";
            case "logiclibrary": return "LogicLibrary";
            default: return scene ?? "";
        }
    }

    public static string TitleForScene(string scene)
    {
        string key = KeyForScene(scene);
        if (key == "LogicGarden") return "Logic Garden";
        if (key == "LogicLibrary") return "Logic Library";
        try { return TrialCatalog.ForScene(key).Topic; }
        catch (ArgumentException) { return string.IsNullOrEmpty(scene) ? "How to Play" : scene; }
    }

    /// <summary>Unknown scenes do not accidentally receive another trial's instructions.</summary>
    public static bool TryForScene(string scene, out string content)
    {
        content = ForScene(scene);
        return !string.IsNullOrEmpty(content);
    }

    public static string ForScene(string scene)
    {
        switch (KeyForScene(scene))
        {
            case "PRELIM": return Propositions();
            case "TruthTable": return TruthTables();
            case "RulesOfInference": return Inference();
            case "Formal vs. Informal Proofs": return ProofStyles();
            case "Direct&Indirect": return ProofMethods();
            case "Induction": return Induction();
            case "Setssss": return Sets();
            case "Functions and Relations": return Functions();
            case "Graph": return Graphs();
            case "Trees": return Trees();
            case "LogicGarden": return Garden();
            case "LogicLibrary": return Library();
            default: return "";
        }
    }

    private static string Propositions() => Guide(
        Section("THE TOPIC", "A <b>proposition</b> is true (T) or false (F). P and Q represent propositions."),
        Controls,
        Section("OPERATORS", "<b>AND ∧:</b> both true.\n<b>OR ∨:</b> at least one true.\n<b>XOR:</b> exactly one true.\n<b>P → Q:</b> false only for T → F.\n<b>P ↔ Q:</b> true when P and Q agree."),
        Section("PLAY", "1. Tap the <b><u>hand at the Book Statue</u></b>.\n2. Read the question. Compare all four doors.\n3. Use the hint board: one row appears every 20 seconds.\n4. Stand on a door pad for about two seconds to answer. Leave to cancel.\n5. A correct answer opens the next room. Start again at its book."),
        Section("TIME & RETRIES", "<b>Six minutes</b> shared across five challenges. Correct answers pause time; books resume it. Help and practice pause time.\nA wrong door costs <color=#A42828><b>10 seconds</b></color>, knocks you back and briefly stuns you. Use the book afterward for a new question.\nAt zero, Start Over resets the run. Try Again keeps progress: four minutes for Challenges 1–3, three for 4–5. Two checkpoint retries are available; then study in Logic Garden."));

    private static string TruthTables() => Guide(
        Section("THE TOPIC", "A <b>truth table</b> gives an expression’s result for every input combination. Each numbered row shows P and Q.\n<b>¬:</b> reverse T/F. <b>∧:</b> both true. <b>∨:</b> at least one true. <b>XOR:</b> exactly one true. <b>→:</b> false only for T → F. <b>↔:</b> inputs agree."),
        Controls,
        Section("YOUR TASK", "Complete Easy, Medium and Hard. Each challenge has <b>three columns of four rows</b>. True/False element blocks represent each row’s result."),
        Section("PLAY", "1. Tap the <b><u>hand at the book</u></b>.\n2. Use the hand at a closed house. Answer its four-choice quiz. Either answer opens it.\n3. Collect blocks from open houses with the hand.\n4. Select a True/False inventory stack. Fill the required rows.\n5. Tap <b><u>Submit Column</u></b>. Finish all three columns, then start the next challenge at the book.\nTap the minimap to enlarge it."),
        Section("TIME & CORRECTIONS", "<b>18 minutes per challenge.</b> Help, practice, quizzes and the marked board area pause time.\nWrong quiz: <color=#A42828><b>−10s</b></color>; read the correct answer.\nWrong column: <b>−20s</b>; blocks return to inventory. Retry the expression.\nInvalid placement: that block returns to a house; doors and quizzes reset. No time penalty.\nAt zero, Restart returns to Easy."));

    private static string Inference() => Guide(
        Section("THE TOPIC", "<b>Premises:</b> statements accepted as true.\n<b>Rule of inference:</b> a valid reasoning pattern.\n<b>Conclusion:</b> what follows from the premises.\nA <b>Rule Crystal</b> represents its labeled inference rule."),
        Controls,
        Section("YOUR TASK", "Solve five questions: one from each bank of ten. Difficulty rises. Correct rules never repeat in a run."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b>.\n2. Climb the altar. Tap <b><u>View Board</u></b>.\n3. Fill the premise blanks. Tap Check argument. Green is correct; fix red entries. Editing clears their feedback.\n4. Find the matching crystal tower. Use the hand. Answer its equivalence quiz, read feedback, then collect. Either answer allows collection.\n5. Carry it to the altar. <b><u>Submit to Altar</u></b> opens the board only. Tap Place Rule Crystal to validate.\n6. Read the conclusion. Use Back or Continue to leave."),
        Section("TIME & CRYSTALS", "<b>18 minutes shared.</b> Help, practice, board view, quizzes and completed challenges pause time. Books resume it.\nWrong pickup quiz: <color=#A42828><b>−10s once</b></color>. Wrong altar submission: <b>−10s separately</b>.\nCarry one crystal. Recollecting it keeps its completed quiz until the next challenge, including after water recovery.\nAt zero, the attempt ends."));

    private static string ProofStyles() => Guide(
        Section("THE TOPIC", "<b>Formal proofs:</b> explicit mathematical statements with justified steps.\n<b>Informal proofs:</b> valid reasoning explained in prose. <u>Informal does not mean incorrect.</u>\nA premise is a starting statement. A justification explains a step."),
        Controls,
        Section("YOUR TASK", "Five challenges, <b>two distinct questions each</b>: classify proofs, classify more scrolls, order steps, supply a justification, then formalize prose."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b> to start.\n2. Tap View Board to read; Back returns.\n3. Interact with a scroll. Read it. <b><u>Get This Scroll</u></b> collects it; Back leaves it.\n4. Carry one scroll. Place it on the matching altar or numbered STEP/RULE slot. Arrows mark empty slots, not correct answers.\n5. Return Scroll restores a held scroll. Use Take Back or remove/replace a filled slot to revise.\n6. Return to the book. Use the <b><u>hand to confirm</u></b> the whole arrangement."),
        Section("TIME & CORRECTIONS", "<b>10 minutes shared.</b> Books resume time; correct answers pause it. Scroll reading and View Board keep time running. Help, practice and introductions pause it.\nWrong or incomplete confirmation: <color=#A42828><b>−10s</b></color>. Placements stay; correct them and retry.\nFinish both questions before advancing. At zero, Retry starts a fresh randomized run."));

    private static string ProofMethods() => Guide(
        Section("THE TOPIC", "<b>Direct:</b> derive the conclusion from the givens.\n<b>Contrapositive:</b> prove ¬Q → ¬P to establish P → Q.\n<b>Contradiction:</b> assume the claim is false; derive a justified impossibility. The last two are <b>indirect</b> methods."),
        Controls,
        Section("YOUR TASK", "Five challenges, <b>two distinct questions each</b>: identify a method; order direct, contrapositive and contradiction proofs; then choose a method and order its proof."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b> to start.\n2. Near the altars, tap View Board. Back returns without submitting.\n3. For classification, use the hand at Direct or Indirect. The glow records selection only. Select it again in Challenge 1 to clear.\n4. For ordering, open the active altar. Drag a row onto another to swap. Put facts before steps that use them.\n5. In Challenge 5, also choose Direct, Contrapositive or Contradiction.\n6. Back to Room keeps your draft. Return to the book; use the <b><u>hand to confirm</u></b>.\nArrows mark available choices. Selecting hides them; clearing restores them. No scroll collection is required."),
        Section("TIME & CORRECTIONS", "<b>10 minutes shared.</b> Reading and ordering use time. Help and tutorials pause it.\nWrong confirmation: <color=#A42828><b>−10s</b></color>. Method and order stay; reopen the altar to correct them.\nCorrect answers pause time. Start each new question at the book. Finish both before advancing. At zero, Retry starts a fresh run."));

    private static string Induction() => Guide(
        Section("THE TOPIC", "<b>Induction</b> proves a statement over its stated integer domain.\n<b>Base case:</b> prove the starting case.\n<b>Hypothesis:</b> assume P(k) for an arbitrary applicable k.\n<b>Step:</b> use that assumption to prove P(k + 1). Testing examples alone is not proof."),
        Controls,
        Section("YOUR TASK", "Five challenges: base case, hypothesis, next-case goal, ordered steps, then a full proof at all three stations."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b>.\n2. Tap View Board to read; Back returns.\n3. At Formula Scrolls, select and read a scroll. Tap <b><u>Get This Scroll</u></b>.\n4. Carry one scroll. Follow the arrow. Use the hand or placement button at the required altar.\n5. Order numbered steps logically. Remove or replace misplaced scrolls. Return Scroll restores a held scroll to the stand.\n6. Tap <b><u>Submit</u></b> to check every required slot. The book only starts challenges."),
        Section("TIME & CORRECTIONS", "<b>18 minutes shared.</b> Books start/resume time. Correct answers pause it; the remaining time carries forward. Help and practice pause it.\nWrong submission: <color=#A42828><b>−10s</b></color>. Placed scrolls stay for correction.\nAt zero, the attempt ends."));

    private static string Sets() => Guide(
        Section("THE TOPIC", "Numbered blocks represent <b>elements</b>. The number identifies an element.\n<b>A and B</b> are sets represented by circles. Place each element in the region required by the question."),
        Controls,
        Section("YOUR TASK", "Complete five challenges, <b>two distinct questions each</b>. Read the required sets on the Information Board. Each challenge has one short introduction."),
        Section("PLAY", "1. Tap the <b><u>hand at the book</u></b>.\n2. Near the spawner, tap <b><u>View Board</u></b>. Back returns.\n3. Collect an element with the hand or Pick Up Element. Carry it to the required region.\n4. Tap the <b><u>hand to drop</u></b>. Return Element sends a held block back.\n5. Let blocks settle. Tap <b><u>Submit</u></b> near the diagram. Each block counts in one region only.\n6. Keep green placements. Move red ones and retry."),
        Section("READ THE REGIONS", "<b>1 — Membership:</b> place the requested element in the named set.\n<b>2 — A and B:</b> fill the required regions.\n<b>3 — A ∩ B:</b> the overlap contains shared elements. A ∪ B is the union of both circles.\n<b>4 — A − B:</b> in A, not B. <b>B − A:</b> in B, not A. Fill full A, full B and the intersection too. Collect separate copies from the replenishing spawner.\n<b>5 — Complement:</b> inside universal set U, outside the named set. Reposition the supplied blocks; no replacement copies."),
        Section("TIME & CORRECTIONS", "<b>18 minutes per challenge.</b> Start at the book. Help and introductions pause play.\nWrong submission: <color=#A42828><b>−10s</b></color>. Blocks stay. Moving a block clears its old feedback.\nFinish both questions, then activate the book for the next challenge. At zero, the attempt ends."));

    private static string Functions() => Guide(
        Section("THE TOPIC", "<b>Relation:</b> input–output pairs.\n<b>Function:</b> exactly one output per input; inputs may share outputs.\n<b>Domain:</b> inputs. <b>Range:</b> outputs actually used. The Range panel also offers possible output choices; unused choices are not part of the range."),
        Controls,
        Section("YOUR TASK", "Five challenges, <b>two distinct random questions each</b>: described relation, ordered pairs, function completion, diagram classification and function properties."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b>.\n2. Tap View Board to read; Back returns.\n3. At Domain, tap the hand. Select an input. Follow the yellow arrow to Range.\n4. At Range, tap the hand and select an available output. The arrow returns to Domain. Repeat every required pair. Relations may give an input several outputs.\n5. In the <b>Range panel</b>, each connection has its own <b><u>Remove</u></b> button. Delete any editable pair; fixed links stay. After connecting, review the list or close the panel and choose another Domain input.\n6. For classification, inspect preset lasers and select the answer pedestal. For properties, toggle every applicable pedestal. Tap again to deselect.\n7. <b><u>Submit</u></b> checks the whole answer. The book only starts/resumes."),
        Section("PROPERTIES", "<b>Injective:</b> different inputs use different outputs.\n<b>Surjective:</b> every available output is used.\n<b>Bijective:</b> both. Follow any extra order clues on the board."),
        Section("TIME & CORRECTIONS", "<b>10 minutes shared.</b> Correct answers pause time; the book resumes it. Help and practice pause it.\nWrong submission: <color=#A42828><b>−10s</b></color>. Connections stay for correction. Complete both questions before the next challenge. Start each question at the book. At zero, retry is available."));

    private static string Graphs() => Guide(
        Section("THE TOPIC", "A <b>graph</b> has vertices joined by edges. Platforms are vertices; bridges are edges.\n<b>Degree:</b> number of incident edges.\n<b>Path:</b> follows edges. Directed paths follow arrows.\n<b>Cycle:</b> returns to its start."),
        Controls,
        Section("YOUR TASK", "Five challenges, two distinct random questions each: travel, degree, simple path, directed path and cycle. Read the task and vertex labels on the Information Board and map."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b> to reveal the graph and start. Before activation, only the starting platform appears.\nTap <b><u>View Board</u></b> near the book or board to read the task. <b>Back</b> returns without submitting.\n2. Fully cross a connected bridge to record a move. Orange marks available bridges; blue marks crossed ones. Unconnected jumps do not count.\n3. Degree opens an overhead view. Movement controls hide. Choose a number and Confirm; Clear Selection clears it.\n4. Simple path: reach the destination without repeated vertices. Directed path: follow arrows and visit the required checkpoint.\n5. Cycle: meet the question’s minimum distinct-vertex count, visit its named vertex, and return to the start. Do not repeat intermediate vertices.\n6. Tap <b><u>Confirm</u></b> at the destination or completed cycle. The book can also confirm. Your live route appears beneath the minimap drawing. Tap the minimap to enlarge it."),
        Section("TIME & CORRECTIONS", "<b>10 minutes shared.</b> Books resume time; correct answers, board viewing, Help and challenge introductions pause it. Finish both questions before the next challenge.\nWrong confirmation: <color=#A42828><b>−10s</b></color>. Your route/selection stays.\n<b><u>Retry Route</u></b> returns to the start and clears the route. Time, mistakes and question stay. Falling also returns you.\nAt zero, Play Again begins a new run."));

    private static string Trees() => Guide(
        Section("THE TOPIC", "A <b>tree</b> is connected and has no cycle.\nThe <b>root</b> has no parent. Other nodes have one parent. Parents link to children; leaves have no children.\n<b>Depth:</b> edges from the root; root depth is zero.\n<b>Height:</b> longest root-to-leaf path, in edges."),
        Controls,
        Section("YOUR TASK", "Five challenges, two distinct random questions each. Question 2 uses a harder tree: identify roles, build a tree, repair parents, find depth/height and build a spanning tree."),
        Section("PLAY", "1. Use the <b><u>hand at the book</u></b> to open the board and start.\n2. Read the objective. Tap the requested node or choose a number.\n3. For links, select parent then child. Tap <b><u>Connect</u></b>. Select endpoints and Remove to delete an editable link. Clear Pick clears selection only.\n4. Repair: remove the old parent before adding a new one.\n5. Spanning tree: connect every vertex without a cycle. V vertices require V − 1 edges. Several answers may be valid.\n6. Tap <b><u>Submit</u></b> to check. Back keeps work; the book reopens it. Tap the minimap to enlarge it."),
        Section("TIME & CORRECTIONS", "<b>10 minutes shared.</b> Board editing uses time; Help pauses it.\nWrong confirmation: <color=#A42828><b>−10s</b></color>. Work stays for correction. Correct answers pause time; start again at the book.\nAt zero, Play Again begins a new run."));

    private static string Garden() => Guide(
        Section("WELCOME", "Learn the controls. Meet <b>Lefyahj</b>, your wizard guide. Complete his introduction to unlock the Logic Library."),
        Controls,
        Section("PLAY", "Practice movement, jumping and the hand button.\nFollow the exclamation mark to Lefyahj.\nTap <b><u>Talk with Lefyahj</u></b> or use the hand.\nRead each message. Follow the next objective."),
        Section("STUDY & REPLAY", "Library books are grouped by topic.\n<b><u>Replay Tutorial</u></b> offers Garden Tutorial, Library Tour or Full Tutorial.\nReplay preserves progress and completed objectives. Returning from the library does not repeat onboarding."));

    private static string Library() => Guide(
        Section("WELCOME", "Lefyahj introduces four study sections: <b>PRELIM, MIDTERMS, PRE-FINALS and FINALS</b>. Read titles and topic descriptions to choose a lesson."),
        Controls,
        Section("READ & EXPLORE", "Finish Lefyahj’s introduction.\nTap Next during the bookshelf tour.\nAt “Did you get all that?”, Yes ends the tour; No repeats it.\nNear a book or shelf, tap <b><u>Read</u></b>. Talk with Lefyahj is for the wizard."),
        Section("REPLAY", "<b><u>Replay Tutorial</u></b> offers Garden Tutorial, Library Tour or Full Tutorial.\nSkip Tutorial restores the normal camera and controls.\nReplay preserves objectives and library access. Practice awards no duplicate rewards."));
}
