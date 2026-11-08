#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Preserves equipment identity objects while replacing only their visual children.</summary>
public static class LatestSkinIntegration
{
    public const string Root = LatestSkinPreparation.Root;
    public const string Reports = Root + "/Integration";
    public static readonly string[] BasePlayers = { "Assets/Prefabs/PREFABS/CHARACTERS/Player.prefab", "Assets/Prefabs/PREFABS/CHARACTERS/Player_Female.prefab" };
    [Serializable] public class Mapping
    {
        public string id, gender, category, oldName, prefab, controller;
        public float walkSpeed, runSpeed;
    }
    [Serializable] public class Manifest { public List<Mapping> mappings = new List<Mapping>(); public List<string> locations = new List<string>(); }
    public static string Category(string id)
    {
        if (id == "male_default" || id == "male default" || id == "female_default") return "Uniform";
        if (id == null || !(id.EndsWith("_Skin") || id.EndsWith("_Costume"))) return null;
        return id.Split('_')[0];
    }
    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        Folder(Path.GetDirectoryName(path).Replace('\\','/')); AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));
    }
    static void Backup(string path)
    {
        var dest="Temp/LatestSkinsIntegrationBackup/"+path;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)); if (!File.Exists(dest)) File.Copy(path,dest);
    }
    static string ID(string gender,string category) => category=="Uniform" ? gender.ToLowerInvariant()+"_default" : category+(gender=="Female"?"_Costume":"_Skin");

    static float MeasureFootSpeed(GameObject model,AnimationClip clip)
    {
        var hips=model.transform.Find("mixamorig:Hips");
        var feet=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="mixamorig:LeftFoot"||t.name=="mixamorig:RightFoot").ToArray();
        var positions=new List<Vector3[]>();var heights=new List<float[]>();
        int count=Mathf.Max(24,Mathf.CeilToInt(clip.length*clip.frameRate));
        for(int i=0;i<=count;i++)
        {
            clip.SampleAnimation(model,clip.length*i/count);
            positions.Add(feet.Select(f=>model.transform.InverseTransformVector(f.position-hips.position)).ToArray());
            heights.Add(feet.Select(f=>model.transform.InverseTransformPoint(f.position).y).ToArray());
        }
        var speeds=new List<float>();
        for(int f=0;f<feet.Length;f++)
        {
            float low=heights.Min(h=>h[f]), high=heights.Max(h=>h[f]);
            for(int i=1;i<positions.Count;i++)
            {
                float backward=-(positions[i][f].z-positions[i-1][f].z)/(clip.length/count);
                if(heights[i][f]<=Mathf.Lerp(low,high,.45f)&&backward>.00001f)speeds.Add(backward);
            }
        }
        // Low velocities near foot-plant reversals are not the stride speed.
        speeds.Sort();return speeds.Count==0?.02f:Mathf.Max(.00001f,speeds[Mathf.Min(speeds.Count-1,Mathf.FloorToInt(speeds.Count*.85f))]);
    }
    static void TrimJump(AnimationClip clip)
    {
        var binding=AnimationUtility.GetCurveBindings(clip).FirstOrDefault(b=>b.path=="mixamorig:Hips"&&b.propertyName=="m_LocalPosition.y");
        var height=AnimationUtility.GetEditorCurve(clip,binding);if(height==null)return;
        float baseline=height.Evaluate(0), peak=height.keys.Max(k=>k.value), threshold=baseline+(peak-baseline)*.12f;
        float start=clip.length,end=0;
        for(float t=0;t<=clip.length;t+=1f/60){if(height.Evaluate(t)>threshold){start=Mathf.Min(start,t);end=t;}}
        start=Mathf.Max(0,start-.1f);end=Mathf.Min(clip.length,end+.12f);
        if(end-start<.35f)return;
        foreach(var b in AnimationUtility.GetCurveBindings(clip))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,b);var keys=new List<Keyframe>();
            for(float t=0;t<end-start;t+=1f/60)keys.Add(new Keyframe(t,curve.Evaluate(start+t)));
            keys.Add(new Keyframe(end-start,curve.Evaluate(end)));
            AnimationUtility.SetEditorCurve(clip,b,new AnimationCurve(keys.ToArray()));
        }
        clip.EnsureQuaternionContinuity();
    }
    static void GroundStunAndRecover(AnimationClip clip,GameObject model,AnimationClip idle)
    {
        // The supplied Stun assets contain a fall only, not a get-up. Reuse this
        // skin's own pose trajectory in reverse for one controlled recovery.
        idle.SampleAnimation(model,0);Bounds standing;IntegratedSkinProfile.TryVisualBounds(model.transform,out standing);
        float floor=standing.min.y, fall=clip.length*.6f, recovery=1.2f, length=fall+recovery;
        var bindings=AnimationUtility.GetCurveBindings(clip);var curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(clip,b)).ToArray();
        var output=bindings.Select(b=>new List<Keyframe>()).ToArray();
        int samples=Mathf.CeilToInt(length*60);
        for(int i=0;i<=samples;i++)
        {
            float t=length*i/samples;
            float sourceTime=t<=fall?t:fall*(1-Mathf.SmoothStep(0,1,(t-fall)/recovery));
            clip.SampleAnimation(model,sourceTime);Bounds posed;IntegratedSkinProfile.TryVisualBounds(model.transform,out posed);
            float lift=model.transform.InverseTransformVector(Vector3.up*(floor-posed.min.y)).y;
            for(int j=0;j<bindings.Length;j++)
            {
                float value=curves[j].Evaluate(sourceTime);
                if(bindings[j].path=="mixamorig:Hips"&&bindings[j].propertyName=="m_LocalPosition.y")value+=lift;
                output[j].Add(new Keyframe(t,value));
            }
        }
        clip.ClearCurves();
        for(int j=0;j<bindings.Length;j++)
        {
            var curve=new AnimationCurve(output[j].ToArray());
            for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);}
            AnimationUtility.SetEditorCurve(clip,bindings[j],curve);
        }
        clip.EnsureQuaternionContinuity();
    }
    static void CloseLoopSeams(AnimationClip clip)
    {
        // Some supplied Walk clips end 9–10 degrees away from their first pose.
        // Distribute that small correction over the cycle instead of inserting a pause.
        var rotations=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("m_LocalRotation.")).GroupBy(b=>b.path).ToArray();
        foreach(var group in rotations)
        {
            var bindings=group.OrderBy(b=>b.propertyName).ToArray();if(bindings.Length!=4)continue;
            var curves=bindings.Select(b=>AnimationUtility.GetEditorCurve(clip,b)).ToArray();
            Func<float,Quaternion> pose=t=>new Quaternion(curves[1].Evaluate(t),curves[2].Evaluate(t),curves[3].Evaluate(t),curves[0].Evaluate(t)).normalized;
            var first=pose(0);var last=pose(clip.length);if(Quaternion.Angle(first,last)<2)continue;
            var correction=Quaternion.Inverse(last)*first;
            var output=new[]{new List<Keyframe>(),new List<Keyframe>(),new List<Keyframe>(),new List<Keyframe>()};
            int count=Mathf.CeilToInt(clip.length*60);
            for(int i=0;i<=count;i++)
            {
                float t=clip.length*i/count;var q=pose(t)*Quaternion.Slerp(Quaternion.identity,correction,Mathf.SmoothStep(0,1,(float)i/count));
                output[0].Add(new Keyframe(t,q.w));output[1].Add(new Keyframe(t,q.x));output[2].Add(new Keyframe(t,q.y));output[3].Add(new Keyframe(t,q.z));
            }
            for(int i=0;i<4;i++)AnimationUtility.SetEditorCurve(clip,bindings[i],new AnimationCurve(output[i].ToArray()));
        }
        clip.EnsureQuaternionContinuity();
    }
    static void GroundCarryPose(AnimationClip clip,GameObject model,AnimationClip idle)
    {
        idle.SampleAnimation(model,0);Bounds standing;IntegratedSkinProfile.TryVisualBounds(model.transform,out standing);
        clip.SampleAnimation(model,clip.length*.35f);Bounds held;IntegratedSkinProfile.TryVisualBounds(model.transform,out held);
        float correction=model.transform.InverseTransformVector(Vector3.up*(standing.min.y-held.min.y)).y;
        foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.path=="mixamorig:Hips"&&b.propertyName=="m_LocalPosition.y"))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
            for(int i=0;i<keys.Length;i++){var key=keys[i];key.value+=correction;keys[i]=key;}curve.keys=keys;
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
    }
    static AnimationClip CarryIdlePose(GameObject model,AnimationClip idle,AnimationClip carry,string path)
    {
        idle.SampleAnimation(model,0);
        var bones=model.GetComponentsInChildren<Transform>(true);
        var positions=bones.Select(b=>b.localPosition).ToArray();var scales=bones.Select(b=>b.localScale).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
        carry.SampleAnimation(model,carry.length*.35f);
        for(int i=0;i<bones.Length;i++)
        {
            string bonePath=AnimationUtility.CalculateTransformPath(bones[i],model.transform);
            // Keep Idle's centred torso and shoulders. The source Carry torso is
            // twisted on some skins; copy only arm/finger poses before runtime support IK.
            if(bonePath.Contains("Spine")&&(bonePath.Contains("Arm")||bonePath.Contains("Hand")))rotations[i]=bones[i].localRotation;
            bones[i].localPosition=positions[i];bones[i].localScale=scales[i];bones[i].localRotation=rotations[i];
        }
        var clip=new AnimationClip{name=Path.GetFileNameWithoutExtension(path),frameRate=30};
        for(int i=0;i<bones.Length;i++)
        {
            string bonePath=AnimationUtility.CalculateTransformPath(bones[i],model.transform);if(string.IsNullOrEmpty(bonePath))continue;
            var p=positions[i];var q=rotations[i];var scale=scales[i];
            string[] names={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w","m_LocalScale.x","m_LocalScale.y","m_LocalScale.z"};
            float[] values={p.x,p.y,p.z,q.x,q.y,q.z,q.w,scale.x,scale.y,scale.z};
            for(int j=0;j<names.Length;j++)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bonePath,typeof(Transform),names[j]),AnimationCurve.Constant(0,1,values[j]));
        }
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clip.EnsureQuaternionContinuity();
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing==null)AssetDatabase.CreateAsset(clip,path);else{EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);clip=existing;}
        return clip;
    }
    static float CarryClearance(GameObject model,Dictionary<string,AnimationClip> clips,Vector3 reference)
    {
        clips["CarryIdle"].SampleAnimation(model,0);
        var upper=model.GetComponentsInChildren<Transform>(true).Where(t=>
        {var p=AnimationUtility.CalculateTransformPath(t,model.transform);return p.Contains("Spine")&&!p.Contains("Neck")&&!p.Contains("Head");}).ToArray();
        var poses=upper.Select(t=>t.localRotation).ToArray();var positions=upper.Select(t=>t.localPosition).ToArray();
        float front=0;var mesh=new Mesh();float handHeight=model.transform.TransformVector(reference).y;
        try
        {
            foreach(var action in new[]{"CarryIdle","Walk","Jump"})for(int sample=0;sample<=12;sample++)
            {
                var clip=clips[action];clip.SampleAnimation(model,clip.length*sample/12);
                for(int i=0;i<upper.Length;i++){upper[i].localRotation=poses[i];upper[i].localPosition=positions[i];}
                foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.BakeMesh(mesh,true);var vertices=mesh.vertices;var weights=renderer.sharedMesh.boneWeights;
                    for(int i=0;i<vertices.Length&&i<weights.Length;i++)
                    {
                        var name=renderer.bones[weights[i].boneIndex0].name;if(!name.Contains("Hips")&&!name.Contains("Spine"))continue;
                        var local=Quaternion.Inverse(model.transform.rotation)*(renderer.transform.TransformPoint(vertices[i])-model.transform.position);
                        if(Mathf.Abs(local.x)<.7f&&local.y>handHeight-.6f)front=Mathf.Max(front,local.z);
                    }
                    mesh.Clear();
                }
            }
        }
        finally{Object.DestroyImmediate(mesh);}
        return Mathf.Max(1.25f,front+.475f+.1f);
    }
    static AnimationClip GameplayClip(AnimationClip source,string path,Vector3 idleHips,string action,GameObject model,AnimationClip idle)
    {
        var copy=Object.Instantiate(source);copy.name=Path.GetFileNameWithoutExtension(path);copy.hideFlags=HideFlags.None;
        if(action=="Jump")TrimJump(copy);
        // CharacterController supplies world movement. Keep source assets intact; remove only duplicate root travel in gameplay copies.
        foreach(var b in AnimationUtility.GetCurveBindings(copy).Where(b=>b.path=="mixamorig:Hips"&&b.propertyName.StartsWith("m_LocalPosition.")))
        {
            var c=AnimationUtility.GetEditorCurve(copy,b);var keys=c.keys;
            bool jumpHeight=action=="Jump"&&b.propertyName.EndsWith(".y");
            if(jumpHeight) { AnimationUtility.SetEditorCurve(copy,b,AnimationCurve.Constant(0,copy.length,idleHips.y));continue; }
            if(b.propertyName.EndsWith(".y"))continue;
            float start=c.Evaluate(0), trend=(c.Evaluate(copy.length)-start)/Mathf.Max(.001f,copy.length);
            float origin=b.propertyName.EndsWith(".x")?idleHips.x:idleHips.z;
            for(int i=0;i<keys.Length;i++)
            {
                var key=keys[i];key.value=key.value-start-trend*key.time+origin;key.inTangent-=trend;key.outTangent-=trend;keys[i]=key;
            }
            c.keys=keys;AnimationUtility.SetEditorCurve(copy,b,c);
        }
        if(action=="Stun")GroundStunAndRecover(copy,model,idle);
        if(action=="Carry")GroundCarryPose(copy,model,idle);
        if(action!="Stun"&&action!="Jump")CloseLoopSeams(copy);
        var settings=AnimationUtility.GetAnimationClipSettings(copy);settings.loopTime=action!="Jump"&&action!="Stun";settings.loopBlend=settings.loopTime;
        AnimationUtility.SetAnimationClipSettings(copy,settings);
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(old==null)AssetDatabase.CreateAsset(copy,path);else{EditorUtility.CopySerialized(copy,old);Object.DestroyImmediate(copy);EditorUtility.SetDirty(old);copy=old;}
        return copy;
    }
    static void Condition(AnimatorStateTransition transition,string parameter,bool value)
    { transition.AddCondition(value?AnimatorConditionMode.If:AnimatorConditionMode.IfNot,0,parameter); }
    static AnimatorController BuildController(string path,GameObject model,Dictionary<string,AnimationClip> clips)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.parameters=new AnimatorControllerParameter[0];
        foreach(var p in new[]{"IsWalking","IsRunning","IsJumping","IsStunned",PlayerAnimator.IS_CARRYING})controller.AddParameter(p,AnimatorControllerParameterType.Bool);
        foreach(var p in new[]{PlayerAnimator.WALK_PLAYBACK,PlayerAnimator.RUN_PLAYBACK,PlayerAnimator.JUMP_PLAYBACK}){controller.AddParameter(p,AnimatorControllerParameterType.Float);var pars=controller.parameters;pars[pars.Length-1].defaultFloat=1;controller.parameters=pars;}
        controller.AddParameter(PlayerAnimator.STUN_TIME,AnimatorControllerParameterType.Float);
        while(controller.layers.Length>1)controller.RemoveLayer(controller.layers.Length-1);
        var sm=controller.layers[0].stateMachine;
        foreach(var t in sm.anyStateTransitions)sm.RemoveAnyStateTransition(t);
        foreach(var state in sm.states)sm.RemoveState(state.state);
        foreach(var action in new[]{"Stun","Jump","Run","Walk","CarryIdle","Idle"})
        {
            var state=sm.AddState(action);state.motion=clips[action];state.writeDefaultValues=false;
            state.tag=action=="Walk"||action=="Run"?"Locomotion":action;
            if(action=="Idle")sm.defaultState=state;
            if(action=="Walk"||action=="Run"){state.speedParameterActive=true;state.speedParameter=action=="Walk"?PlayerAnimator.WALK_PLAYBACK:PlayerAnimator.RUN_PLAYBACK;}
            if(action=="Jump"){state.speedParameterActive=true;state.speedParameter=PlayerAnimator.JUMP_PLAYBACK;}
            if(action=="Stun"){state.timeParameterActive=true;state.timeParameter=PlayerAnimator.STUN_TIME;}
            if(action=="CarryIdle"){state.speed=0;state.tag="Idle";}
            var t=sm.AddAnyStateTransition(state);t.canTransitionToSelf=false;t.hasExitTime=false;t.hasFixedDuration=true;t.duration=action=="Stun"?.08f:.12f;
            Condition(t,"IsStunned",action=="Stun");
            if(action!="Stun")Condition(t,"IsJumping",action=="Jump");
            if(action=="Run"||action=="Walk"){Condition(t,"IsWalking",true);Condition(t,"IsRunning",action=="Run");}
            if(action=="Idle"||action=="CarryIdle"){Condition(t,"IsWalking",false);Condition(t,PlayerAnimator.IS_CARRYING,action=="CarryIdle");}
        }
        var maskPath=Path.GetDirectoryName(path).Replace('\\','/')+"/CarryUpperBody.mask";
        var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
        if(mask==null){mask=new AvatarMask();AssetDatabase.CreateAsset(mask,maskPath);}
        var transforms=model.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
        for(int i=0;i<transforms.Length;i++)
        {
            var p=AnimationUtility.CalculateTransformPath(transforms[i],model.transform);mask.SetTransformPath(i,p);
            bool arms=p.Contains("Spine")&&!p.Contains("Neck")&&!p.Contains("Head");mask.SetTransformActive(i,arms);
        }
        for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
        EditorUtility.SetDirty(mask);
        controller.AddLayer("Carry Upper Body");var layers=controller.layers;var layer=layers[1];layer.avatarMask=mask;layer.blendingMode=AnimatorLayerBlendingMode.Override;layer.defaultWeight=0;controller.layers=layers;
        var carry=layer.stateMachine.AddState("Carry");carry.motion=clips["CarryIdle"];carry.writeDefaultValues=false;carry.speed=0;layer.stateMachine.defaultState=carry;
        EditorUtility.SetDirty(controller);return controller;
    }
    public static string BuildVisuals()
    {
        Folder(Reports);var manifest=new Manifest();var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var prepared=JsonUtility.FromJson<LatestSkinPreparation.Result>(File.ReadAllText(Root+"/Preparation/PreparedSkins.json"));
            foreach(var skin in prepared.skins)
            {
                var wrapper=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(skin.prefab),scene);
                try
                {
                    var animator=wrapper.GetComponentInChildren<Animator>();animator.enabled=false;var model=animator.gameObject;
                    var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(skin.clips.Single(c=>c.action=="Idle").path);idle.SampleAnimation(model,0);
                    var hips=model.transform.Find("mixamorig:Hips").localPosition;
                    var folder=skin.folder+"/Animations/Gameplay";Folder(folder);
                    var clips=new Dictionary<string,AnimationClip>();
                    foreach(var clip in skin.clips)clips.Add(clip.action,GameplayClip(AssetDatabase.LoadAssetAtPath<AnimationClip>(clip.path),folder+"/"+skin.gender+"_"+skin.category+"_"+clip.action+"_Gameplay.anim",hips,clip.action,model,idle));
                    clips.Add("CarryIdle",CarryIdlePose(model,idle,clips["Carry"],folder+"/"+skin.gender+"_"+skin.category+"_CarryIdle_Gameplay.anim"));
                    var record=new Mapping{id=ID(skin.gender,skin.category),gender=skin.gender,category=skin.category};
                    record.walkSpeed=MeasureFootSpeed(model,clips["Walk"]);record.runSpeed=MeasureFootSpeed(model,clips["Run"]);
                    idle.SampleAnimation(model,0);
                    record.controller=folder+"/"+skin.gender+"_"+skin.category+"_Gameplay.controller";
                    animator.runtimeAnimatorController=BuildController(record.controller,model,clips);animator.applyRootMotion=false;animator.enabled=true;
                    var driver=model.GetComponent<PlayerAnimator>();if(driver==null)driver=model.AddComponent<PlayerAnimator>();
                    var so=new SerializedObject(driver);so.FindProperty("sourceWalkSpeed").floatValue=record.walkSpeed;so.FindProperty("sourceRunSpeed").floatValue=record.runSpeed;so.FindProperty("sourceJumpDuration").floatValue=clips["Jump"].length;
                    so.FindProperty("sourceStunDuration").floatValue=clips["Stun"].length;so.FindProperty("stunRestPoint").floatValue=(clips["Stun"].length-1.2f)/clips["Stun"].length;so.ApplyModifiedPropertiesWithoutUndo();
                    var profile=model.GetComponent<IntegratedSkinProfile>();if(profile==null)profile=model.AddComponent<IntegratedSkinProfile>();
                    profile.itemID=record.id;profile.gender=skin.gender;profile.category=skin.category;profile.referenceHeight=skin.referenceHeight;
                    profile.leftHand=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:LeftHand");profile.rightHand=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="mixamorig:RightHand");
                    clips["CarryIdle"].SampleAnimation(model,0);
                    profile.carryReferenceLocal=model.transform.InverseTransformPoint((profile.leftHand.position+profile.rightHand.position)*.5f);
                    profile.carryHandSpanLocal=Vector3.Distance(profile.leftHand.position,profile.rightHand.position)/Mathf.Abs(model.transform.lossyScale.x);
                    profile.minimumCarryForward=CarryClearance(model,clips,profile.carryReferenceLocal);
                    idle.SampleAnimation(model,0);
                    record.prefab=skin.folder+"/Prefabs/"+skin.gender+"_"+skin.category+"_Gameplay.prefab";
                    PrefabUtility.SaveAsPrefabAsset(wrapper,record.prefab);manifest.mappings.Add(record);
                }
                finally{Object.DestroyImmediate(wrapper);}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.SaveAssets();}
        File.WriteAllText(Reports+"/Mapping.json",JsonUtility.ToJson(manifest,true));
        LatestSkinAnimationReuse.Apply();
        AssetDatabase.Refresh();return "Built 12 gameplay variants and masked controllers; prepared source assets preserved; requested shared-animation mappings reapplied.";
    }
    static void Replace(GameObject identity,Mapping map)
    {
        map.oldName=identity.name;
        if(identity.GetComponentInChildren<IntegratedSkinProfile>(true)!=null)return;
        if(PrefabUtility.IsPartOfPrefabInstance(identity)&&PrefabUtility.GetNearestPrefabInstanceRoot(identity)==identity)
            PrefabUtility.UnpackPrefabInstance(identity,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var child in identity.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
        foreach(var component in identity.GetComponents<Component>())
            if(component is Animator||component is PlayerAnimator||component is Renderer||component is MeshFilter)Object.DestroyImmediate(component);
        var controller=identity.GetComponentInParent<CharacterController>();
        float y=controller!=null?controller.center.y-controller.height*.5f+controller.skinWidth:identity.transform.localPosition.y;
        identity.transform.localPosition=new Vector3(0,y,0);identity.transform.localRotation=Quaternion.identity;identity.transform.localScale=Vector3.one;
        var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(map.prefab),identity.scene);
        visual.name="Latest Skin Visual";visual.transform.SetParent(identity.transform,false);
        visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
        // Keep the prepared prefab's equal X/Y/Z scale; do not normalize it to one.
    }
    static void EquipRoots(GameObject root,Manifest manifest)
    {
        foreach(var loader in root.GetComponentsInChildren<PlayerEquipmentLoader>(true))
        {
            var so=new SerializedObject(loader);var models=so.FindProperty("clothesModels");
            for(int i=0;i<models.arraySize;i++)
            {
                var entry=models.GetArrayElementAtIndex(i);var id=entry.FindPropertyRelative("itemID").stringValue;
                var map=manifest.mappings.FirstOrDefault(m=>PlayerEquipmentLoader.SameEquipmentID(m.id,id));
                if(map!=null)Replace((GameObject)entry.FindPropertyRelative("model").objectReferenceValue,map);
            }
            var player=loader.GetComponent<Player>();
            if(player!=null)
            {
                var rig=loader.GetComponent<PlayerCarryRig>();if(rig==null)rig=loader.gameObject.AddComponent<PlayerCarryRig>();rig.anchor=player.GetHoldPoint();
            }
        }
    }
    public static string ApplyToPlayers()
    {
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Reports+"/Mapping.json"));
        foreach(var path in BasePlayers)
        {
            Backup(path);var root=PrefabUtility.LoadPrefabContents(path);
            try{EquipRoots(root,manifest);PrefabUtility.SaveAsPrefabAsset(root,path);manifest.locations.Add(path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        File.WriteAllText(Reports+"/Mapping.json",JsonUtility.ToJson(manifest,true));AssetDatabase.SaveAssets();return "Replaced visuals behind all 12 existing player equipment identity roots.";
    }
    public static string ApplyToMainMenu()
    {
        const string path="Assets/Scenes/Main Menu.unity";Backup(path);
        var original=SceneManager.GetActiveScene();var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Reports+"/Mapping.json"));
        try
        {
            var done=new HashSet<GameObject>();
            foreach(var root in scene.GetRootGameObjects())foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(!(component is CustomizationManager)&&!(component is ShopPreviewUI)&&!(component is MainMenuProfileSection))continue;
                var so=new SerializedObject(component);var models=so.FindProperty(component is CustomizationManager?"mannequinModels":"models");
                for(int i=0;models!=null&&i<models.arraySize;i++)
                {
                    var entry=models.GetArrayElementAtIndex(i);var id=entry.FindPropertyRelative("itemID").stringValue;
                    var map=manifest.mappings.FirstOrDefault(m=>PlayerEquipmentLoader.SameEquipmentID(m.id,id));var identity=entry.FindPropertyRelative("model").objectReferenceValue as GameObject;
                    if(map!=null&&identity!=null&&done.Add(identity))Replace(identity,map);
                }
            }
            EditorSceneManager.SaveScene(scene);manifest.locations.Add(path+" ("+done.Count+" shared mannequin identity roots)");
        }
        finally{SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
        File.WriteAllText(Reports+"/Mapping.json",JsonUtility.ToJson(manifest,true));return "Updated customization, shop and profile mannequins without changing IDs or UI references.";
    }
}
#endif
