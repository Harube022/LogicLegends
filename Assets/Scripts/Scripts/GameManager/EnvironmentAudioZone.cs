using UnityEngine;
using System.Collections.Generic;

public class EnvironmentAudioZone : MonoBehaviour
{
    [Header("Audio Layers")]
    [SerializeField] private List<string> layerNames = new List<string>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        //  NEW: clear previous zone audio
        EnvironmentAudioManager.Instance.DeactivateAllLayers();

        // EXISTING BEHAVIOR (unchanged)
        foreach (var layer in layerNames)
        {
            EnvironmentAudioManager.Instance.ActivateLayer(layer);
        }
    }
}