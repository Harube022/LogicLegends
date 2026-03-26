using UnityEngine;
using System.Collections.Generic;

public class TutorialORGateBasket : MonoBehaviour
{
    [Header("Outputs (Door or Bulb)")]
    [SerializeField] private GameObject outputOffVisual;
    [SerializeField] private GameObject outputOnVisual;

    [Header("Snapping Setup")]
    [SerializeField] private Transform[] snapPoints;

    // ---> NEW: Explicitly tell the basket WHICH manager to talk to! <---
    [Header("Tutorial Link")]
    [SerializeField] private PuzzleTutorialManager myTutorialManager;

    private List<GameObject> fruitsInBasket = new List<GameObject>();

    private void Start()
    {
        UpdateVisuals();
    }

    public void PlaceFruitInteractive(GameObject fruitObj)
    {
        if (fruitObj.GetComponent<FruitItem>() != null)
        {
            if (fruitsInBasket.Contains(fruitObj)) return;

            fruitsInBasket.Add(fruitObj);

            Rigidbody fruitRb = fruitObj.GetComponent<Rigidbody>();
            if (fruitRb != null) fruitRb.isKinematic = true; 

            int snapIndex = fruitsInBasket.Count - 1;
            if (snapPoints != null && snapIndex < snapPoints.Length)
            {
                if (snapPoints[snapIndex] != null) 
                {
                    fruitObj.transform.position = snapPoints[snapIndex].position;
                    fruitObj.transform.rotation = snapPoints[snapIndex].rotation; 
                }
            }

            GrabbableObject grabbable = fruitObj.GetComponent<GrabbableObject>();
            if (grabbable != null)
            {
                grabbable.SetTutorialBasket(this);
            }

            UpdateVisuals();

            // ---> FIXED: Talk ONLY to our assigned manager! <---
            if (myTutorialManager != null) 
            {
                myTutorialManager.AdvanceTutorial(this.transform); 
            }
        }
    }

    public void RemoveFruitExplicit(GameObject fruitObj)
    {
        if (fruitsInBasket.Contains(fruitObj))
        {
            fruitsInBasket.Remove(fruitObj);
            UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        bool isPowered = fruitsInBasket.Count > 0;

        if (outputOnVisual != null) outputOnVisual.SetActive(isPowered);
        if (outputOffVisual != null) outputOffVisual.SetActive(!isPowered);
    }
}