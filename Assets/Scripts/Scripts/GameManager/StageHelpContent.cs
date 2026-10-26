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
    private static string Controls => Section("ESSENTIAL CONTROLS",
        "Move with the joystick. Tap the jump button to jump and the " + Action("hand button") +
        " to interact with nearby objects. Help pauses play. Tap " + Action("Let’s Begin") +
        " to open any pending practice, or restore gameplay.");

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
        Section("THE TOPIC", "A <b>proposition</b> is a statement that is true (T) or false (F). P and Q stand for propositions. Identify five logical operators from descriptions and truth-table patterns."),
        Section("KNOW THE OPERATORS", "<b>AND</b> (∧): both are true. <b>OR</b> (∨): at least one is true. <b>XOR</b>: exactly one is true. A <b>conditional</b> P → Q is false only when P is true and Q is false. A <b>biconditional</b> P ↔ Q is true when P and Q agree."),
        Controls,
        Section("PLAY THE CHALLENGE", "1. Approach the current room’s " + Action("Book Statue") + " and use the hand to reveal the question.\n2. Read the question and compare the four answer doors. The hint board reveals one truth-table result every 20 seconds, up to four rows.\n3. Stand on your chosen door’s pad for about two seconds. Leaving the pad cancels your choice.\n4. A correct door advances to the next room. Activate that room’s book to continue."),
        Section("TIME, MISTAKES & RETRIES", "One <b>six-minute shared timer</b> covers all five challenges. Correct answers pause it; the next book resumes the remaining time. A wrong door costs <color=#A42828><b>10 seconds</b></color>, knocks you back and briefly stuns you. After recovering, use the book for a new question in the same challenge.\n\nAt zero, Start Over begins a fresh run. Try Again keeps your challenge progress with four minutes for Challenges 1–3 or three minutes for Challenges 4–5. Two checkpoint retries are available; afterward you can study in Logic Garden. Help and practice use no countdown time."));

    private static string TruthTables() => Guide(
        Section("THE TOPIC", "A <b>truth table</b> lists the result of a logical expression for every combination of its inputs. P and Q can each be T or F. Negation (¬) reverses a truth value; AND (∧), OR (∨), XOR, conditional (→) and biconditional (↔) combine statements."),
        Section("YOUR OBJECTIVE", "Complete the Easy, Medium and Hard challenges. Each has three columns of four rows. The numbered rows identify the P/Q combination; fill the required result column with the matching <b>True/False element blocks</b>."),
        Controls,
        Section("PLAY THE CHALLENGE", "1. Use the hand at the " + Action("Book Statue") + " to start.\n2. Approach a house door and use the hand to answer its four-choice quiz. Either answer opens the door. The quiz tests logic separately from the board’s rows.\n3. Collect available True/False blocks from open houses using the hand. Closed houses hide their block prompts.\n4. At the board, use the selected stack to fill each required row, then " + Action("Submit") + " the complete column. Continue through all three columns.\n5. Activate the book again for the next challenge. Tap the minimap for a larger map."),
        Section("TIME & CORRECTIONS", "Each challenge starts with <b>18 minutes</b>. Help, practice, door quizzes and the marked board area pause the countdown. An incorrect house quiz costs <color=#A42828><b>10 seconds</b></color> and shows the correct answer. An incorrect column costs <color=#A42828><b>20 seconds</b></color> and returns its blocks to your inventory; retry that expression. An invalid placement returns that block to a house and resets doors and quizzes without a time deduction. At zero, Restart begins again at Easy."));

    private static string Inference() => Guide(
        Section("THE TOPIC", "<b>Premises</b> are statements accepted as true. A <b>rule of inference</b> is a valid reasoning pattern that derives a <b>conclusion</b> from them. A <b>Rule Crystal</b> represents the inference rule printed on its label."),
        Section("YOUR OBJECTIVE", "Solve five questions: one random question from each challenge’s bank of ten. The tasks become harder, and the correct inference rule never repeats within a playthrough."),
        Controls,
        Section("COMPLETE THE ARGUMENT", "1. Use the hand at the " + Action("Book Statue") + " to begin. Climb the altar stairs and open " + Action("View Board") + ".\n2. Drag words into the premise blanks and tap Check argument. Green entries are correct; red entries need correction. Editing an entry clears its old feedback.\n3. Identify the rule that fits the completed premises. Approach its tower and use the hand. Answer a logical-equivalence pickup quiz, read the feedback, then collect the crystal. Either answer allows collection.\n4. Carry it to the altar. " + Action("Submit to Altar") + " opens the board; it does not grade the crystal. Tap Place Rule Crystal to validate.\n5. Read the revealed conclusion before using Back or Continue."),
        Section("TIME & FEEDBACK", "One <b>18-minute shared timer</b> starts or resumes through the book. Help, practice, board viewing, pickup quizzes and time between challenges pause it. An incorrect pickup-quiz answer costs <color=#A42828><b>10 seconds</b></color> once; an incorrect altar submission separately costs <b>10 seconds</b>. Correct answers pause time and prepare the next book activation.\n\nCarry one crystal at a time. Dropping, recovering and recollecting the same crystal keeps its completed quiz state until the next challenge; it does not require another quiz. At zero, the attempt ends."));

    private static string ProofStyles() => Guide(
        Section("THE TOPIC", "<b>Formal proofs</b> use explicit mathematical statements and justified logical steps. <b>Informal proofs</b> explain valid reasoning through prose. <u>Informal does not mean incorrect.</u> A premise is a starting statement; a justification explains why a step follows."),
        Section("YOUR OBJECTIVE", "Solve <b>two distinct random questions in each of five challenges</b>: classify proof styles, classify more scrolls, order proof steps, supply a missing justification, then formalize an explanation with ordered statements and rules."),
        Controls,
        Section("READ, PLACE & CONFIRM", "1. Use the hand at the " + Action("Book Statue") + " to start each question. View Board brings the Information Board closer; Back restores the camera.\n2. Interact with a selectable scroll to read its full content. " + Action("Get This Scroll") + " collects it; Back leaves it. Carry one scroll at a time.\n3. Place it on the appropriate proof altar or numbered STEP/RULE slot. Yellow arrows identify empty available slots, not correct answers.\n4. Use Return Scroll to restore a carried scroll to its source. Retrieve a placed scroll through an altar’s Take Back menu or remove/replace a filled step slot.\n5. Return to the book and use the " + Action("hand to confirm") + " the entire arrangement."),
        Section("TIME & CORRECTIONS", "One <b>10-minute shared countdown</b> continues between questions and challenges. The book starts or resumes it. Reading scrolls and View Board keep active time running; Help, practice and challenge introductions pause it. Incorrect or incomplete confirmations cost <color=#A42828><b>10 seconds</b></color> and preserve your work. Correct your placements and confirm again. Correct answers pause time; start the next question at the book. Both questions are required before advancing. At zero, Retry starts a fresh randomized run."));

    private static string ProofMethods() => Guide(
        Section("THE TOPIC", "A <b>direct proof</b> derives the conclusion from the given assumptions. An <b>indirect proof</b> uses a contrapositive or contradiction. To prove P → Q by <b>contrapositive</b>, prove ¬Q → ¬P. A <b>contradiction</b> assumes the opposite of the claim and derives a justified impossibility."),
        Section("YOUR OBJECTIVE", "Complete <b>five challenges with two distinct random questions each</b>. Identify a method, order a direct proof, order a contrapositive, order a contradiction, then choose a strategy and complete its proof."),
        Controls,
        Section("BUILD YOUR REASONING", "1. Use the hand at the " + Action("Book Statue") + " to start a question. Read the Information Board.\n2. For classification, interact with the Direct or Indirect altar to select your answer. A selection glow records your choice; it does not reveal correctness.\n3. For ordering tasks, interact with the active altar to open its proof board. Read statements and justifications, then drag one row onto another to swap them. Put assumptions and definitions before the steps that use them.\n4. In the final challenge, also choose Direct, Contrapositive or Contradiction as appropriate.\n5. " + Action("Back to Room") + " keeps your method and order. Return to the book and use the " + Action("hand to confirm") + " your complete answer. There is no gameplay scroll-collection requirement here."),
        Section("BOARD & GUIDANCE", "Near the proof altars, " + Action("View Board") + " frames the main Information Board. Back restores the room without submitting. Yellow arrows mark available altars. Selecting an answer hides all choice arrows. In Challenge 1, tap your selected altar again to clear the choice and restore the available arrows. In ordering challenges, opening an altar hides its arrow; Back to Room restores it and keeps your draft. The main board supplies the full question for Challenges 4 and 5."),
        Section("TIME & CORRECTIONS", "One <b>10-minute shared timer</b> starts or resumes through the book and continues while you read and arrange. Help and tutorials pause it. A wrong validated answer costs <color=#A42828><b>10 seconds</b></color> and preserves your method and row order. Reopen the altar to fix them. Correct answers pause time; activate the book for the next question. Complete both questions before the next challenge. At zero, Retry begins a fresh randomized run."));

    private static string Induction() => Guide(
        Section("THE TOPIC", "<b>Mathematical induction</b> proves a statement over its integer domain. Establish the <b>base case</b>, assume it holds for an arbitrary applicable integer k (the <b>inductive hypothesis</b>), then use that assumption to prove the case k + 1 (the <b>inductive step</b>). Testing examples alone is not a proof."),
        Section("YOUR OBJECTIVE", "Complete five challenges: verify the base case, identify the hypothesis, identify the next-case goal, arrange proof steps, then complete a full proof across all three stations."),
        Controls,
        Section("ASSEMBLE THE PROOF", "1. Activate the " + Action("Book Statue") + " with the hand. Open View Board to read the objective; Back returns to gameplay.\n2. At Formula Scrolls, select a parchment option and tap " + Action("Get This Scroll") + ". Read its formula carefully and carry one scroll at a time.\n3. Follow the animated arrow to the required altar. Place your scroll using the hand or contextual action. Numbered proof steps must be logically ordered.\n4. Remove, replace or rearrange placed scrolls to correct your reasoning. Return Scroll makes a held scroll available at the stand again.\n5. Press " + Action("Submit at the altar") + ". It checks the whole current challenge, including every required station in the full-proof challenge. The book starts challenges; it does not submit them."),
        Section("TIME & FEEDBACK", "One <b>18-minute shared timer</b> starts or resumes through the book and never resets between challenges. A wrong submission costs <color=#A42828><b>10 seconds</b></color> and keeps placed scrolls for correction. Correct answers pause time and unlock the next challenge; activate the book again to continue. Help and practice pause the timer. At zero, the attempt ends."));

    private static string Sets() => Guide(
        Section("THE TOPIC", "These numbered blocks represent <b>elements of a set</b>. The number identifies each element. <b>A and B are sets</b>, represented by the diagram’s two circles. Place each element in the region required by the question; a set and its visual placement region are different things."),
        Section("YOUR OBJECTIVE", "Complete five challenges, each with <b>two distinct random questions</b>. Read the required sets on the Information Board. Each challenge has one short introduction before its first question."),
        Controls,
        Section("COLLECT, PLACE & SUBMIT", "1. Use the hand at the " + Action("Book Statue") + " to begin. View Board shows the current question; Back restores the camera.\n2. Collect an element block using the hand or Pick Up Element. Carry it to the diagram and use the hand to drop it. Return Element sends a held block back.\n3. Let blocks settle naturally inside the boundaries, then press " + Action("Submit") + ". Actual settled positions are graded. Each physical block fills only one required placement.\n4. Green number labels mark correct placements; red labels mark mistakes. Keep green blocks in place and move only what needs fixing."),
        Section("READ THE REGIONS", "<b>1 — Membership:</b> place the requested element in the named set.\n<b>2 — A and B:</b> fill their required regions from the board’s sets.\n<b>3 — Intersection:</b> A ∩ B contains elements shared by both sets.\n<b>4 — Differences:</b> A − B contains A’s elements absent from B; B − A reverses this. This task also requires full A, full B and intersection placements. Obtain separate copies from the replenishing spawner wherever an element is required in several regions.\n<b>5 — Complement:</b> U is the universal set of considered elements. A complement lies inside U but outside the specified set. Reposition the supplied blocks; this challenge has no replacement copies."),
        Section("TIME & CORRECTIONS", "Each challenge has its own <b>18-minute countdown</b>, started through the book. Help and introductions pause play. Wrong submissions cost <color=#A42828><b>10 seconds</b></color> and keep blocks in position; pickup or movement clears an element’s old label feedback. Answer both questions to complete a challenge, then activate the book for the next. At zero, the attempt ends."));

    private static string Functions() => Guide(
        Section("THE TOPIC", "A <b>relation</b> is a set of input–output pairs. A <b>function</b> gives every input exactly one output; different inputs may share an output. The <b>domain</b> contains inputs, the <b>codomain</b> contains all allowed outputs, and the <b>range</b> contains the outputs actually used."),
        Section("YOUR OBJECTIVE", "Complete five challenges, with one randomly selected question per challenge. Build a described relation, match ordered pairs, complete a function, classify a diagram, then identify function properties."),
        Controls,
        Section("SELECT, CONNECT & SUBMIT", "1. Activate the " + Action("Book Statue") + " with the hand. Use View Board to read the Information Board; Back restores gameplay.\n2. At Domain, use the hand or nearby action to open the number choices and select an input.\n3. At the output station, choose an output to make a laser connection. Repeat for every required pair. A relation may connect one input to several outputs.\n4. " + Action("Undo") + " at the output station removes the last editable connection; preset connections stay fixed. Select values again to rebuild it.\n5. For classification/property tasks, inspect preset lasers and select the required answer pedestals. Tap a property again to deselect it.\n6. " + Action("Submit") + " checks the entire current arrangement or pedestal selection. The book only starts or resumes challenges."),
        Section("PROPERTIES & FEEDBACK", "<b>Injective</b> means different inputs use different outputs. <b>Surjective</b> means every codomain value is used. <b>Bijective</b> means both. Follow the board’s extra order clues when completing the function.\n\nOne <b>10-minute shared timer</b> carries across all five challenges. Correct answers pause it; activate the book to continue. Wrong submissions cost <color=#A42828><b>10 seconds</b></color> and preserve connections for correction. Help and practice pause time. At zero, the trial offers a retry."));

    private static string Graphs() => Guide(
        Section("THE TOPIC", "A <b>graph</b> has vertices (points) joined by edges. Here, platforms represent vertices and bridges represent edges. Degree counts incident edges. A path follows connections; a directed path obeys arrows. A cycle returns to its starting vertex."),
        Section("YOUR OBJECTIVE", "Complete five progressively harder challenges, with one random question per type: travel, degree, simple path, directed path and cycle. Read the objective and minimap; vertex labels may be letters or numbers."),
        Controls,
        Section("TRAVEL, SELECT & CONFIRM", "1. Use the hand at the " + Action("Book Statue") + " to start at the named vertex.\n2. Cross a connecting bridge completely to record a move. Available bridges glow orange; crossed bridges turn blue. The recorded route lists visited vertices. Jumping between unconnected platforms does not create an edge.\n3. For degree, inspect the named vertex, choose a number and Confirm. Clear Selection removes that choice.\n4. For a simple path, reach the destination without repeating vertices. Follow arrows on directed paths. For a cycle, visit at least three distinct vertices and return to the start without repeating an intermediate vertex.\n5. Tap " + Action("Confirm") + " at your destination or completed cycle. It checks the whole route. The book can also confirm an active answer. Tap the minimap for a full map."),
        Section("TIME & CORRECTIONS", "One <b>10-minute shared timer</b> starts through the book, pauses between challenges and during Help, then resumes at the next book activation. Wrong confirmation costs <color=#A42828><b>10 seconds</b></color> and keeps the route or selection for review. " + Action("Retry Route") + " returns you to the start and clears the route while preserving time and mistakes; falling also returns you. Help and route resets do not reshuffle questions. At zero, Play Again begins a new run."));

    private static string Trees() => Guide(
        Section("THE TOPIC", "A <b>tree</b> is a connected structure with no cycle. In a rooted tree, the root has no parent; every other node has one parent. Parents link to children, and leaves have no children. <b>Depth</b> counts edges from the root; root depth is zero. <b>Height</b> is the longest root-to-leaf path, also counted in edges."),
        Section("YOUR OBJECTIVE", "Complete five challenges: identify tree roles, build a tree, repair parent links, determine depth or height, and construct a spanning tree. Each challenge selects a question variant from its bank."),
        Controls,
        Section("EDIT THE TREE", "1. Approach the " + Action("Book Statue") + " and use the hand to open the focused board and start the challenge.\n2. Read its objective. For identification, tap the required node. For depth or height, choose a number.\n3. To edit a branch, choose the parent first and the child second, then press " + Action("Connect") + ". Select endpoints and press Remove to delete an editable link. Clear Pick clears only your selection.\n4. Repair tasks require removing the old parent before adding the new one. A spanning tree must include every vertex, stay connected and have no cycle; V vertices need V − 1 edges. Several spanning trees may be valid.\n5. Press " + Action("Confirm") + " on the board to check the whole answer. Exit Board keeps your work; use the book to reopen it."),
        Section("TIME & CORRECTIONS", "One <b>10-minute shared timer</b> runs after book activation, including board editing. Field Guide pauses it. Wrong confirmation costs <color=#A42828><b>10 seconds</b></color> and keeps your connections and selection. Correct answers pause the timer and prepare the next challenge; activate the book again to continue. At zero, Play Again begins a new run. Tap the minimap to enlarge the diagram."));

    private static string Garden() => Guide(
        Section("WELCOME TO LOGIC GARDEN", "Explore the garden, learn the controls and meet <b>Lefyahj</b>, your wizard guide. The introductory conversation prepares your next objective and unlocks access to the Logic Library."),
        Controls,
        Section("YOUR FIRST STEPS", "Practice movement, jumping and tapping the hand button. Then follow the exclamation mark to Lefyahj. Approach him and tap " + Action("Talk with Lefyahj") + " or use the hand to begin the conversation. Read each message before continuing."),
        Section("STUDY & REPLAY", "Follow the displayed objectives to the library, where books provide study material grouped by topic. " + Action("Replay Tutorial") + " can replay Garden Tutorial, Library Tour or Full Tutorial. Replaying practice preserves saved objectives and progress; returning from the library does not repeat completed onboarding."));

    private static string Library() => Guide(
        Section("WELCOME TO LOGIC LIBRARY", "Lefyahj introduces the four study sections: <b>PRELIM, MIDTERMS, PRE-FINALS and FINALS</b>. Each bookshelf groups its available lessons; read the book titles and topic descriptions to choose what to study."),
        Controls,
        Section("READ & EXPLORE", "Finish Lefyahj’s introduction, then use Next during the bookshelf tour. At “Did you get all that?”, Yes restores exploration and No repeats the introduction and tour. Approach a book or bookshelf and choose " + Action("Read") + ". Talk with Lefyahj is reserved for the wizard."),
        Section("REPLAY SAFELY", "Use " + Action("Replay Tutorial") + " to choose Garden Tutorial, Library Tour or Full Tutorial. The shared Skip Tutorial control cancels the tour and restores the normal camera and controls. Replays preserve saved objectives and library access; practice does not award duplicate rewards."));
}
