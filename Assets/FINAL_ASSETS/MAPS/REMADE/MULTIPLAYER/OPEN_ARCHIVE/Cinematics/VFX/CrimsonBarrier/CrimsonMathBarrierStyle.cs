using UnityEngine;

namespace LogicLegends.OpenArchive.Cinematics.VFX
{
    [CreateAssetMenu(menuName="Logic Legends/VFX/Crimson Mathematical Barrier")]
    public sealed class CrimsonMathBarrierStyle : ScriptableObject
    {
        public Color darkRed = new Color32(0x65,0,0x09,255);
        public Color crimson = new Color32(0xC8,0x10,0x2E,255);
        public Color scarlet = new Color32(255,0x30,0x30,255);
        public Color runeHighlights = new Color32(255,0x77,0x77,255);
        [Range(.5f,4)] public float glowIntensity = 1.65f;
        [Min(2)] public float radius = 4.8f;
        [Min(3)] public float height = 8;
        [Range(.1f,3)] public float animationSpeed = 1;
        [Range(0,.15f)] public float wallOpacity = .055f;
        [Range(0,.08f)] public float insideWallOpacity = .018f;
        [Range(.2f,3)] public float formationSeconds = 1.1f;
        [Range(.2f,3)] public float dissolveSeconds = .9f;
        [Range(1,15)] public float runeDegreesPerSecond = 4;
    }
}
