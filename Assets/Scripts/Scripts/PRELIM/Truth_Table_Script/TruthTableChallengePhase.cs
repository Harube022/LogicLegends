using System;
using UnityEngine;

// The TRUTIBOL question banks. Keep the displayed expressions separate from the
// six house-door pop quizzes, which have their own question bank.
public static class TruthTableChallengeQuestions
{
    public sealed class Question
    {
        public readonly string Expression;
        private readonly Func<bool, bool, bool> evaluate;

        public Question(string expression, Func<bool, bool, bool> evaluate)
        {
            Expression = expression;
            this.evaluate = evaluate;
        }

        public bool Evaluate(bool p, bool q) { return evaluate(p, q); }

        // Matches the P/Q rows already used by TRUTIBOL: TT, TF, FT, FF.
        public bool EvaluateRow(int row)
        {
            bool p = row == 0 || row == 1;
            bool q = row == 0 || row == 2;
            return Evaluate(p, q);
        }
    }

    private static bool Implies(bool antecedent, bool consequent)
    {
        return !antecedent || consequent;
    }

    public static readonly Question[][] Banks =
    {
        new[]
        {
            new Question("P ∧ Q", (p, q) => p && q),
            new Question("P ∨ Q", (p, q) => p || q),
            new Question("P ↔ Q", (p, q) => p == q),
            new Question("P ⊕ Q", (p, q) => p != q),
            new Question("P → Q", (p, q) => Implies(p, q)),
            new Question("Q ∧ P", (p, q) => q && p),
            new Question("Q ∨ P", (p, q) => q || p),
            new Question("Q ↔ P", (p, q) => q == p),
            new Question("Q ⊕ P", (p, q) => q != p),
            new Question("Q → P", (p, q) => Implies(q, p))
        },
        new[]
        {
            new Question("P ∧ (P ∨ Q)", (p, q) => p && (p || q)),
            new Question("P ∨ (P ∧ Q)", (p, q) => p || (p && q)),
            new Question("P → (P ∨ Q)", (p, q) => Implies(p, p || q)),
            new Question("P → (P ∧ Q)", (p, q) => Implies(p, p && q)),
            new Question("(P ∨ Q) ∧ P", (p, q) => (p || q) && p),
            new Question("(P ∧ Q) ∨ Q", (p, q) => (p && q) || q),
            new Question("(P → Q) ∧ P", (p, q) => Implies(p, q) && p),
            new Question("(P ↔ Q) ∨ P", (p, q) => (p == q) || p),
            new Question("(P ⊕ Q) ∧ P", (p, q) => (p != q) && p),
            new Question("(P ∨ Q) → Q", (p, q) => Implies(p || q, q))
        },
        new[]
        {
            new Question("(P ∨ Q) ∧ (Q ∨ P)", (p, q) => (p || q) && (q || p)),
            new Question("(P ∧ Q) ∨ (Q ∧ P)", (p, q) => (p && q) || (q && p)),
            new Question("(P → Q) ∧ (Q → P)", (p, q) => Implies(p, q) && Implies(q, p)),
            new Question("(P → Q) ∨ (Q → P)", (p, q) => Implies(p, q) || Implies(q, p)),
            new Question("(P ↔ Q) ∧ (P ⊕ Q)", (p, q) => (p == q) && (p != q)),
            new Question("(P ∨ Q) → (P ∧ Q)", (p, q) => Implies(p || q, p && q)),
            new Question("(P ∧ Q) → (P ∨ Q)", (p, q) => Implies(p && q, p || q)),
            new Question("(P ⊕ Q) → (P ↔ Q)", (p, q) => Implies(p != q, p == q)),
            new Question("(P → Q) ↔ (P ∧ Q)", (p, q) => Implies(p, q) == (p && q)),
            new Question("[(P ∨ Q) ∧ Q] → P", (p, q) => Implies((p || q) && q, p))
        }
    };
}

// One session selects three distinct entries from each difficulty and keeps
// them fixed through retries, door resets and block returns.
public sealed class TruthTableChallengePhase : IPuzzlePhase
{
    private static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard" };
    private readonly DynamicLogicPuzzle puzzle;
    private readonly TruthTableChallengeQuestions.Question[,] selected =
        new TruthTableChallengeQuestions.Question[3, 3];
    private readonly TruthBlock[] placedBlocks = new TruthBlock[4];
    private readonly bool[] currentAnswers = new bool[4];
    private int challengeIndex;
    private int columnIndex;
    private int currentRow;

    public int ChallengeNumber { get { return challengeIndex + 1; } }
    public int ColumnNumber { get { return columnIndex + 1; } }
    public bool CanSubmitColumn { get { return currentRow == placedBlocks.Length; } }

