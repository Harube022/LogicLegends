using UnityEngine;

namespace LogicLegends.OpenArchive.Hunting
{
    /// <summary>Optional availability bridge for future elimination/encounter systems; no health logic.</summary>
    public sealed class KhezuHuntEligibility : MonoBehaviour
    {
        [SerializeField] bool eligible = true;
        public bool IsEligible { get { return eligible && gameObject.activeInHierarchy; } }
        public void SetEligible(bool value) { eligible = value; }
    }
}
