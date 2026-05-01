using UnityEngine;
using Photon.Pun;
public class SoilMound : MonoBehaviourPun
{
    public SeedItem currentSeed;
    [SerializeField] private Transform snapPoint;
    
    [Header("Truth Table Logic (AND Gate)")]
    [Tooltip("What seed belongs here? True = Glowing, False = Dead Weed")]
    [SerializeField] private bool expectedToBeGlowing; 

    private void OnTriggerEnter(Collider other)
{
    // 1. Ignore if a seed is already planted in this mound
    if (HasSeed()) return;

    // 2. Check if the object entering the Box Collider is a Seed
    if (other.TryGetComponent(out SeedItem incomingSeed))
    {
        // 3. MULTIPLAYER SAFETY: 
        // Only let the Master Client trigger the network event. 
        // This prevents 4 players from sending 4 identical "Snap" RPCs at the exact same time.
        if (PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient) return;

        // 4. Call your existing networked snapping method!
        PlaceSeedNetworked(other.gameObject);
    }
}

    // ---> NEW: The networked method your Player script will call! <---
    public void PlaceSeedNetworked(GameObject seedObj)
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonView seedView = seedObj.GetComponent<PhotonView>();
            if (seedView != null) photonView.RPC("RPC_PlaceSeed", RpcTarget.All, seedView.ViewID);
        }
        else
        {
            PlaceSeed(seedObj); // Offline fallback
        }
    }

    [PunRPC]
    public void RPC_PlaceSeed(int seedViewID)
    {
        PhotonView seedView = PhotonView.Find(seedViewID);
        if (seedView != null)
        {
            PlaceSeed(seedView.gameObject);
        }
    }
    
    public void PlaceSeed(GameObject seedObj)
    {
        SeedItem seed = seedObj.GetComponent<SeedItem>();
        if (seed != null)
        {
            currentSeed = seed;
            seedObj.transform.position = snapPoint.position;
            seedObj.transform.rotation = snapPoint.rotation;

            Rigidbody rb = seedObj.GetComponent<Rigidbody>();
            if (rb != null) 
            {
                rb.isKinematic = true;
                rb.useGravity = false; 
            }

            // Lock it so the player can't pick it back up!
            if (seedObj.TryGetComponent(out GrabbableObject grab)) grab.enabled = false;
        }
    }

    public bool HasSeed() { return currentSeed != null; }

    public bool IsCorrect()
    {
        if (currentSeed == null) return false;
        return currentSeed.isGlowing == expectedToBeGlowing;
    }

    // The Comical "BOING!" Failure Effect
    public void SpitOutSeed()
    {
        if (currentSeed != null)
        {
            GameObject seedObj = currentSeed.gameObject;
            currentSeed = null;

            // Turn grabbing back on
            if (seedObj.TryGetComponent(out GrabbableObject grab)) grab.enabled = true;

            // Turn physics back on and launch it into the air!
            Rigidbody rb = seedObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                
                // ---> FIXED: VelocityChange forces it to launch perfectly regardless of mass! <---
                // 1. Flip a coin! 50% chance to be positive 3 (Right) or negative 3 (Left)
                float randomDirection = Random.value > 0.5f ? 3f : -3f;

                // 2. Apply the force!
                rb.AddForce(Vector3.up * 7f + transform.right * randomDirection, ForceMode.VelocityChange);
            }
        }
    }
    // ---> NEW: Clears the mound's memory cleanly! <---
    public void ClearMound()
    {
        currentSeed = null;
    }
}