// using UnityEngine;
// using System;

// [System.Serializable]
// public class InventorySlot
// {
//     public bool isEmpty = true;
//     public bool blockValue; 
//     public int count = 0;
//     public const int MAX_STACK = 2; 

//     // NEW: Keep track of the physical block object assigned to this slot
//     public TruthBlock physicalBlockReference;

//     public bool TryAdd(bool value, TruthBlock blockInstance)
//     {
//         if (isEmpty)
//         {
//             blockValue = value;
//             isEmpty = false;
//             count = 1;
//             physicalBlockReference = blockInstance; // Save the reference
//             return true;
//         }

//         if (blockValue == value && count < MAX_STACK)
//         {
//             count++;
//             // If stacking, we can hide extra duplicates or manage them, 
//             // but let's keep the reference to the main one being held
//             if (physicalBlockReference == null) physicalBlockReference = blockInstance;
//             return true;
//         }

//         return false; 
//     }

//     public void Consume()
//     {
//         if (isEmpty) return;

//         count--;
//         if (count <= 0)
//         {
//             isEmpty = true;
//             count = 0;
//             physicalBlockReference = null;
//         }
//     }
// }

using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class InventorySlot
{
    public bool isEmpty = true;
    public bool blockValue; 
    public int count = 0;
    public const int MAX_STACK = 2; 

    // Track every individual block game object inside this specific stack
    public List<TruthBlock> physicalBlocks = new List<TruthBlock>();

    public bool TryAdd(bool value, TruthBlock blockInstance)
    {
        if (isEmpty)
        {
            blockValue = value;
            isEmpty = false;
            count = 1;
            physicalBlocks.Clear();
            physicalBlocks.Add(blockInstance);
            return true;
        }

        if (blockValue == value && count < MAX_STACK)
        {
            count++;
            if (!physicalBlocks.Contains(blockInstance))
            {
                physicalBlocks.Add(blockInstance);
            }
            return true;
        }

        return false; 
    }

    public TruthBlock Consume()
    {
        if (isEmpty || physicalBlocks.Count == 0) return null;

        // Pop the top block off the stack
        TruthBlock removedBlock = physicalBlocks[physicalBlocks.Count - 1];
        physicalBlocks.RemoveAt(physicalBlocks.Count - 1);
        
        count = physicalBlocks.Count;
        if (count <= 0)
        {
            isEmpty = true;
            count = 0;
        }
        return removedBlock;
    }
}