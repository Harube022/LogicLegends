using UnityEngine;
using System.Collections.Generic;

public class EnvironmentAudioManager : MonoBehaviour
{
    public static EnvironmentAudioManager Instance;

    [System.Serializable]
    public class AudioLayer
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        [HideInInspector] public AudioSource source;
        [HideInInspector] public float targetVolume;
        [HideInInspector] public int activeCount = 0;
    }

    [Header("Audio Layers")]
    [SerializeField] private List<AudioLayer> layers = new List<AudioLayer>();

    [Header("Settings")]
    [SerializeField] private float fadeSpeed = 2f;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Create AudioSources
        foreach (var layer in layers)
        {
            GameObject obj = new GameObject("Audio_" + layer.name);
            obj.transform.parent = transform;

            AudioSource src = obj.AddComponent<AudioSource>();
            src.clip = layer.clip;
            src.loop = true;
            src.playOnAwake = false;
            src.volume = 0f;
            src.spatialBlend = 0f; // BGM = 2D

            layer.source = src;
            layer.targetVolume = 0f;
        }
    }

    private void Update()
    {
        foreach (var layer in layers)
        {
            if (layer.source == null) continue;

            // Start if needed
            if (!layer.source.isPlaying && layer.targetVolume > 0f)
                layer.source.Play();

            // Smooth fade
            layer.source.volume = Mathf.Lerp(
                layer.source.volume,
                layer.targetVolume * layer.volume,
                Time.deltaTime * fadeSpeed
            );

            // Stop if silent
            if (layer.source.volume < 0.01f && layer.targetVolume == 0f)
                layer.source.Stop();
        }
    }

    // Activate a layer
    public void ActivateLayer(string layerName)
    {
        var layer = layers.Find(l => l.name == layerName);
        if (layer != null)
        {
            layer.activeCount++;
            layer.targetVolume = 1f;
        }
    }

    public void DeactivateLayer(string layerName)
    {
        var layer = layers.Find(l => l.name == layerName);
        if (layer != null)
        {
            layer.activeCount = Mathf.Max(0, layer.activeCount - 1);

            if (layer.activeCount == 0)
                layer.targetVolume = 0f;
        }
    }

    public void DeactivateAllLayers()
    {
        foreach (var layer in layers)
        {
            layer.activeCount = 0;
            layer.targetVolume = 0f;
        }
    }
}