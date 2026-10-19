using UnityEngine;
using UnityEngine.AI;

namespace LogicLegends.OpenArchive.Hunting
{
    [DefaultExecutionOrder(-200)]
    public sealed class KhezuNavigationData : MonoBehaviour
    {
        public NavMeshData data;
        NavMeshDataInstance instance;
        void OnEnable() { if (data != null && !instance.valid) instance = NavMesh.AddNavMeshData(data); }
        void OnDisable() { if (instance.valid) instance.Remove(); }
    }
}