    public void DebugCompleteCurrentChallenge()
    {
        if (puzzle.PuzzleCompleted) return;
        puzzle.ClearActiveBlocksForDebug();
        Array.Clear(placedBlocks, 0, placedBlocks.Length);
        currentRow = 0;
        if (challengeIndex == 2)
        {
            columnIndex = 2;
            puzzle.CompletePuzzle();
            return;
        }

        challengeIndex++;
        columnIndex = 0;
        UpdateHeaders();
        UpdateMasking();
        UpdateProgress();
        puzzle.UpdatePlacementIndicator();
        SpawnCurrentBlocks();
        puzzle.PrepareNextChallenge();
    }

    public TruthTableChallengePhase(DynamicLogicPuzzle puzzle)
    {
        this.puzzle = puzzle;
        for (int difficulty = 0; difficulty < 3; difficulty++)
        {
            var bank = TruthTableChallengeQuestions.Banks[difficulty];
            var indices = new int[bank.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            for (int column = 0; column < 3; column++)
            {
                int pick = UnityEngine.Random.Range(column, indices.Length);
                int previous = indices[column];
                indices[column] = indices[pick];
                indices[pick] = previous;
                selected[difficulty, column] = bank[indices[column]];
            }
        }
    }

    public string GetSelectedExpression(int challenge, int column)
    {
        return selected[challenge, column].Expression;
    }

    public void StartPhase()
    {
        challengeIndex = 0;
        columnIndex = 0;
        currentRow = 0;
        Array.Clear(placedBlocks, 0, placedBlocks.Length);
        UpdateHeaders();
        UpdateMasking();
        UpdateProgress();
        SpawnCurrentBlocks();
    }

    public Transform GetActiveSnapPoint()
    {
        if (puzzle.PuzzleCompleted || currentRow >= placedBlocks.Length) return null;
        Transform[] points = puzzle.GetColumnSnapPoints(columnIndex);
        return points != null && currentRow < points.Length ? points[currentRow] : null;
    }

    public void HandleTryPlace(TruthBlock block, int requestedColumn)
    {
        Transform target = GetActiveSnapPoint();
        if (requestedColumn != columnIndex || target == null)
        {
            puzzle.RejectInvalidPlacement(block);
            return;
        }
        puzzle.LockBlock(block, target);
        placedBlocks[currentRow++] = block;
        puzzle.UpdatePlacementIndicator();
    }

    public bool SubmitColumn()
    {
        if (!CanSubmitColumn) return false;
        Transform[] points = puzzle.GetColumnSnapPoints(columnIndex);
        bool correct = true;
        for (int row = 0; row < placedBlocks.Length; row++)
        {
            TruthBlock block = placedBlocks[row];
            if (block == null || block.value != currentAnswers[row] ||
                points == null || row >= points.Length || points[row] == null ||
                Vector3.Distance(block.transform.position, points[row].position) > 0.2f)
                correct = false;
        }

        if (!correct)
        {
            // Replace this attempt's four blocks from the same answers used by
            // validation. The selected question remains unchanged on a retry.
            puzzle.RespawnBlocksForRetry(currentAnswers);
            Array.Clear(placedBlocks, 0, placedBlocks.Length);
            currentRow = 0;
            puzzle.UpdatePlacementIndicator();
            return false;
        }

        Advance();
        return true;
    }

    private void Advance()
    {
        currentRow = 0;
        Array.Clear(placedBlocks, 0, placedBlocks.Length);
        columnIndex++;
        bool nextChallenge = false;
        if (columnIndex == 3)
        {
            if (challengeIndex == 2)
            {
                // CompletePuzzle refreshes the current phase's headers and
                // masking, so retain the final valid question indices.
                columnIndex = 2;
                puzzle.CompletePuzzle();
                return;
            }
            challengeIndex++;
            columnIndex = 0;
            nextChallenge = true;
            puzzle.ClearAllSnappedBlocks();
        }

        UpdateHeaders();
        UpdateMasking();
        UpdateProgress();
        puzzle.UpdatePlacementIndicator();
        SpawnCurrentBlocks();
        if (nextChallenge) puzzle.PrepareNextChallenge();
    }

    private void SpawnCurrentBlocks()
    {
        for (int row = 0; row < currentAnswers.Length; row++)
            currentAnswers[row] = selected[challengeIndex, columnIndex].EvaluateRow(row);
        puzzle.SpawnBlocksForTruthValues(currentAnswers);
    }

    private void UpdateProgress()
    {
        puzzle.SetChallengeProgress(challengeIndex + 1, DifficultyNames[challengeIndex],
            columnIndex + 1, selected[challengeIndex, columnIndex].Expression);
    }

    public void UpdateHeaders()
    {
        for (int column = 0; column < 3; column++)
            puzzle.SetHeaderLabel(column, selected[challengeIndex, column].Expression);
    }

    public void UpdateMasking()
    {
        for (int column = 0; column < 3; column++)
        {
            if (puzzle.PuzzleCompleted || column == columnIndex)
                puzzle.UpdateBarrier(column, false, null);
            else if (column < columnIndex)
                puzzle.UpdateBarrier(column, true, puzzle.CompletedMaterial);
            else
                puzzle.UpdateBarrier(column, true, puzzle.LockedMaterial);
        }
    }
}
