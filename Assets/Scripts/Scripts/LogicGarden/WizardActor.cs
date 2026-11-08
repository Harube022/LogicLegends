using System;
using UnityEngine;

/// <summary>Authored wizard poses only; the interaction anchor never moves.</summary>
[DisallowMultipleComponent]
public sealed class WizardActor : MonoBehaviour
{
    public Animator animator;
    public bool greetOnSpawn;
    public float standingHeight=7;
    [Min(.05f)] public float blendSeconds=.22f;
    public string[] conversationStates={"Wizard_FistPump","Wizard_Cheering","Wizard_HandsForwardGesture","Wizard_HardHeadNod","Wizard_Nod","Wizard_Idle2","Wizard_Float"};
    public bool GreetingPlayed { get; private set; }
    public bool GreetingActive { get; private set; }
    public int LastGesture { get; private set; }=-1;
    public string LastRequestedState { get; private set; }
    readonly System.Random random=new System.Random();
    public float walkingNaturalSpeed=2.5f,walking1NaturalSpeed=2.5f;
    public AnimationCurve walkingRootZ,walking1RootZ;
    public float walkingDuration=1,walking1Duration=1;
    Transform hips;float hipsHomeZ;
    static readonly int WalkPlayback=Animator.StringToHash("WalkPlayback");
    Vector3 rootPosition,rootScale;Quaternion rootRotation;
    void Awake()
    {
        if(animator==null)animator=GetComponentInChildren<Animator>();
        if(animator==null)return;
        animator.applyRootMotion=false;animator.speed=1;animator.updateMode=AnimatorUpdateMode.UnscaledTime;
        rootPosition=animator.transform.localPosition;rootRotation=animator.transform.localRotation;rootScale=animator.transform.localScale;
        foreach(var bone in animator.GetComponentsInChildren<Transform>())if(bone.name=="mixamorig:Hips"){hips=bone;hipsHomeZ=bone.localPosition.z;break;}
    }
    void Update()
    {
        if(greetOnSpawn&&!GreetingPlayed&&Player.LocalInstance!=null&&Player.LocalInstance.gameObject.activeInHierarchy)
        {GreetingPlayed=true;GreetingActive=true;Play("Wizard_Wave");}
    }
    public void SpeakLine()
    {
        GreetingActive=false;
        int count=conversationStates.Length;if(count==0)return;
        int chosen=count==1?0:random.Next(count-(LastGesture>=0?1:0));
        if(LastGesture>=0&&chosen>=LastGesture&&count>1)chosen++;
        LastGesture=chosen;Play(conversationStates[chosen]);
    }
    public void ReturnIdle(){if(!GreetingActive)Play("Wizard_Idle");}
    public void SetWalking(float actualSpeed,int variant)
    {
        if(actualSpeed<.06f){ReturnIdle();return;}
        float reference=variant==1?walking1NaturalSpeed:walkingNaturalSpeed;
        animator.SetFloat(WalkPlayback,Mathf.Clamp(actualSpeed/Mathf.Max(.1f,reference),.05f,2.5f),.12f,Time.deltaTime);
        Play(variant==1?"Wizard_Walking1":"Wizard_Walking");
    }
    public void Play(string state)
    {
        if(animator==null||!animator.isActiveAndEnabled)return;
        int hash=Animator.StringToHash("Base Layer."+state);if(!animator.HasState(0,hash))return;
        bool sameRequest=LastRequestedState==state;LastRequestedState=state;
        // CrossFade is queued until the Animator updates. A new Idle request
        // must cancel a gesture queued earlier in this very same frame.
        if(sameRequest&&((animator.GetCurrentAnimatorStateInfo(0).fullPathHash==hash&&!animator.IsInTransition(0))||
            (animator.IsInTransition(0)&&animator.GetNextAnimatorStateInfo(0).fullPathHash==hash)))return;
        animator.CrossFadeInFixedTime(hash,blendSeconds,0,0);
    }
    void LateUpdate()
    {
        if(animator==null||animator.transform==transform)return;
        // Float can move the hips inside the rig, but may not move/rotate the
        // model root or its stable dialogue/collider anchor permanently.
        animator.transform.localPosition=rootPosition;animator.transform.localRotation=rootRotation;animator.transform.localScale=rootScale;
        if(hips!=null)
        {
            // These walks contain forward travel in Hips. Navigation owns that
            // travel; consume only its root Z curve, retaining all limb motion,
            // vertical motion and lateral sway. Blend compensation with poses.
            float offset=RootTravel(animator.GetCurrentAnimatorStateInfo(0));
            if(animator.IsInTransition(0))offset=Mathf.Lerp(offset,RootTravel(animator.GetNextAnimatorStateInfo(0)),animator.GetAnimatorTransitionInfo(0).normalizedTime);
            var position=hips.localPosition;position.z-=offset;hips.localPosition=position;
        }
    }
    float RootTravel(AnimatorStateInfo state)
    {
        AnimationCurve curve=null;float duration=1;
        if(state.IsName("Wizard_Walking")){curve=walkingRootZ;duration=walkingDuration;}
        else if(state.IsName("Wizard_Walking1")){curve=walking1RootZ;duration=walking1Duration;}
        if(curve==null||curve.length==0)return 0;
        return curve.Evaluate(Mathf.Repeat(state.normalizedTime,1)*duration)-hipsHomeZ;
    }
}
