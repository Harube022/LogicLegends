using UnityEngine;
using System.Collections.Generic;

public class TutorialORGateBasket : MonoBehaviour
{
    [Header("Outputs (Door or Bulb)")]
    [SerializeField] private GameObject outputOffVisual;
    [SerializeField] private GameObject outputOnVisual;

    [Header("Snapping Setup")]
    [SerializeField] private Transform[] snapPoints;

    private List<GameObject> fruitsInBasket = new List<GameObject>();

    private void Start()
    {
        UpdateVisuals();
    }

    // ---> NEW: The foolproof method the Player script will call directly! <---
    public void PlaceFruitInteractive(GameObject fruitObj)
    {
        if (fruitObj.GetComponent<FruitItem>() != null)
        {
            // Prevent double-counting
            if (fruitsInBasket.Contains(fruitObj)) return;

            fruitsInBasket.Add(fruitObj);

            // Turn off physics so it freezes in place
            Rigidbody fruitRb = fruitObj.GetComponent<Rigidbody>();
            if (fruitRb != null)
            {
                fruitRb.isKinematic = true; 
            }

            // Snap it precisely to the empty point
            int snapIndex = fruitsInBasket.Count - 1;
            if (snapPoints != null && snapIndex < snapPoints.Length)
            {
                if (snapPoints[snapIndex] != null) // Safety check
                {
                    fruitObj.transform.position = snapPoints[snapIndex].position;
                    fruitObj.transform.rotation = snapPoints[snapIndex].rotation; 
                }
            }

            // ---> NEW: Tell the fruit it is inside this basket! <---
            GrabbableObject grabbable = fruitObj.GetComponent<GrabbableObject>();
            if (grabbable != null)
            {
                grabbable.SetTutorialBasket(this);
            }

            UpdateVisuals();

            // Advance the tutorial arrow!
            PuzzleTutorialManager tutorialManager = FindFirstObjectByType<PuzzleTutorialManager>();
            if (tutorialManager != null)
            {
                tutorialManager.AdvanceTutorial(this.transform); 
            }
        }
    }

    // ---> NEW: The GrabbableObject will call this safely when the player grabs it! <---
    public void RemoveFruitExplicit(GameObject fruitObj)
    {
        if (fruitsInBasket.Contains(fruitObj))
        {
            fruitsInBasket.Remove(fruitObj);
            UpdateVisuals();
        }
    }

    // (We keep this just in case they push a fruit in by walking into it)
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<FruitItem>() != null)
        {
            PlaceFruitInteractive(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<FruitItem>() != null && fruitsInBasket.Contains(other.gameObject))
        {
            fruitsInBasket.Remove(other.gameObject);
            
            Rigidbody fruitRb = other.GetComponent<Rigidbody>();
            if (fruitRb != null)
            {
                fruitRb.isKinematic = false;
            }

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