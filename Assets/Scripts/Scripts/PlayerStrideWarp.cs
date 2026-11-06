using System;
using UnityEngine;

/// <summary>Legacy serialized data only. No longer drives bones or runs an IK update.</summary>
[DisallowMultipleComponent]
public sealed class PlayerStrideWarp : MonoBehaviour
{
    [Serializable] public class Leg { public Transform thigh,knee,foot; [NonSerialized] public Quaternion thighPose,kneePose,footPose; }
    public Transform hips;
    public Leg left=new Leg(),right=new Leg();
    public SharedSkinAnimationRetarget.Contact[] contacts=new SharedSkinAnimationRetarget.Contact[0];
    public float floorLocalY,maximumStride=2.15f;
    public float MaximumFootError=>0;
    public float AppliedStride=>1;
    public bool PoseAdjusted=>false;
    // Kept inert so old development fixtures / prefab overrides cannot restore IK.
    public void Apply(float stride,float weight) { }
}
