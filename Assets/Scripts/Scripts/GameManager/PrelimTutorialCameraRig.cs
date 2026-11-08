using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>A PRELIM-style demonstration camera. The Brain remains the only writer of the rendered camera.</summary>
public sealed class PrelimTutorialCameraRig : MonoBehaviour
{
    static readonly HashSet<PrelimTutorialCameraRig> active = new HashSet<PrelimTutorialCameraRig>();
    public static bool HasActiveView
    { get { foreach (var rig in active) if (rig != null && rig.Camera != null && rig.Camera.enabled) return true; return false; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetActive() { active.Clear(); leases.Clear(); }
    public CinemachineCamera Camera { get; private set; }
    public bool IsTutorialView { get; private set; }
    public static bool HasActiveTutorialView
    { get { foreach(var rig in active)if(rig!=null&&rig.IsTutorialView&&rig.Camera!=null&&rig.Camera.enabled)return true;return false; } }
    struct BrainState
    {
        public CinemachineBlendDefinition blend;
        public CinemachineBlenderSettings custom;
        public bool unscaled;
    }
    sealed class BrainLease
    {
        public BrainState original;
        public readonly HashSet<PrelimTutorialCameraRig> owners = new HashSet<PrelimTutorialCameraRig>();
    }
    static readonly Dictionary<CinemachineBrain, BrainLease> leases = new Dictionary<CinemachineBrain, BrainLease>();
    readonly Dictionary<CinemachineBrain, BrainState> brains = new Dictionary<CinemachineBrain, BrainState>();
    Action returned;
    bool finishing;
    float hintMotionUntil;
    public bool IsMoving => finishing || Time.unscaledTime < hintMotionUntil;
    public static PrelimTutorialCameraRig Create(string name,bool tutorial=false)
    {
        var rig = new GameObject(name).AddComponent<PrelimTutorialCameraRig>();
        rig.Camera = rig.gameObject.AddComponent<CinemachineCamera>();
        rig.IsTutorialView=tutorial;
        active.Add(rig); MobileLookInput.CancelAll();
        var rendered = UnityEngine.Camera.main;
        if (rendered != null)
        {
            rig.transform.SetPositionAndRotation(rendered.transform.position, rendered.transform.rotation);
            var lens=rig.Camera.Lens; lens.FieldOfView=rendered.fieldOfView;
            lens.NearClipPlane=rendered.nearClipPlane;lens.FarClipPlane=rendered.farClipPlane;rig.Camera.Lens=lens;
        }
        foreach(var brain in FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None))
        {
            if(!brain.enabled) continue;
            if (!leases.TryGetValue(brain, out var lease))
            {
                lease = new BrainLease { original = new BrainState { blend=brain.DefaultBlend,custom=brain.CustomBlends,unscaled=brain.IgnoreTimeScale } };
                leases.Add(brain, lease);
            }
            lease.owners.Add(rig); rig.brains[brain]=lease.original;
            brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,PrelimUIPresentation.CameraTransitionSeconds);
            brain.CustomBlends=null; brain.IgnoreTimeScale=true;
        }
        var priority=rig.Camera.Priority;priority.Value=1000;rig.Camera.Priority=priority;
        return rig;
    }
    public void MoveTowards(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        if(finishing) return;
        if (Vector3.Distance(transform.position, position) > .08f || Quaternion.Angle(transform.rotation, rotation) > .3f ||
            Mathf.Abs(Camera.Lens.FieldOfView - fieldOfView) > .1f) hintMotionUntil = Time.unscaledTime + .15f;
        float t=1-Mathf.Exp(-Time.unscaledDeltaTime*4/PrelimUIPresentation.CameraTransitionSeconds);
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position,position,t),Quaternion.Slerp(transform.rotation,rotation,t));
        var lens=Camera.Lens;lens.FieldOfView=Mathf.Lerp(lens.FieldOfView,fieldOfView,t);Camera.Lens=lens;
    }
    public void ReturnToGameplay(Action afterReturn)
    {
        if(finishing) return;
        finishing=true;returned=afterReturn;
        var priority=Camera.Priority;priority.Value=-1000;Camera.Priority=priority;
        StartCoroutine(FinishBlend());
    }
    IEnumerator FinishBlend()
    {
        yield return new WaitForSecondsRealtime(PrelimUIPresentation.CameraTransitionSeconds+.1f);
        FinishReturn();
    }
    public void FinishReturn()
    {
        StopAllCoroutines();
        ReleaseBrains();
        if(Camera!=null) Camera.enabled=false;
        active.Remove(this);
        var callback=returned;returned=null;callback?.Invoke();
        Destroy(gameObject);
    }
    void OnDestroy()
    {
        active.Remove(this);
        ReleaseBrains();var callback=returned;returned=null;callback?.Invoke();
    }
    void ReleaseBrains()
    {
        foreach (var pair in brains)
        {
            if (!leases.TryGetValue(pair.Key, out var lease)) continue;
            lease.owners.Remove(this);
            if (lease.owners.Count != 0) continue;
            if (pair.Key != null)
            { pair.Key.DefaultBlend=lease.original.blend;pair.Key.CustomBlends=lease.original.custom;pair.Key.IgnoreTimeScale=lease.original.unscaled; }
            leases.Remove(pair.Key);
        }
        brains.Clear();
    }
}
