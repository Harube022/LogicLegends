using UnityEngine;

[DisallowMultipleComponent]
public sealed class LaserEmitter : MonoBehaviour
{
    [SerializeField] private Transform beamAnchor;
    public Transform BeamAnchor => beamAnchor != null ? beamAnchor : transform;
    public void SetAnchor(Transform value) => beamAnchor = value;
}
