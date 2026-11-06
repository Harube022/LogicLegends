#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Assign existing clips only. No FBX, .anim, Avatar or saved equipment changes.</summary>
public static class LatestSkinAnimationReuse
{
    public const string Report = LatestSkinIntegration.Reports + "/AnimationReuseChecks.txt";
    public struct Request
    {
        public string target, donor, action;
        public Request(string target, string action, string donor) { this.target=target; this.action=action; this.donor=donor; }
    }
    public static readonly Request[] Requests = {
        new Request("Male_Uniform","Walk","Male_Elite"), new Request("Male_Uniform","Run","Male_Elite"),
        new Request("Male_Special","Walk","Male_Elite"), new Request("Male_Rare","Walk","Male_Elite"),
        new Request("Male_Epic","Jump","Male_Common"),
        new Request("Male_Common","Walk","Male_Elite"), new Request("Male_Common","Run","Male_Elite"),
        new Request("Female_Uniform","Run","Female_Special"), new Request("Female_Epic","Walk","Female_Rare")
    };
    static string Name(LatestSkinIntegration.Mapping m) => m.gender+"_"+m.category;
    static AnimatorState State(LatestSkinIntegration.Mapping m,string action) =>
        AssetDatabase.LoadAssetAtPath<AnimatorController>(m.controller).layers[0].stateMachine.states.Single(s=>s.state.name==action).state;
    public static AnimationClip OriginalClip(LatestSkinIntegration.Mapping m,string action) =>
        AssetDatabase.LoadAssetAtPath<AnimationClip>(Path.GetDirectoryName(m.controller).Replace('\\','/')+"/"+Name(m)+"_"+action+"_Gameplay.anim");

