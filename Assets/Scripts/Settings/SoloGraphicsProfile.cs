using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>References the same rendering assets used by Open Archive multiplayer.</summary>
public sealed class SoloGraphicsProfile : ScriptableObject
{
    public UniversalRenderPipelineAsset pipeline;
    public VolumeProfile atmosphere;
}
