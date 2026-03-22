using UnityEngine;
using Photon.Pun;

public class FruitBasket : MonoBehaviour
{
    public FruitItem currentFruit;
    [Tooltip("Place an empty GameObject inside the basket and drag it here")]
    public Transform fruitSnapPoint;

    private PhotonView view; // 2. Add PhotonView reference

    private void Awake()
    {
        view = GetComponent<PhotonView>();
    }

    public void PlaceFruit(GameObject fruitObj)
    {
        PhotonView fruitView = fruitObj.GetComponent<PhotonView>();
        
        if (view != null && fruitView != null && PhotonNetwork.InRoom)
        {
            // 3. Send the network ID of the fruit so everyone knows WHICH fruit to snap!
            view.RPC("RPC_PlaceFruit", RpcTarget.All, fruitView.ViewID);
        }
        else
        {
            // OFFLINE: Just snap the object directly without needing Photon lookups!
            PerformFruitSnap(fruitObj);
        }
    }

    [PunRPC]
    public void RPC_PlaceFruit(int fruitViewID)
    {
        // 4. Find the fruit object using the ID we sent over the network
        PhotonView fruitView = PhotonNetwork.GetPhotonView(fruitViewID);
        if (fruitView != null)
        {
            PerformFruitSnap(fruitView.gameObject);
        }
    }

    // ---> NEW HELPER METHOD: Holds the actual physical snapping code <---
    private void PerformFruitSnap(GameObject fruitObj)
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

    // --- SYNC REMOVAL AND CLEARING ---
    public void RemoveFruit()
    {
        if (view != null && PhotonNetwork.InRoom) view.RPC("RPC_RemoveFruit", RpcTarget.All);
        else RPC_RemoveFruit();
    }

    [PunRPC]
    public void RPC_RemoveFruit()
    {
        currentFruit = null;
    }

    public void ClearBasket()
    {
        if (view != null && PhotonNetwork.InRoom) view.RPC("RPC_ClearBasket", RpcTarget.All);
        else RPC_ClearBasket();
    }

    [PunRPC]
    public void RPC_ClearBasket()
    {
        if (currentFruit != null)
        {
            // 1. Tell the fruit's own script to handle the teleport and physics!
            ResettableObject resettable = currentFruit.GetComponent<ResettableObject>();
            if (resettable != null) 
            {
                resettable.ResetPosition();
            }

            // 2. Clear the basket's memory completely
            currentFruit = null;
        }
    }
}