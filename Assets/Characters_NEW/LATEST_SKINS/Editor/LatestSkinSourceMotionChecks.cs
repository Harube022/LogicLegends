#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class LatestSkinSourceMotionChecks
{
    public const string Report=LatestSkinIntegration.Reports+"/SourceMotionChecks.txt";
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool good,string text){if(!good)throw new InvalidOperationException(text);}
    public static string Static()
    {
        var maps=JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json"));
        foreach(var m in maps.mappings)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(m.prefab);var a=root.GetComponentInChildren<Animator>(true);
            Check(root.GetComponentsInChildren<PlayerStrideWarp>(true).Length==0,m.id+" no procedural stride component");
            Check(!a.applyRootMotion&&a.speed==1,m.id+" root motion off/global rate one");
            var states=((UnityEditor.Animations.AnimatorController)a.runtimeAnimatorController).layers[0].stateMachine.states;
            foreach(var state in states)
            {
                string action=state.state.name;
                var request=LatestSkinAnimationReuse.Requests.FirstOrDefault(r=>r.target==m.gender+"_"+m.category&&r.action==action);
                var owner=request.donor==null?m:maps.mappings.Single(d=>d.gender+"_"+d.category==request.donor);
                Check(state.state.motion==LatestSkinAnimationReuse.OriginalClip(owner,action),m.id+" only explicitly requested clip replacements: "+action);
                Check(!state.state.iKOnFeet,m.id+" no Humanoid foot IK: "+action);
                if(action=="Walk"||action=="Run"||action=="Idle")Check(state.state.speed==1,m.id+" no hidden state multiplier: "+action);
            }
            foreach(var request in LatestSkinAnimationReuse.Requests.Where(r=>r.target==m.gender+"_"+m.category))
                Check(LatestSkinPlaybackSetup.Clip(a,request.action)==LatestSkinAnimationReuse.OriginalClip(maps.mappings.Single(d=>d.gender+"_"+d.category==request.donor),request.action),"requested donor unchanged");
            var adapter=a.GetComponent<SharedSkinAnimationRetarget>();if(adapter!=null)
            {
                Check(adapter.soleContacts.Length==0&&adapter.motions.All(c=>c.groundOffset.length==0),"no procedural ground offsets");
                Check(Vector3.Distance(adapter.targetHipsOrigin,LatestSkinPlaybackSetup.BindHips(a))<.000001f,"mesh bind origin; no animated pose offset");
            }
        }
        return "12 source rigs checked: no stride IK, no procedural floor offsets, original mappings and root-motion rules retained.";
    }
    static IEnumerator WaitState(Animator a,string state){yield return new WaitForSeconds(.4f);Check(a.GetCurrentAnimatorStateInfo(0).IsName(state)||a.GetNextAnimatorStateInfo(0).IsName(state),state+" active");}
    static IEnumerator Clock(Animator a,string action)
    {
        var clip=LatestSkinPlaybackSetup.Clip(a,action);string parameter=action=="Walk"?PlayerAnimator.WALK_PLAYBACK:PlayerAnimator.RUN_PLAYBACK;
        float rate=a.GetFloat(parameter),started=Time.time,phase=a.GetCurrentAnimatorStateInfo(0).normalizedTime;
        yield return new WaitForSeconds(.35f);
        float advanced=a.GetCurrentAnimatorStateInfo(0).normalizedTime-phase;
        Check(a.GetCurrentAnimatorStateInfo(0).IsName(action)&&Mathf.Abs(advanced-(Time.time-started)*rate/clip.length)<.04f+Time.deltaTime*rate/clip.length,"one continuous animation clock, no repeated state entry");
        Check(rate>0&&rate<=(action=="Walk"?1.65f:1.5f)+.001f,"bounded playback only");
    }
    public static IEnumerator Run()
    {
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;yield return null;int count=0;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.GetComponent<Player>()!=null).ToArray())
        {
            root.SetActive(true);yield return null;var p=root.GetComponent<Player>();var cc=root.GetComponent<CharacterController>();var loader=root.GetComponent<PlayerEquipmentLoader>();
            var entries=(PlayerEquipmentLoader.EquippableModel[])typeof(PlayerEquipmentLoader).GetField("clothesModels",Hidden).GetValue(loader);
            foreach(var entry in entries.Where(e=>e!=null&&LatestSkinIntegration.Category(e.itemID)!=null))
            {
                GameInput.Instance.SetMobileMovement(Vector2.zero);cc.enabled=false;root.transform.SetPositionAndRotation(new Vector3(0,1.03f,0),Quaternion.identity);cc.enabled=true;p.ResetMotionAfterTeleport();
                typeof(PlayerEquipmentLoader).GetMethod("ApplyEquipment",Hidden).Invoke(loader,new object[]{entry.itemID,entries});yield return new WaitForSeconds(.25f);
                var skin=entry.model.GetComponentInChildren<IntegratedSkinProfile>();var a=skin.GetComponent<Animator>();
                var monitor=skin.gameObject.AddComponent<SourcePoseMonitor>();monitor.Initialize(skin);
                p.SetRunningEnabled(false);GameInput.Instance.SetMobileMovement(Vector2.up);yield return WaitState(a,"Walk");yield return new WaitForSeconds(.8f);yield return Clock(a,"Walk");
                p.SetRunningEnabled(true);yield return new WaitForSeconds(3.1f);yield return WaitState(a,"Run");yield return Clock(a,"Run");
                var velocity=cc.velocity;velocity.y=0;Check(Mathf.Abs(velocity.magnitude-p.moveSpeed*p.RunSpeedMultiplier)<.2f,"run speed unchanged");
                GameInput.Instance.SetMobileMovement(Vector2.right);yield return new WaitForSeconds(.4f);GameInput.Instance.SetMobileMovement(Vector2.zero);yield return WaitState(a,"Idle");
                var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.localScale=Vector3.one*2.6f;item.AddComponent<Rigidbody>();var grab=item.AddComponent<GrabbableObject>();grab.Grab(p.HoldPoint);p.SetHeldObjectSilently(grab);yield return WaitState(a,"CarryIdle");
                GameInput.Instance.SetMobileMovement(Vector2.up);p.SetRunningSuppressed(monitor,true);
                foreach(float weight in new[]{.95f,.88f,.8f,.75f})
                {
                    p.SetMovementSpeedModifier(monitor,weight);yield return new WaitForSeconds(.5f);var v=cc.velocity;v.y=0;Check(Mathf.Abs(v.magnitude-p.moveSpeed*weight)<.2f,"carry penalty unchanged");
                    Check(root.GetComponent<PlayerCarryRig>().IsCarrying&&Vector3.Distance(item.transform.lossyScale,Vector3.one*2.6f)<.01f,"carry size/pose retained");
                }
                p.RemoveMovementSpeedModifier(monitor);p.SetRunningSuppressed(monitor,false);yield return new WaitForSeconds(3.1f);yield return WaitState(a,"Run");
                GameInput.Instance.SetMobileMovement(Vector2.left);yield return new WaitForSeconds(.4f);GameInput.Instance.MobileJump();yield return WaitState(a,"Jump");
                float deadline=Time.time+3;while(p.IsJumping()&&Time.time<deadline)yield return null;
                GameInput.Instance.SetMobileMovement(Vector2.zero);grab.Drop();p.SetHeldObjectSilently(null);Object.Destroy(item);yield return WaitState(a,"Idle");
                monitor.Verify();Object.Destroy(monitor);count++;
            }
            root.SetActive(false);Object.Destroy(root);yield return null;
        }
        Check(count==12,"all 12 skins tested");File.AppendAllText(Report,"ALL 12 SOURCE-MOTION SKINS PASSED. Editor only.\n");
    }
}

