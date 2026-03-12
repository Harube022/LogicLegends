using UnityEngine;

public class FruitBasket : MonoBehaviour
{
    public FruitItem currentFruit;
    [Tooltip("Place an empty GameObject inside the basket and drag it here")]
    public Transform fruitSnapPoint;

    public void PlaceFruit(GameObject fruitObj)
    {
        FruitItem fruit = fruitObj.GetComponent<FruitItem>();
        if (fruit != null)
        {
            currentFruit = fruit;
            
            // Snap it into the basket
            fruitObj.transform.position = fruitSnapPoint.position;
            fruitObj.transform.rotation = fruitSnapPoint.rotation;

            // Turn off physics so it doesn't fall through the bottom!
            Rigidbody rb = fruitObj.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // ---> NEW: Tell the grabbable script that it is currently in this basket! <---
            GrabbableObject grabbable = fruitObj.GetComponent<GrabbableObject>();
            if (grabbable != null)
            {
                grabbable.SetBasket(this);
            }
        }
    }

    public bool HasFruit() { return currentFruit != null; }

    // THE ACTUAL DISCRETE MATH! (Red OR Berry)
    public bool CheckORGate()
    {
        if (currentFruit == null) return false;
        return currentFruit.isRed || currentFruit.isBerry;
    }

    public void RemoveFruit()
    {
        currentFruit = null;
    }

    public void ClearBasket()
    {
        if (currentFruit != null)
        {
            // 1. Teleport the fruit safely back to the tree FIRST
            ResettableObject resettable = currentFruit.GetComponent<ResettableObject>();
            if (resettable != null) resettable.ResetPosition();

            // 2. THEN turn physics/gravity back on so it can fall naturally
            Rigidbody rb = currentFruit.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            // 3. Clear the basket's memory
            currentFruit = null;
        }
    }
}