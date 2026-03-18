using UnityEngine;

public class SoilMound : MonoBehaviour
{
    public SeedItem currentSeed;
    [SerializeField] private Transform snapPoint;
    
    [Header("Truth Table Logic (AND Gate)")]
    [Tooltip("What seed belongs here? True = Glowing, False = Dead Weed")]
    [SerializeField] private bool expectedToBeGlowing; 

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
                rb.AddForce(Vector3.up * 7f + transform.forward * -3f, ForceMode.VelocityChange); 
            }
        }
    }
}