    static Dictionary<string,Matrix4x4> Bind(Animator model)
    {
        var output=new Dictionary<string,Matrix4x4>();
        foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            for(int i=0;i<renderer.bones.Length;i++)
            {
                var path=AnimationUtility.CalculateTransformPath(renderer.bones[i],model.transform);
                output[path]=model.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix*renderer.sharedMesh.bindposes[i].inverse;
            }
        output[""]=Matrix4x4.identity;return output;
    }
    static string Parent(string path) { int i=path.LastIndexOf('/');return i<0?"":path.Substring(0,i); }
    static float LegLength(Animator model)
    {
        var bones=model.GetComponentsInChildren<Transform>(true);
        return bones.Single(t=>t.name=="mixamorig:LeftLeg").localPosition.magnitude+
               bones.Single(t=>t.name=="mixamorig:LeftFoot").localPosition.magnitude;
    }
    internal static SharedSkinAnimationRetarget.Contact[] SoleContacts(Animator model)
    {
        var contacts=new List<SharedSkinAnimationRetarget.Contact>();
        foreach(string side in new[]{"Left","Right"})
        {
            var candidates=new List<SharedSkinAnimationRetarget.Contact>();
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                for(int v=0;v<vertices.Length;v++)
                {
                    var w=weights[v];var indices=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                    float foot=0;for(int i=0;i<4;i++)if(renderer.bones[indices[i]].name.StartsWith("mixamorig:"+side)&&
                        (renderer.bones[indices[i]].name.Contains("Foot")||renderer.bones[indices[i]].name.Contains("Toe")))foot+=values[i];
                    if(foot<.5f)continue;
                    var bones=new List<Transform>();var points=new List<Vector3>();var contributions=new List<float>();
                    for(int i=0;i<4;i++)if(values[i]>0){bones.Add(renderer.bones[indices[i]]);points.Add(mesh.bindposes[indices[i]].MultiplyPoint3x4(vertices[v]));contributions.Add(values[i]);}
                    candidates.Add(new SharedSkinAnimationRetarget.Contact{bones=bones.ToArray(),bindPoints=points.ToArray(),weights=contributions.ToArray()});
                }
            }
            if(candidates.Count==0)throw new InvalidOperationException("Missing foot mesh weights: "+model.name+" "+side);
            // Cover the sole and toe/heel extremes, including points that become
            // lowest as the foot rotates. Thirty-two per foot is cheap at runtime.
            var selected=new List<SharedSkinAnimationRetarget.Contact>{candidates.OrderBy(c=>c.WorldPoint().y).First()};
            candidates.Remove(selected[0]);
            while(selected.Count<32&&candidates.Count>0)
            {
                var next=candidates.OrderByDescending(c=>selected.Min(s=>(s.WorldPoint()-c.WorldPoint()).sqrMagnitude)).First();
                selected.Add(next);candidates.Remove(next);
            }
            contacts.AddRange(selected);
        }
        return contacts.ToArray();
    }
    static SharedSkinAnimationRetarget Configure(Animator target,Animator donor,AnimationClip targetIdle,AnimationClip donorIdle)
    {
        var source=Bind(donor);var destination=Bind(target);
        var adapter=target.GetComponent<SharedSkinAnimationRetarget>()??target.gameObject.AddComponent<SharedSkinAnimationRetarget>();
        var bones=new List<SharedSkinAnimationRetarget.Bone>();
        foreach(var pair in source.Where(p=>p.Key!=""))
        {
            string parent=Parent(pair.Key);
            if(!destination.ContainsKey(pair.Key)||!source.ContainsKey(parent)||!destination.ContainsKey(parent))
                throw new InvalidOperationException("Unmatched weighted bone/parent: "+pair.Key);
            bones.Add(new SharedSkinAnimationRetarget.Bone {
                target=target.transform.Find(pair.Key),
                before=Quaternion.Inverse(destination[parent].rotation)*source[parent].rotation,
                after=Quaternion.Inverse(pair.Value.rotation)*destination[pair.Key].rotation,
                carryMasked=pair.Key.Contains("Spine")&&!pair.Key.Contains("Neck")&&!pair.Key.Contains("Head")
            });
        }
        adapter.bones=bones.ToArray();adapter.hips=target.transform.Find("mixamorig:Hips");
        donorIdle.SampleAnimation(donor.gameObject,0);targetIdle.SampleAnimation(target.gameObject,0);
        adapter.sourceHipsOrigin=LatestSkinPlaybackSetup.BindHips(donor);
        adapter.targetHipsOrigin=LatestSkinPlaybackSetup.BindHips(target);
        adapter.hipScale=LegLength(target)/LegLength(donor);
        Bounds standing;IntegratedSkinProfile.TryVisualBounds(target.transform,out standing);
        adapter.floorLocalY=target.transform.InverseTransformPoint(new Vector3(target.transform.position.x,standing.min.y,target.transform.position.z)).y;
        adapter.soleContacts=new SharedSkinAnimationRetarget.Contact[0];
        return adapter;
    }
    public static void Sample(Animator model,SharedSkinAnimationRetarget adapter,SharedSkinAnimationRetarget.Motion motion,float phase,bool grounded=true)
    {
        motion.clip.SampleAnimation(model.gameObject,motion.clip.length*phase);
        adapter.CorrectPose(1,grounded?motion.groundOffset.Evaluate(phase):0,0);
    }
    static float FootSpeed(Animator model,SharedSkinAnimationRetarget adapter,SharedSkinAnimationRetarget.Motion motion)
    {
        var feet=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="mixamorig:LeftFoot"||t.name=="mixamorig:RightFoot").ToArray();
        int count=Mathf.Max(40,Mathf.CeilToInt(motion.clip.length*60));
        var p=new List<Vector3[]>();var y=new List<float[]>();
        for(int i=0;i<=count;i++) { Sample(model,adapter,motion,(float)i/count);p.Add(feet.Select(f=>model.transform.InverseTransformVector(f.position-adapter.hips.position)).ToArray());y.Add(feet.Select(f=>model.transform.InverseTransformPoint(f.position).y).ToArray()); }
        var speeds=new List<float>();
        for(int f=0;f<feet.Length;f++)
        {
            float low=y.Min(h=>h[f]),high=y.Max(h=>h[f]);
            for(int i=1;i<p.Count;i++){float speed=-(p[i][f].z-p[i-1][f].z)/(motion.clip.length/count);if(y[i][f]<=Mathf.Lerp(low,high,.45f)&&speed>0.00001f)speeds.Add(speed);}
        }
        speeds.Sort();if(speeds.Count==0)throw new InvalidOperationException("No stride contacts: "+model.name);
        return speeds[Mathf.Min(speeds.Count-1,Mathf.FloorToInt(speeds.Count*.85f))];
    }
    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use Edit Mode");
        var manifest=JsonUtility.FromJson<LatestSkinIntegration.Manifest>(File.ReadAllText(LatestSkinIntegration.Reports+"/Mapping.json"));
        File.WriteAllText(Report,"Existing Generic clips reused; bind-space retargeting, original target proportions and carry mask retained.\n");
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var group in Requests.GroupBy(r=>r.target))
            {
                var target=manifest.mappings.Single(m=>Name(m)==group.Key);var donor=manifest.mappings.Single(m=>Name(m)==group.First().donor);
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(target.prefab),scene);
                var donorRoot=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(donor.prefab),scene);
                try
                {
                    var a=root.GetComponentInChildren<Animator>(true);a.enabled=false;
                    a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    var d=donorRoot.GetComponentInChildren<Animator>(true);d.enabled=false;
                    if(a.isHuman||d.isHuman||a.avatar!=null||d.avatar!=null)throw new InvalidOperationException("Recheck changed rig types before using Generic retargeting");
                    var idle=OriginalClip(target,"Idle");idle.SampleAnimation(a.gameObject,0);Bounds standing;IntegratedSkinProfile.TryVisualBounds(a.transform,out standing);
                    var adapter=Configure(a,d,idle,OriginalClip(donor,"Idle"));adapter.donorSkin=Name(donor);
                    adapter.motions=group.Select(r=>new SharedSkinAnimationRetarget.Motion{state=r.action,clip=OriginalClip(donor,r.action)}).ToArray();
                    var driver=new SerializedObject(a.GetComponent<PlayerAnimator>());
                    foreach(var motion in adapter.motions)
                    {
                        var bindings=AnimationUtility.GetCurveBindings(motion.clip);
                        foreach(var binding in bindings)
                            if(!string.IsNullOrEmpty(binding.path)&&a.transform.Find(binding.path)==null)throw new InvalidOperationException("Missing clip binding: "+binding.path);
                        // These clips animate hips translation and joint rotations, not
                        // limb lengths. Leave every destination limb offset untouched.
                        if(bindings.Any(b=>b.path!="mixamorig:Hips"&&b.propertyName.StartsWith("m_LocalPosition")))
                            throw new InvalidOperationException("Recheck translated limb bindings before reusing "+motion.clip.name);
                        foreach(var bone in adapter.bones)
                        {
                            string path=AnimationUtility.CalculateTransformPath(bone.target,a.transform);
                            if(bindings.Count(b=>b.path==path&&b.propertyName.StartsWith("m_LocalRotation"))!=4)
                                throw new InvalidOperationException("Incomplete animated rotation: "+motion.clip.name+" "+path);
                        }
                        motion.groundOffset=new AnimationCurve();
                        var state=State(target,motion.state);state.motion=motion.clip;
                        if(motion.state=="Jump")driver.FindProperty("sourceJumpDuration").floatValue=motion.clip.length;
                        else
                        {
                            float speed=FootSpeed(a,adapter,motion);string lower=motion.state=="Walk"?"walk":"run";
                            driver.FindProperty("source"+motion.state+"Speed").floatValue=speed;
                            // Bounded playback is configured after all assignments below.
                            if(motion.state=="Walk")target.walkSpeed=speed;else target.runSpeed=speed;
                        }
                        EditorUtility.SetDirty(AssetDatabase.LoadAssetAtPath<AnimatorController>(target.controller));
                        File.AppendAllText(Report,"ASSIGN "+group.Key+" "+motion.state+" <- "+Name(donor)+" exact clip GUID="+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(motion.clip))+"; mapped bones="+adapter.bones.Length+"; leg ratio="+adapter.hipScale.ToString("F4")+"\n");
                    }
                    File.AppendAllText(Report,"BIND CONVERSION "+group.Key+": mesh bind origins; no procedural ground/stride corrections.\n");
                    driver.ApplyModifiedPropertiesWithoutUndo();idle.SampleAnimation(a.gameObject,0);a.enabled=true;
                    PrefabUtility.SaveAsPrefabAsset(root,target.prefab);
                }
                finally{Object.DestroyImmediate(root);Object.DestroyImmediate(donorRoot);}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.SaveAssets();}
        File.WriteAllText(LatestSkinIntegration.Reports+"/Mapping.json",JsonUtility.ToJson(manifest,true));
        LatestSkinPlaybackSetup.Apply();
        return "Reused nine existing motions on seven skins; no animation clips created or modified.";
    }
}
#endif
