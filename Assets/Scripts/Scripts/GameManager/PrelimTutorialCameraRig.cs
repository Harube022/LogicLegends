using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>A PRELIM-style demonstration camera. The Brain remains the only writer of the rendered camera.</summary>
public sealed class PrelimTutorialCameraRig : MonoBehaviour
{
    public CinemachineCamera Camera { get; private set; }
    struct BrainState
    {
        public CinemachineBlendDefinition blend;
        public CinemachineBlenderSettings custom;
        public bool unscaled;
    }
    readonly Dictionary<CinemachineBrain, BrainState> brains = new Dictionary<CinemachineBrain, BrainState>();
    Action returned;
    bool finishing;
    public static PrelimTutorialCameraRig Create(string name)
    {
        var rig = new GameObject(name).AddComponent<PrelimTutorialCameraRig>();
        rig.Camera = rig.gameObject.AddComponent<CinemachineCamera>();
        var rendered = UnityEngine.Camera.main;
        if (rendered != null)
        {
            rig.transform.SetPositionAndRotation(rendered.transform.position, rendered.transform.rotation);
            var lens=rig.Camera.Lens; lens.FieldOfView=rendered.fieldOfView;rig.Camera.Lens=lens;
        }
        foreach(var brain in FindObjectsByType<CinemachineBrain>(FindObjectsSortMode.None))
        {
            if(!brain.enabled) continue;
            rig.brains[brain]=new BrainState { blend=brain.DefaultBlend,custom=brain.CustomBlends,unscaled=brain.IgnoreTimeScale };
            brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,PrelimUIPresentation.CameraTransitionSeconds);
            brain.CustomBlends=null; brain.IgnoreTimeScale=true;
        }
        var priority=rig.Camera.Priority;priority.Value=1000;rig.Camera.Priority=priority;
        return rig;
    }
    public void MoveTowards(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        if(finishing) return;
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
        foreach(var pair in brains) if(pair.Key!=null)
        {pair.Key.DefaultBlend=pair.Value.blend;pair.Key.CustomBlends=pair.Value.custom;pair.Key.IgnoreTimeScale=pair.Value.unscaled;}
        brains.Clear();
        var callback=returned;returned=null;callback?.Invoke();
        if(Camera!=null) Camera.enabled=false;
        Destroy(gameObject);
    }
    void OnDestroy()
    {
        foreach(var pair in brains) if(pair.Key!=null)
        {pair.Key.DefaultBlend=pair.Value.blend;pair.Key.CustomBlends=pair.Value.custom;pair.Key.IgnoreTimeScale=pair.Value.unscaled;}
        brains.Clear();var callback=returned;returned=null;callback?.Invoke();
    }
}
