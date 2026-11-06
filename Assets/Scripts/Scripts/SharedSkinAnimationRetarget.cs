using System;
using UnityEngine;

/// <summary>
/// Reuses a verified Generic clip without copying it. Corrects its bind basis and
/// translations for this skin after animation, before the existing carry arm IK.
/// </summary>
[DefaultExecutionOrder(120), DisallowMultipleComponent]
public sealed class SharedSkinAnimationRetarget : MonoBehaviour
{
    [Serializable] public class Bone
    {
        public Transform target;
        public Quaternion before = Quaternion.identity, after = Quaternion.identity;
        public bool carryMasked;
    }
    [Serializable] public class Motion
    {
        public string state;
        public AnimationClip clip;
        public AnimationCurve groundOffset = new AnimationCurve();
        [NonSerialized] public int stateHash;
    }
    [Serializable] public class Contact
    {
        public Transform[] bones;
        public Vector3[] bindPoints;
        public float[] weights;
        public Vector3 WorldPoint()
        {
            var point=Vector3.zero;
            for(int i=0;i<bones.Length;i++)point+=bones[i].TransformPoint(bindPoints[i])*weights[i];
            return point;
        }
    }

    public string donorSkin;
    public Bone[] bones = new Bone[0];
    public Motion[] motions = new Motion[0];
    public Transform hips;
    public Vector3 sourceHipsOrigin, targetHipsOrigin;
    public float hipScale = 1;
    public Contact[] soleContacts = new Contact[0];
    public float floorLocalY;
    Animator animator;
    int carryLayer = -1;
    Quaternion[] renderedRotations;
    Vector3 renderedHips;
    bool hasRenderedPose;

    void Awake()
    {
        animator = GetComponent<Animator>();
        carryLayer = animator.GetLayerIndex("Carry Upper Body");
        foreach (var motion in motions)
            motion.stateHash = Animator.StringToHash("Base Layer." + motion.state);
        renderedRotations=new Quaternion[bones.Length];
    }
    void OnEnable(){hasRenderedPose=false;}

    void LateUpdate()
    {
        if (animator == null || !animator.isActiveAndEnabled) return;
        // Menus can freeze animation evaluation. Preserve the last posed bones
        // instead of applying the bind correction repeatedly to that same pose.
        if(Time.deltaTime<=0)
        {
            if(hasRenderedPose){for(int i=0;i<bones.Length;i++)bones[i].target.localRotation=renderedRotations[i];hips.localPosition=renderedHips;}
            return;
        }
        var current = animator.GetCurrentAnimatorStateInfo(0);
        bool blending = animator.IsInTransition(0);
        float blend = blending ? Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime) : 0;
        var next = blending ? animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
        float weight = 0, ground = 0;
        foreach (var motion in motions)
        {
            if (current.fullPathHash == motion.stateHash)
                Accumulate(motion, current.normalizedTime, 1 - blend, ref weight, ref ground);
            if (blending && next.fullPathHash == motion.stateHash)
                Accumulate(motion, next.normalizedTime, blend, ref weight, ref ground);
        }
        CorrectPose(weight, ground, carryLayer >= 0 ? animator.GetLayerWeight(carryLayer) : 0);
        for(int i=0;i<bones.Length;i++)renderedRotations[i]=bones[i].target.localRotation;
        renderedHips=hips.localPosition;hasRenderedPose=true;
    }

    static void Accumulate(Motion motion, float phase, float contribution, ref float weight, ref float ground)
    {
        weight += contribution;
        // No floor-following or pose-height corrections. Preserve the clip path.
    }

    // Also used by the offline rig calibration. No animation asset is modified.
    public void CorrectPose(float weight, float groundOffset, float carryWeight)
    {
        if (weight <= 0) return;
        weight = Mathf.Clamp01(weight);
        foreach (var bone in bones)
        {
            float w = weight * (bone.carryMasked ? 1 - carryWeight : 1);
            var original = bone.target.localRotation;
            bone.target.localRotation = Quaternion.Slerp(original, bone.before * original * bone.after, w);
        }
        if (hips != null)
        {
            var original = hips.localPosition;
            var adjusted = targetHipsOrigin + (original - sourceHipsOrigin) * hipScale;
            hips.localPosition = Vector3.Lerp(original, adjusted, weight);
        }
    }
}
