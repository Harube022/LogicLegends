using UnityEngine;

/// <summary>A stable full-size carry socket with symmetric arm support, independent of idle hand sway.</summary>
[DefaultExecutionOrder(150), DisallowMultipleComponent]
public sealed class PlayerCarryRig : MonoBehaviour
{
    public Transform anchor;
    public IntegratedSkinProfile ActiveSkin { get; private set; }
    Player player;
    Vector3 originalPosition;
    Quaternion originalRotation;
    bool captured;
    Arm leftArm,rightArm;
    float handWeight;
    float supportHeight,gripHalfSpan;
    Transform carrySpine;
    Quaternion neutralSpine;
    sealed class Arm
    {
        public Transform upper,lower,hand;
        public Quaternion palmBasis;
    }
    public float CarryHandSpan=>ActiveSkin!=null&&ActiveSkin.carryHandSpanLocal>0 ?
        ActiveSkin.carryHandSpanLocal*Mathf.Abs(ActiveSkin.transform.lossyScale.x) :
        ActiveSkin!=null&&ActiveSkin.leftHand!=null&&ActiveSkin.rightHand!=null ? Vector3.Distance(ActiveSkin.leftHand.position,ActiveSkin.rightHand.position) : 1f;

    void Awake()
    {
        player = GetComponent<Player>();
        if (anchor == null && player != null) anchor = player.GetHoldPoint();
        if (anchor != null)
        {
            originalPosition = anchor.localPosition; originalRotation = anchor.localRotation; captured = true;
        }
    }
    public void SelectSkin(IntegratedSkinProfile skin)
    {
        ActiveSkin=skin;handWeight=0;
        leftArm=CreateArm(skin!=null?skin.leftHand:null);
        rightArm=CreateArm(skin!=null?skin.rightHand:null);
        carrySpine=null;
        if(skin!=null)foreach(var bone in skin.GetComponentsInChildren<Transform>())
            if(bone.name=="mixamorig:Spine"){carrySpine=bone;neutralSpine=Quaternion.Inverse(transform.rotation)*bone.rotation;break;}
        if(leftArm!=null&&rightArm!=null)
        {
            var left=transform.InverseTransformPoint(leftArm.upper.position);var right=transform.InverseTransformPoint(rightArm.upper.position);
            supportHeight=(left.y+right.y)*.5f-.1f;
            gripHalfSpan=Mathf.Abs(right.x-left.x)*.5f+.15f;
        }
    }
    public void DeselectSkin(IntegratedSkinProfile skin) { if (ActiveSkin == skin) ActiveSkin = null; }
    public bool IsCarrying
    {
        get
        {
            if (player == null) player = GetComponent<Player>();
            var held = player != null ? player.GetHeldObject() : null;
            if (held != null && held.IsHeld && held.gameObject.activeInHierarchy) return true;
            if (anchor != null)
                for (int i = 0; i < anchor.childCount; i++)
                    if (anchor.GetChild(i).gameObject.activeInHierarchy) return true;
            return false;
        }
    }
    public void RefreshAnchor()
    {
        if (anchor == null) return;
        if (ActiveSkin == null || !ActiveSkin.isActiveAndEnabled)
        {
            if (captured) { anchor.localPosition = originalPosition; anchor.localRotation = originalRotation; }
            return;
        }
        var skin = ActiveSkin;
        if (skin.leftHand == null || skin.rightHand == null) return;
        var world=skin.carryHandSpanLocal>0 ? skin.transform.TransformPoint(skin.carryReferenceLocal) : (skin.leftHand.position+skin.rightHand.position)*.5f;
        var midpoint = transform.InverseTransformPoint(world);
        var support=SupportItem();
        var size=support!=null?support.WorldSize:Vector3.zero;
        // Full-size objects clear the torso by moving upward/forward, never by shrinking.
        midpoint.x = 0;
        // The stored calibration includes the old .475 half-depth and .1 margin.
        float torsoFront=Mathf.Max(.4f,skin.minimumCarryForward-.575f);
        midpoint.z = Mathf.Max(skin.minimumCarryForward,Mathf.Max(midpoint.z,torsoFront+size.z*.5f+.18f));
        if(support!=null)midpoint.y=Mathf.Max(supportHeight,midpoint.y+Mathf.Min(.3f,size.y*.14f));
        midpoint += skin.carryOffset;
        midpoint.x=0;
        anchor.SetPositionAndRotation(transform.TransformPoint(midpoint), transform.rotation);
        bool stunned=skin.GetComponent<PlayerAnimator>()!=null&&skin.GetComponent<PlayerAnimator>().IsStunned;
        handWeight=Mathf.MoveTowards(handWeight,support!=null&&IsCarrying&&!stunned?1:0,Time.deltaTime/.18f);
        if(handWeight>0&&support!=null&&!stunned)
        {
            // Counter the source jump/stride hip twist in the carrying torso only.
            // Legs keep their matching locomotion; arm lengths and item scale stay intact.
            if(carrySpine!=null)carrySpine.rotation=Quaternion.Slerp(carrySpine.rotation,transform.rotation*neutralSpine,handWeight);
            // Support the rear face at shoulder width. The bottom block stays on the player's
            // centreline; the arms follow it rather than moving the stack with idle sway.
            float halfWidth=size.x*.5f;
            var grip=new Vector3(Mathf.Min(halfWidth*.94f,gripHalfSpan),-size.y*.075f,-size.z*.5f-.025f);
            SolveArm(leftArm,anchor.TransformPoint(new Vector3(-grip.x,grip.y,grip.z)),-1);
            SolveArm(rightArm,anchor.TransformPoint(grip),1);
        }
    }
    CarriedObjectAlignment SupportItem()
    {
        var stack=GetComponent<TruthTableCarryStack>();
        if(stack!=null&&stack.Count>0)return stack.BottomItem;
        var held=player!=null?player.GetHeldObject():null;
        if(held!=null&&held.IsHeld)return held.GetComponent<CarriedObjectAlignment>();
        return anchor!=null?anchor.GetComponentInChildren<CarriedObjectAlignment>():null;
    }
    static Arm CreateArm(Transform hand)
    {
        if(hand==null||hand.parent==null||hand.parent.parent==null)return null;
        Transform middle=null,index=null,pinky=null;
        foreach(var t in hand.GetComponentsInChildren<Transform>())
        {
            if(t.name.EndsWith("HandMiddle1"))middle=t;
            if(t.name.EndsWith("HandIndex1"))index=t;
            if(t.name.EndsWith("HandPinky1"))pinky=t;
        }
        var forward=middle!=null?hand.InverseTransformPoint(middle.position).normalized:Vector3.right;
        var normal=index!=null&&pinky!=null?Vector3.Cross(hand.InverseTransformPoint(index.position),hand.InverseTransformPoint(pinky.position)).normalized:Vector3.up;
        if(normal.sqrMagnitude<.01f||Vector3.Cross(forward,normal).sqrMagnitude<.01f)normal=Vector3.up;
        return new Arm{upper=hand.parent.parent,lower=hand.parent,hand=hand,palmBasis=Quaternion.LookRotation(forward,normal)};
    }
    void SolveArm(Arm arm,Vector3 target,float side)
    {
        if(arm==null)return;
        var origin=arm.upper.position;float a=Vector3.Distance(origin,arm.lower.position),b=Vector3.Distance(arm.lower.position,arm.hand.position);
        var delta=target-origin;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);
        if(a<.001f||b<.001f||delta.sqrMagnitude<.0001f)return;
        var direction=delta.normalized;
        var bend=transform.right*side*.7f-transform.up*.7f-transform.forward*.25f;
        bend=Vector3.ProjectOnPlane(bend,direction).normalized;
        float along=(a*a+distance*distance-b*b)/(2*distance);
        var elbow=origin+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        var upper=Quaternion.FromToRotation(arm.lower.position-origin,elbow-origin)*arm.upper.rotation;
        arm.upper.rotation=Quaternion.Slerp(arm.upper.rotation,upper,handWeight);
        var lower=Quaternion.FromToRotation(arm.hand.position-arm.lower.position,target-arm.lower.position)*arm.lower.rotation;
        arm.lower.rotation=Quaternion.Slerp(arm.lower.rotation,lower,handWeight);
        var wrist=Quaternion.LookRotation(transform.up,transform.forward)*Quaternion.Inverse(arm.palmBasis);
        arm.hand.rotation=Quaternion.Slerp(arm.hand.rotation,wrist,handWeight);
    }
    void LateUpdate() { RefreshAnchor(); }
}
