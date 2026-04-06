using UnityEngine;

public class GateIndicator : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private MeshRenderer indicatorRenderer;
    [SerializeField] private Material unpoweredMaterial; 
    [SerializeField] private Material poweredMaterial;

    // ---> NEW: What it looks like when hit by a False beam! <---
    [SerializeField] private Material failedMaterial;   

    [Header("Puzzle Link")]
    [Tooltip("Drag the object holding the TruthTableGate script here")]
    [SerializeField] private TruthTableGate myGateManager;

    // ---> NEW: Audio Settings <---
    [Header("Audio Settings")]
    [SerializeField] private AudioClip successElectricClip;
    [SerializeField] private AudioClip errorElectricClip;
    [SerializeField, Range(0f, 1f)] private float audioVolume = 0.8f;

    [Header("State")]
    public bool isPoweredCorrectly = false;

    public void SetPowerState(bool isHitByBeam, bool isGreenBeam)
    {
        // ---> NEW: If the puzzle is already locked/failing, ignore new beams! <---
        if (myGateManager != null && myGateManager.isLocked) return;

        if (isHitByBeam)
        {
            if (isGreenBeam)
            {
                if (!isPoweredCorrectly) // Only change if it wasn't already green
                {
                    indicatorRenderer.material = poweredMaterial;
                    isPoweredCorrectly = true;

                    // ---> NEW: Play Success Sound <---
                    if (successElectricClip != null) Spawn3DAudio(successElectricClip);
                    
                    // Tell the gate to check if we won!
                    if (myGateManager != null) myGateManager.EvaluateGate();
                }
            }
            else
            {
                // ---> NEW: HIT BY A RED BEAM! Trigger the penalty! <---
                indicatorRenderer.material = failedMaterial;
                isPoweredCorrectly = false;

                // ---> NEW: Play Error Sound <---
                if (errorElectricClip != null) Spawn3DAudio(errorElectricClip);
                
                if (myGateManager != null) myGateManager.TriggerFailSequence();
            }
        }
        else
        {
            indicatorRenderer.material = unpoweredMaterial;
            isPoweredCorrectly = false;
        }
    }
    
    // ---> NEW: Forces the crystal to wipe its color back to default! <---
    public void ResetIndicator()
    {
        isPoweredCorrectly = false;
        if (indicatorRenderer != null) indicatorRenderer.material = unpoweredMaterial;
    }
    
    private void Spawn3DAudio(AudioClip clip)
    {
        // Spawn an invisible object right at the indicator crystal's position
        GameObject audioObj = new GameObject("TempIndicatorAudio");
        audioObj.transform.position = transform.position;

        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = audioVolume; 
        
        // Slight random pitch to make it sound organic
        source.pitch = Random.Range(0.95f, 1.05f);
        
        // 3D Audio setup
        source.spatialBlend = 1f; 
        source.minDistance = 3f;
        source.maxDistance = 15f; 

        source.Play();
        
        // Destroy after playing
        Destroy(audioObj, clip.length + 0.1f);
    }
}