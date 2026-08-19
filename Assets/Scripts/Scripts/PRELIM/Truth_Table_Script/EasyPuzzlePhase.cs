using UnityEngine;

public class EasyPuzzlePhase : IPuzzlePhase
{
    private DynamicLogicPuzzle puzzle;
    private int easyModeStep = 0;
    private int currentRow = 0;

    public EasyPuzzlePhase(DynamicLogicPuzzle puzzle)
    {
        this.puzzle = puzzle;
    }

    public void StartPhase()
    {
        easyModeStep = 0;
        currentRow = 0;
        UpdateMasking();
        UpdateHeaders();
        puzzle.SpawnEasyBlocks(easyModeStep);
    }

    public Transform GetActiveSnapPoint()
    {
        if (puzzle.PuzzleCompleted) return null;
        Transform[] activeArray = puzzle.GetColumnSnapPoints(GetExpectedColumnIndex());
        
        if (activeArray != null && currentRow >= 0 && currentRow < activeArray.Length)
            return activeArray[currentRow];
            
        return null;
    }

    public void HandleTryPlace(TruthBlock block, int columnIndex)
    {
        int expectedCol = GetExpectedColumnIndex();
        if (columnIndex != expectedCol)
        {
            block.ReturnToOrigin(true);
            return;
        }

        bool p = (currentRow == 0 || currentRow == 1);
        bool q = (currentRow == 0 || currentRow == 2);

        // Map step directly to logic type for cleaner evaluation
        DynamicLogicType expectedType = (DynamicLogicType)easyModeStep;
        bool correctValue = LogicUtility.EvaluateLogic(expectedType, p, q);

        if (block.value != correctValue)
        {
            block.ReturnToOrigin(true);
            return;
        }

        Transform targetSnap = puzzle.GetColumnSnapPoints(columnIndex)[currentRow];
        puzzle.LockBlock(block, targetSnap);
        AdvancePhase();
    }

    private void AdvancePhase()
    {
        currentRow++;
        puzzle.UpdatePlacementIndicator();

        if (currentRow < 4) return;

        currentRow = 0;
        easyModeStep++;

        if (easyModeStep == 3)
        {
            puzzle.ClearColumnsOfBlocks(0, 1); // Clear columns 1 and 2
        }
        else if (easyModeStep >= 5)
        {
            Debug.Log("Easy Mode Finished! Transitioning to Hard Mode...");
            puzzle.SwitchToHardMode();
            return;
        }

        UpdateMasking();
        UpdateHeaders();
        puzzle.UpdatePlacementIndicator();
        puzzle.SpawnEasyBlocks(easyModeStep);
    }

    private int GetExpectedColumnIndex()
    {
        switch (easyModeStep)
        {
            case 0: return 0; // Col 1
            case 1: return 1; // Col 2
            case 2: return 2; // Col 3
            case 3: return 0; // Col 1
            case 4: return 1; // Col 2
            default: return 0;
        }
    }

    public void UpdateHeaders()
    {
        if (easyModeStep >= 3)
        {
            puzzle.SetHeaderLabel(0, "P → Q");
            puzzle.SetHeaderLabel(1, "P ↔ Q");
        }
        else
        {
            puzzle.SetHeaderLabel(0, "P ∧ Q");
            puzzle.SetHeaderLabel(1, "P ∨ Q");
            puzzle.SetHeaderLabel(2, "P ⊕ Q");
        }
    }

    public void UpdateMasking()
    {
        if (puzzle.PuzzleCompleted)
        {
            SetAllBarriers(true, puzzle.CompletedMaterial);
            return;
        }

        SetAllBarriers(true, puzzle.LockedMaterial);

        switch (easyModeStep)
        {
            case 0: puzzle.UpdateBarrier(0, false, null); break;
            case 1: 
                puzzle.UpdateBarrier(0, true, puzzle.CompletedMaterial); 
                puzzle.UpdateBarrier(1, false, null); 
                break;
            case 2: 
                puzzle.UpdateBarrier(0, true, puzzle.CompletedMaterial); 
                puzzle.UpdateBarrier(1, true, puzzle.CompletedMaterial); 
                puzzle.UpdateBarrier(2, false, null); 
                break;
            case 3: 
                puzzle.UpdateBarrier(0, false, null); 
                puzzle.UpdateBarrier(2, true, puzzle.CompletedMaterial); 
                break;
            case 4: 
                puzzle.UpdateBarrier(0, true, puzzle.CompletedMaterial); 
                puzzle.UpdateBarrier(1, false, null); 
                puzzle.UpdateBarrier(2, true, puzzle.CompletedMaterial); 
                break;
        }
    }

    private void SetAllBarriers(bool state, Material mat)
    {
        puzzle.UpdateBarrier(0, state, mat);
        puzzle.UpdateBarrier(1, state, mat);
        puzzle.UpdateBarrier(2, state, mat);
    }
}