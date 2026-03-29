using UnityEngine;
using System.Collections.Generic;

public class DynamicObjectiveIndicator : MonoBehaviour
{
    [Header("Arrow Setup")]
    [Tooltip("Drag your WorldPointer PREFAB here from your Project window (Not the Hierarchy!)")]
    [SerializeField] private WorldIndicator arrowPrefab;

    [Header("The Targets")]
    [Tooltip("Drag all the objects you want to point at into this list!")]
    [SerializeField] private Transform[] targets;

    // We keep a list of the arrows we spawn just in case you ever want to turn them off later!
    private List<WorldIndicator> spawnedArrows = new List<WorldIndicator>();
    private bool hasTriggered = false;

    // ---> The Wizard calls this! <---
    public void ActivateArrows()
    {
        if (hasTriggered || arrowPrefab == null) return;
        hasTriggered = true;

        foreach (Transform target in targets)
        {
            if (target != null)
            {
                // Clone a brand new arrow from the prefab
                WorldIndicator newArrow = Instantiate(arrowPrefab);
                
                // Tell the new arrow to point at this specific target
                newArrow.PointAt(target);
                
                // Add it to our memory list
                spawnedArrows.Add(newArrow);
            }
        }
    }

    // ---> NEW: This destroys the arrows and resets the switch! <---
    public void ResetArrows()
    {
        foreach (WorldIndicator arrow in spawnedArrows)
        {
            // We destroy the clone so they don't pile up and lag your game
            if (arrow != null) Destroy(arrow.gameObject); 
        }
        
        spawnedArrows.Clear();
        hasTriggered = false; // This allows the Wizard to spawn them again!
    }
}