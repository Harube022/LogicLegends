using UnityEngine;
using System.Collections.Generic;

public class HardPuzzlePhase : IPuzzlePhase
{
    private DynamicLogicPuzzle puzzle;
    
    // Track the progressive logic structure
    private DynamicLogicType col1Logic;
    private DynamicLogicType col2Logic;
    private ComplexLogicExpression col3Logic;
    
    private int currentColumnIndex = 0;
    private int currentRow = 0;

    // NEW: Track the current round
    private int currentRound = 1;

    public HardPuzzlePhase(DynamicLogicPuzzle puzzle)
    {
        this.puzzle = puzzle;
    }

    public void StartPhase()
    {
        GenerateRandomLogics();
        currentColumnIndex = 0;
        currentRow = 0;

        puzzle.ClearAllSnappedBlocks();
        UpdateMasking();
        UpdateHeaders();
        
        // Start by spawning simple blocks for Column 1
        puzzle.SpawnHardBlocksSimple(col1Logic);
    }

    private void GenerateRandomLogics()
    {
        System.Array typesArray = System.Enum.GetValues(typeof(DynamicLogicType));
        DynamicLogicType[] types = (DynamicLogicType[])typesArray;

        // 1. Pick simple logic for Col 1
        col1Logic = types[Random.Range(0, types.Length)];
        
        // 2. Pick simple logic for Col 2 (ensure it's different from Col 1)
        do {
            col2Logic = types[Random.Range(0, types.Length)];
        } while (col2Logic == col1Logic);

        // 3. Pick a main operator to bind them together for Col 3
        DynamicLogicType mainOperator = types[Random.Range(0, types.Length)];

        col3Logic = new ComplexLogicExpression
        {
            LeftSub = col1Logic,
            RightSub = col2Logic,
            MainOperator = mainOperator
        };
    }

    public Transform GetActiveSnapPoint()
    {
        if (puzzle.PuzzleCompleted) return null;
        Transform[] activeArray = puzzle.GetColumnSnapPoints(currentColumnIndex);

        if (activeArray != null && currentRow >= 0 && currentRow < activeArray.Length)
            return activeArray[currentRow];

        return null;
    }

    public void HandleTryPlace(TruthBlock block, int columnIndex)
    {
        if (columnIndex != currentColumnIndex)
        {
            block.ReturnToOrigin(true);
            return;
        }

        bool p = (currentRow == 0 || currentRow == 1);
        bool q = (currentRow == 0 || currentRow == 2);

        bool expectedValue = false;

        // Evaluate based on which column the player is currently on
        if (currentColumnIndex == 0)
            expectedValue = LogicUtility.EvaluateLogic(col1Logic, p, q);
        else if (currentColumnIndex == 1)
            expectedValue = LogicUtility.EvaluateLogic(col2Logic, p, q);
        else if (currentColumnIndex == 2)
            expectedValue = LogicUtility.EvaluateComplexLogic(col3Logic, p, q);

        if (block.value != expectedValue)
        {
            block.ReturnToOrigin(true);
            return;
        }

        Transform targetSnap = puzzle.GetColumnSnapPoints(currentColumnIndex)[currentRow];
        puzzle.LockBlock(block, targetSnap);
        AdvancePhase();
    }

    private void AdvancePhase()
    {
        currentRow++;
        puzzle.UpdatePlacementIndicator();

        if (currentRow < 4) return;

        currentRow = 0;
        currentColumnIndex++;

        // CHANGED: Check against the required rounds before completing the puzzle
        if (currentColumnIndex >= 3)
        {
            if (currentRound < puzzle.HardModeRequiredRounds)
            {
                Debug.Log($"Hard Mode Round {currentRound} finished! Generating Round {currentRound + 1}...");
                currentRound++;
                
                // Restart the phase to clear blocks and generate new random logic!
                StartPhase(); 
            }
            else
            {
                Debug.Log("All Hard Mode rounds completed! Puzzle entirely finished.");
                puzzle.CompletePuzzle();
            }
            return;
        }

        UpdateMasking();
        UpdateHeaders();
        puzzle.UpdatePlacementIndicator();
        
        // Spawn the correct block types based on the column we just advanced to
        if (currentColumnIndex == 1)
            puzzle.SpawnHardBlocksSimple(col2Logic);
        else if (currentColumnIndex == 2)
            puzzle.SpawnHardBlocksComplex(col3Logic);
    }

    public void UpdateHeaders()
    {
        puzzle.SetHeaderLabel(0, LogicUtility.GetLogicSymbolString(col1Logic, "P", "Q"));
        puzzle.SetHeaderLabel(1, LogicUtility.GetLogicSymbolString(col2Logic, "P", "Q"));
        puzzle.SetHeaderLabel(2, LogicUtility.GetComplexLogicString(col3Logic));
    }

    public void UpdateMasking()
    {
        for (int i = 0; i < 3; i++)
        {
            if (puzzle.PuzzleCompleted)
            {
                // Keep all columns fully visible when the entire puzzle is finished
                puzzle.UpdateBarrier(i, false, null);
            }
            else if (currentColumnIndex >= i) 
            {
                // CURRENT AND PREVIOUS COLUMNS:
                // Disable the barrier completely so the player can see their previous answers.
                // (Using ">=" handles both the active column and any past columns)
                puzzle.UpdateBarrier(i, false, null);
            }
            else
            {
                // FUTURE COLUMNS:
                // Keep these covered with the LockedMaterial until the player reaches them
                puzzle.UpdateBarrier(i, true, puzzle.LockedMaterial);
            }
        }
    }
}