[DefaultExecutionOrder(300)]
public sealed class SourcePoseMonitor:MonoBehaviour
{
    IntegratedSkinProfile skin;Animator live,reference;GameObject referenceRoot;
    Transform[] actualBones,referenceBones;int walk,run,turns,blends,carry;float maxAngle,maxHip,maxLimbOffset,maxKneeStep;Quaternion previousRoot,leftKnee,rightKnee;bool hadGait;
    public void Initialize(IntegratedSkinProfile value)
    {
        skin=value;live=skin.GetComponent<Animator>();previousRoot=skin.GetComponentInParent<Player>().transform.rotation;
        var maps=JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json"));var m=maps.mappings.Single(x=>x.gender==skin.gender&&x.category==skin.category);
        referenceRoot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(m.prefab));
        foreach(var b in referenceRoot.GetComponentsInChildren<Behaviour>())b.enabled=false;
        foreach(var r in referenceRoot.GetComponentsInChildren<Renderer>())r.enabled=false;
        reference=referenceRoot.GetComponentInChildren<Animator>(true);
        string[] names={"mixamorig:Hips","mixamorig:LeftUpLeg","mixamorig:LeftLeg","mixamorig:LeftFoot","mixamorig:LeftToeBase","mixamorig:RightUpLeg","mixamorig:RightLeg","mixamorig:RightFoot","mixamorig:RightToeBase"};
        actualBones=names.Select(n=>live.GetComponentsInChildren<Transform>().Single(t=>t.name==n)).ToArray();
        referenceBones=names.Select(n=>reference.GetComponentsInChildren<Transform>().Single(t=>t.name==n)).ToArray();
    }
    void LateUpdate()
    {
        if(skin==null||Time.deltaTime<=0)return;var root=skin.GetComponentInParent<Player>();
        if(Quaternion.Angle(previousRoot,root.transform.rotation)>.1f)turns++;previousRoot=root.transform.rotation;
        var state=live.GetCurrentAnimatorStateInfo(0);
        bool gait=state.IsName("Walk")||state.IsName("Run")||state.IsName("Idle")||state.IsName("CarryIdle");
        if(gait&&hadGait)maxKneeStep=Mathf.Max(maxKneeStep,Mathf.Max(Quaternion.Angle(leftKnee,actualBones[2].localRotation),Quaternion.Angle(rightKnee,actualBones[6].localRotation))/(60*Time.deltaTime));
        hadGait=gait;leftKnee=actualBones[2].localRotation;rightKnee=actualBones[6].localRotation;
        if(live.IsInTransition(0)){blends++;return;}
        string action=state.IsName("Walk")?"Walk":state.IsName("Run")?"Run":null;if(action==null)return;
        if(action=="Walk")walk++;else run++;if(root.GetComponent<PlayerCarryRig>().IsCarrying)carry++;
        var clip=LatestSkinPlaybackSetup.Clip(reference,action);LatestSkinPlaybackSetup.Sample(reference,clip,Mathf.Repeat(state.normalizedTime,1));
        for(int i=0;i<actualBones.Length;i++)maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(actualBones[i].localRotation,referenceBones[i].localRotation));
        for(int i=1;i<actualBones.Length;i++)maxLimbOffset=Mathf.Max(maxLimbOffset,Vector3.Distance(actualBones[i].localPosition,referenceBones[i].localPosition));
        maxHip=Mathf.Max(maxHip,Vector3.Distance(actualBones[0].localPosition,referenceBones[0].localPosition)*live.transform.lossyScale.y);
    }
    public void Verify()
    {
        if(walk<20||run<20||turns<2||blends<2||carry<20||maxAngle>3||maxHip>.04f||maxLimbOffset>.000001f||maxKneeStep>45)
            throw new InvalidOperationException("Source comparison "+skin.gender+skin.category+" walk/run="+walk+"/"+run+" turn/blend/carry="+turns+"/"+blends+"/"+carry+" angle="+maxAngle+" hip="+maxHip);
        File.AppendAllText(LatestSkinSourceMotionChecks.Report,"PASS "+skin.gender+"_"+skin.category+": source lower-body match max angle="+maxAngle.ToString("F4")+"deg hip="+maxHip.ToString("F5")+" limb offset="+maxLimbOffset.ToString("F8")+"; peak knee step/60Hz="+maxKneeStep.ToString("F2")+"deg; walk/run frames="+walk+"/"+run+"; turns/blends/carry="+turns+"/"+blends+"/"+carry+".\n");
    }
    void OnDestroy(){if(referenceRoot!=null)Object.Destroy(referenceRoot);}
}
#endif
