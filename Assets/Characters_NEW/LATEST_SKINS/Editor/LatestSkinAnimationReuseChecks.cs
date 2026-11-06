#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class LatestSkinAnimationReuseChecks
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string label) { if(!ok)throw new InvalidOperationException(label); }
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden).GetValue(o);
    static bool State(Animator a,string name)=>a.GetCurrentAnimatorStateInfo(0).IsName(name)||a.IsInTransition(0)&&a.GetNextAnimatorStateInfo(0).IsName(name);
    static void Log(string s)=>File.AppendAllText(LatestSkinPlayChecks.PlaybackOnly?LatestSkinPlaybackSetup.Report:LatestSkinAnimationReuse.Report,s+"\n");
    static LatestSkinIntegration.Manifest Manifest()=>JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json"));

    public static string Static()
    {
        var manifest=Manifest();var scene=EditorSceneManager.NewPreviewScene();int total=0;
        try
        {
            foreach(var target in LatestSkinAnimationReuse.Requests.GroupBy(r=>r.target))
            {
                var record=manifest.mappings.Single(m=>m.gender+"_"+m.category==target.Key);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(record.prefab),scene);
                try
                {
                    var a=root.GetComponentInChildren<Animator>();a.enabled=false;
                    var adapter=a.GetComponent<SharedSkinAnimationRetarget>();
                    LatestSkinAnimationReuse.OriginalClip(record,"Idle").SampleAnimation(a.gameObject,0);
                    Bounds standing;IntegratedSkinProfile.TryVisualBounds(a.transform,out standing);
                    var rest=adapter.bones.ToDictionary(b=>b.target,b=>b.target.localPosition);
                    var rootP=a.transform.position;var rootQ=a.transform.rotation;var rootScale=a.transform.localScale;
                    foreach(var motion in adapter.motions)
                    {
                        Check(target.Any(r=>r.action==motion.state&&r.donor==adapter.donorSkin),"only requested motion changed");
                        Check(motion.clip==LatestSkinAnimationReuse.OriginalClip(manifest.mappings.Single(m=>m.gender+"_"+m.category==adapter.donorSkin),motion.state),"exact donor asset reused");
                        float deepest=0,tallest=0;
                        for(int i=0;i<=80;i++)
                        {
                            LatestSkinAnimationReuse.Sample(a,adapter,motion,i/80f);
                            Bounds b;Check(IntegratedSkinProfile.TryVisualBounds(a.transform,out b),"mesh present");
                            deepest=Mathf.Min(deepest,b.min.y-standing.min.y);tallest=Mathf.Max(tallest,b.size.y);
                            Check(b.min.y>=standing.min.y-.04f,target.Key+" "+motion.state+" ground penetration at "+i+": "+(b.min.y-standing.min.y));
                            Check(b.size.magnitude<standing.size.magnitude*1.9f,"bounded retargeted silhouette");
                            foreach(var bone in adapter.bones)
                            {
                                if(bone.target!=adapter.hips)Check(Vector3.Distance(rest[bone.target],bone.target.localPosition)<.0000001f,"destination limb length retained: "+bone.target.name);
                                Check(!float.IsNaN(bone.target.localRotation.x),"finite bone rotation");
                            }
                            Check(a.transform.position==rootP&&a.transform.rotation==rootQ&&a.transform.localScale==rootScale,"no root travel or model resizing");
                        }
                        if(motion.clip.isLooping)
                        {
                            LatestSkinAnimationReuse.Sample(a,adapter,motion,0);var first=adapter.bones.Select(b=>b.target.localRotation).ToArray();
                            LatestSkinAnimationReuse.Sample(a,adapter,motion,1);
                            Check(adapter.bones.Select((b,i)=>Quaternion.Angle(first[i],b.target.localRotation)).Max()<2.1f,"smooth reused cycle boundary");
                        }
                        Log("STATIC PASS "+target.Key+" "+motion.state+": 81 phases, unchanged limb offsets/proportions/root, floor min="+deepest.ToString("F4")+", no missing binding or new clip.");total++;
                    }
                }
                finally{Object.DestroyImmediate(root);}
            }
            var expected=new HashSet<string>(LatestSkinAnimationReuse.Requests.Select(r=>r.target+"|"+r.action));
            foreach(string line in File.ReadAllLines("Library/AnimationReuseOriginalStates.txt"))
            {
                var parts=line.Split('|');if(expected.Contains(parts[0]+"|"+parts[1]))continue;
                var map=manifest.mappings.Single(m=>m.gender+"_"+m.category==parts[0]);
                var state=AssetDatabase.LoadAssetAtPath<AnimatorController>(map.controller).layers[0].stateMachine.states.Single(s=>s.state.name==parts[1]).state;
                Check(AssetDatabase.GetAssetPath(state.motion)==parts[2],"other animation retained: "+parts[0]+" "+parts[1]);
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        return total+" reused motions passed fit, grounding, proportions and loop checks";
    }
    static void Geometry(IntegratedSkinProfile skin,Dictionary<Transform,Vector3> offsets,string label)
    {
        foreach(var pair in offsets)Check(Vector3.Distance(pair.Key.localPosition,pair.Value)<.0000002f,label+" limb offsets stable: "+pair.Key.name);
        Bounds b;Check(IntegratedSkinProfile.TryVisualBounds(skin.transform,out b)&&b.size.magnitude<skin.referenceHeight*2,label+" no mesh explosion");
        Check(!skin.GetComponent<Animator>().applyRootMotion,label+" no competing root motion");
    }
    public static string Blends()
    {
        var maps=Manifest();var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try
        {
            foreach(var group in LatestSkinAnimationReuse.Requests.GroupBy(r=>r.target))
            {
                var map=maps.mappings.Single(m=>m.gender+"_"+m.category==group.Key);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(map.prefab),scene);
                try
                {
                    var a=root.GetComponentInChildren<Animator>();a.enabled=false;
                    var adapter=a.GetComponent<SharedSkinAnimationRetarget>();var idle=LatestSkinAnimationReuse.OriginalClip(map,"Idle");
                    idle.SampleAnimation(a.gameObject,0);Bounds standing;IntegratedSkinProfile.TryVisualBounds(a.transform,out standing);
                    var bones=adapter.bones.Select(b=>b.target).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
                    foreach(var motion in adapter.motions)
                    {
                        float lowest=0;
                        for(int phase=0;phase<4;phase++)for(int sample=0;sample<=20;sample++)
                        {
                            idle.SampleAnimation(a.gameObject,0);motion.clip.SampleAnimation(a.gameObject,motion.clip.length*phase/4);
                            float w=sample/20f;
                            for(int b=0;b<bones.Length;b++){bones[b].localPosition=Vector3.Lerp(positions[b],bones[b].localPosition,w);bones[b].localRotation=Quaternion.Slerp(rotations[b],bones[b].localRotation,w);}
                            adapter.CorrectPose(w,motion.groundOffset.Evaluate(phase/4f)*w,0);
                            Bounds bounds;IntegratedSkinProfile.TryVisualBounds(a.transform,out bounds);lowest=Mathf.Min(lowest,bounds.min.y-standing.min.y);
                        }
                        Check(lowest>=-.04f,group.Key+" "+motion.state+" mixed-pose floor dip="+lowest);
                        Log("BLEND PASS "+group.Key+" "+motion.state+": 84 phase/weight combinations, floor minimum delta="+lowest.ToString("F4"));count++;
                    }
                }
                finally{Object.DestroyImmediate(root);}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        return count+" blend sets passed";
    }
    static void Speed(CharacterController cc,float expected,string label)
    {var v=cc.velocity;v.y=0;Check(Mathf.Abs(v.magnitude-expected)<.2f,label+" movement speed unchanged: "+v.magnitude+" / "+expected);}
    static void Grip(Player player,PlayerCarryRig rig,IntegratedSkinProfile skin,GameObject item,string label)
    {
        // Coroutine checks resume before LateUpdate. Flush the existing socket /
        // item-follow stages before observing their final rendered-frame result.
        rig.RefreshAnchor();item.GetComponent<CarriedObjectAlignment>().Apply();
        Check(Vector3.Distance(item.GetComponent<Renderer>().bounds.center,rig.anchor.position)<.04f,label+" item remains on stable carry socket");
        var hands=player.transform.InverseTransformPoint((skin.leftHand.position+skin.rightHand.position)*.5f);
        Check(Mathf.Abs(hands.x)<.12f,label+" hand support centred");
        Check(Vector3.Distance(item.transform.lossyScale,Vector3.one*2.6f)<.01f,label+" original item size");
    }
    public static IEnumerator Run()
    {
        Application.runInBackground=true;Application.targetFrameRate=60;Time.timeScale=1;yield return null;int total=0;
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g.GetComponent<Player>()!=null).ToArray())
        {
            root.SetActive(true);yield return null;
            var player=root.GetComponent<Player>();var cc=root.GetComponent<CharacterController>();var loader=root.GetComponent<PlayerEquipmentLoader>();var rig=root.GetComponent<PlayerCarryRig>();
            var entries=Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels").Where(e=>e!=null&&LatestSkinIntegration.Category(e.itemID)!=null).ToArray();
            foreach(var entry in entries)
            {
                GameInput.Instance.SetMobileMovement(Vector2.zero);cc.enabled=false;root.transform.SetPositionAndRotation(new Vector3(0,1.03f,0),Quaternion.identity);cc.enabled=true;player.ResetMotionAfterTeleport();
                typeof(PlayerEquipmentLoader).GetMethod("ApplyEquipment",Hidden).Invoke(loader,new object[]{entry.itemID,Field<PlayerEquipmentLoader.EquippableModel[]>(loader,"clothesModels")});
                yield return new WaitForSeconds(.3f);
                var skin=entry.model.GetComponentInChildren<IntegratedSkinProfile>();var a=skin.GetComponent<Animator>();var driver=skin.GetComponent<PlayerAnimator>();var name=skin.gender+"_"+skin.category;
                var footMonitor=LatestSkinPlayChecks.FootJitterOnly?LatestSkinFootJitterChecks.Monitor(skin):null;
                var offsets=skin.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")&&t.name!="mixamorig:Hips").ToDictionary(t=>t,t=>t.localPosition);
                Check(State(a,"Idle")&&Mathf.Abs(a.speed-1)<.001f,name+" initial idle, global speed unchanged");
                if(LatestSkinPlayChecks.PlaybackOnly)yield return LatestSkinPlaybackChecks.Idle(skin);
                player.SetRunningEnabled(false);GameInput.Instance.SetMobileMovement(Vector2.up);yield return new WaitForSeconds(.4f);
                Check(State(a,"Walk")&&!player.IsRunning(),name+" walk and run restriction");Speed(cc,player.moveSpeed,name);Geometry(skin,offsets,name);
                for(int i=0;i<3;i++){yield return new WaitForSeconds(.3f);Geometry(skin,offsets,name);}
                if(LatestSkinPlayChecks.PlaybackOnly)yield return LatestSkinPlaybackChecks.Observe(player,skin,"Walk");
                var retarget=skin.GetComponent<SharedSkinAnimationRetarget>();
                var warp=skin.GetComponent<PlayerStrideWarp>();
                if(retarget!=null||warp!=null)
                {
                    Time.timeScale=0;yield return new WaitForSecondsRealtime(.1f);
                    var pauseBones=skin.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("mixamorig:")).ToArray();
                    var paused=pauseBones.Select(b=>b.localRotation).ToArray();var hips=retarget!=null?retarget.hips:warp.hips;var hip=hips.localPosition;
                    yield return new WaitForSecondsRealtime(.15f);
                    Check(pauseBones.Select((b,i)=>Quaternion.Angle(paused[i],b.localRotation)).Max()<.02f&&Vector3.Distance(hip,hips.localPosition)<.000001f,name+" paused pose never accumulates pose corrections");
                    Time.timeScale=1;yield return new WaitForSeconds(.2f);Check(State(a,"Walk"),name+" pause resumes same walk");
                }
                if(LatestSkinPlayChecks.PlaybackOnly||retarget!=null)Capture(name+"_Walk",player);
                player.SetRunningEnabled(true);yield return new WaitForSeconds(.35f);Check(!player.IsRunning(),name+" run activation not immediate");
                yield return new WaitForSeconds(3.05f);Check(player.IsRunning()&&State(a,"Run"),name+" existing three-second running activation");Speed(cc,player.moveSpeed*1.5f,name);Geometry(skin,offsets,name);
                if(LatestSkinPlayChecks.PlaybackOnly){yield return LatestSkinPlaybackChecks.Observe(player,skin,"Run");Capture(name+"_Run",player);}
                GameInput.Instance.SetMobileMovement(Vector2.right);yield return new WaitForSeconds(.4f);Geometry(skin,offsets,name);
                GameInput.Instance.SetMobileMovement(Vector2.zero);yield return new WaitForSeconds(.3f);Check(State(a,"Idle"),name+" smooth stop");
                var at=root.transform.position;yield return new WaitForSeconds(.2f);Check(Vector3.Distance(at,root.transform.position)<.1f,name+" no idle drift");
                GameInput.Instance.MobileJump();yield return new WaitForSeconds(.2f);Check(State(a,"Jump")&&player.IsJumping(),name+" actual jump");Geometry(skin,offsets,name);
                if(LatestSkinPlayChecks.PlaybackOnly)LatestSkinPlaybackChecks.Jump(player,skin);
                if(name=="Male_Epic")Capture(name+"_Jump",player);
                float until=Time.time+3;while(player.IsJumping()&&Time.time<until)yield return null;yield return new WaitForSeconds(.2f);Check(cc.isGrounded&&State(a,"Idle"),name+" grounded landing");
                var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.transform.localScale=Vector3.one*2.6f;item.AddComponent<Rigidbody>();var grab=item.AddComponent<GrabbableObject>();grab.Grab(player.HoldPoint);player.SetHeldObjectSilently(grab);
                yield return new WaitForSeconds(.3f);Check(State(a,"CarryIdle")&&a.GetLayerWeight(1)>.99f,name+" original carry pose/layer");Grip(player,rig,skin,item,name);
                GameInput.Instance.SetMobileMovement(Vector2.up);yield return new WaitForSeconds(.5f);Geometry(skin,offsets,name);Grip(player,rig,skin,item,name);
                if(LatestSkinPlayChecks.PlaybackOnly)yield return LatestSkinPlaybackChecks.Weights(player,skin);
                yield return new WaitForSeconds(3.1f);Check(State(a,"Run")&&player.IsRunning(),name+" carry/run blending retains running rules");Grip(player,rig,skin,item,name);
                GameInput.Instance.SetMobileMovement(Vector2.right);yield return new WaitForSeconds(.3f);Geometry(skin,offsets,name);Grip(player,rig,skin,item,name);
                GameInput.Instance.MobileJump();yield return new WaitForSeconds(.2f);Check(State(a,"Jump")&&a.GetLayerWeight(1)>.99f,name+" carry/jump blending");Geometry(skin,offsets,name);Grip(player,rig,skin,item,name);
                until=Time.time+3;while(player.IsJumping()&&Time.time<until)yield return null;
                GameInput.Instance.SetMobileMovement(Vector2.zero);yield return new WaitForSeconds(.3f);
                if(LatestSkinPlayChecks.PlaybackOnly||retarget!=null)Capture(name+"_Carry",player);
                driver.BeginStun(driver.MinimumStunDuration);Check(State(a,"Stun")&&a.GetLayerWeight(1)==0,name+" stun still interrupts immediately and excludes carry");
                yield return new WaitForSeconds(.2f);Geometry(skin,offsets,name);driver.SetStunned(false);
                grab.Drop();player.SetHeldObjectSilently(null);Object.Destroy(item);yield return new WaitForSeconds(.35f);Check(!rig.IsCarrying&&a.GetLayerWeight(1)<.001f&&State(a,"Idle"),name+" release restores idle and layer");
                if(footMonitor!=null){footMonitor.Verify();Object.Destroy(footMonitor);}
                Log("PLAY PASS "+name+": actual joystick walk/run delay/speed/turn/stop, jump/landing, carry idle/walk/run/jump/turn/release, stable proportions/item size, immediate original stun.");total++;
            }
            root.SetActive(false);Object.Destroy(root);yield return null;
        }
        Check(total==12,"all twelve equipment selections");Log("PASS 12 skins / 7 affected / 9 reused states. Editor only; no account writes or physical-device test.");
    }
    static void Capture(string name,Player player)
    {
        string folder=LatestSkinPlayChecks.PlaybackOnly?"Library/AnimationPlaybackPreviews":"Library/AnimationReusePreviews";Directory.CreateDirectory(folder);var camera=Camera.main;
        var position=camera.transform.position;var rotation=camera.transform.rotation;var prior=camera.targetTexture;var active=RenderTexture.active;
        var target=RenderTexture.GetTemporary(720,720,24);Texture2D image=null;
        try
        {
            camera.transform.position=player.transform.position+new Vector3(8,5,14);camera.transform.LookAt(player.transform.position+Vector3.up*1.8f);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();File.WriteAllBytes(folder+"/"+name+".png",image.EncodeToPNG());
        }
        finally{camera.targetTexture=prior;RenderTexture.active=active;camera.transform.SetPositionAndRotation(position,rotation);RenderTexture.ReleaseTemporary(target);if(image!=null)Object.Destroy(image);}
    }
}
#endif
