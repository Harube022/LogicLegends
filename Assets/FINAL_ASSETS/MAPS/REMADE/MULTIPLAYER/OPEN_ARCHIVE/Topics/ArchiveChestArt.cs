using UnityEngine;

namespace LogicLegends.OpenArchive.Topics
{
    /// <summary>Reusable art anchors. The existing topic session still owns the drop and claim.</summary>
    [DisallowMultipleComponent]
    public sealed class ArchiveChestArt : MonoBehaviour
    {
        public Transform lid, rewardAnchor;
        public Light interiorLight;
        public Vector3 displaySize = new Vector3(1.55f, .40f, .84f);
        [Range(75, 115)] public float openAngle = 105;
    }
}
