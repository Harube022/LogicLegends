#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Asset preparation only. Never assigns anything to live players, menus, or scenes.</summary>
public static class LatestSkinPreparation
{
    public const string Root = "Assets/Characters_NEW/LATEST_SKINS";
    const string Reports = Root + "/Preparation";
    static readonly string[] Actions = { "Carry", "Jump", "Idle", "Run", "Stun", "Walk" };
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    [Serializable] public class Stamp { public string path, sha; }
    [Serializable] public class Baseline { public List<Stamp> live = new List<Stamp>(); public List<Stamp> sources = new List<Stamp>(); }
    [Serializable] public class ClipInfo
    {
        public string action, source, sourceGuid, path, fingerprint;
        public float duration, frameRate;
        public int bindings, framesSampled;
        public bool curveCopyExact, pathsMatch, animationMoves, playableWorks;
    }
    [Serializable] public class SkinInfo
    {
        public string gender, category, folder, model, modelGuid, prefab, material, reference;
        public float nativeHeight, referenceHeight, uniformMultiplier, finalHeight, footOffset, groundedMinY;
        public int vertexCount, bones;
        public List<string> missing = new List<string>();
        public List<ClipInfo> clips = new List<ClipInfo>();
        public bool validMesh, validMaterials, validBones, uniformScale, boundsContainAnimation;
    }
    [Serializable] public class Result
    {
        public string scope = "Preparation only; Generic rigs and original clip curves retained. No live assignments.";
        public List<SkinInfo> skins = new List<SkinInfo>();
        public List<string> changedLiveFiles = new List<string>(), changedSourceBytes = new List<string>();
        public int verifiedClips;
    }

    static void Folder(string path)
    {
        path=path.Replace('\\','/');
        if(AssetDatabase.IsValidFolder(path))return;
        var parent=Path.GetDirectoryName(path).Replace('\\','/');
        Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    static string Hash(string path)
    {
        using(var sha=SHA256.Create()) using(var stream=File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
    }
    public static string CaptureBaseline()
    {
        Folder(Reports); var b=new Baseline();
        foreach(var p0 in Directory.GetFiles("Assets","*",SearchOption.AllDirectories))
        {
            var p=p0.Replace('\\','/');if(p.StartsWith(Root+"/",StringComparison.Ordinal))continue;
            var ext=Path.GetExtension(p).ToLowerInvariant();
            if(ext==".prefab"||ext==".unity"||ext==".controller"||ext==".cs")
                b.live.Add(new Stamp{path=p,sha=Hash(p)});
        }
        foreach(var p0 in Directory.GetFiles(Root,"*",SearchOption.AllDirectories))
        {
            var p=p0.Replace('\\','/');var ext=Path.GetExtension(p).ToLowerInvariant();
            if(ext==".fbx"||ext==".png")b.sources.Add(new Stamp{path=AssetDatabase.AssetPathToGUID(p),sha=Hash(p)});
        }
        File.WriteAllText(Reports+"/Baseline.json",JsonUtility.ToJson(b,true));
        return "Captured "+b.live.Count+" existing live files and "+b.sources.Count+" FBX/texture sources.";
    }
    static AnimationClip ClipAt(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));
    static string Number(float x)=>x.ToString("R",CI);
    static string Fingerprint(AnimationClip c)
    {
        var s=new StringBuilder();
        s.Append(Number(c.length)).Append('|').Append(Number(c.frameRate)).Append('|').Append(c.legacy).Append('|').Append(c.wrapMode);
        foreach(var b in AnimationUtility.GetCurveBindings(c).OrderBy(b=>b.path+"|"+b.type.FullName+"|"+b.propertyName))
        {
            s.Append(b.path).Append('|').Append(b.type.FullName).Append('|').Append(b.propertyName);
            var curve=AnimationUtility.GetEditorCurve(c,b);
            s.Append(curve.preWrapMode).Append(curve.postWrapMode);
            foreach(var k in curve.keys)
                s.Append('|').Append(Number(k.time)).Append(',').Append(Number(k.value)).Append(',').Append(Number(k.inTangent)).Append(',').Append(Number(k.outTangent)).Append(',').Append(Number(k.inWeight)).Append(',').Append(Number(k.outWeight)).Append(',').Append(k.weightedMode);
        }
        foreach(var b in AnimationUtility.GetObjectReferenceCurveBindings(c))
        {
            s.Append(b.path).Append(b.propertyName);
            foreach(var k in AnimationUtility.GetObjectReferenceCurve(c,b))s.Append(Number(k.time)).Append(AssetDatabase.GetAssetPath(k.value));
        }
        foreach(var e in AnimationUtility.GetAnimationEvents(c))s.Append(JsonUtility.ToJson(e));
        s.Append(JsonUtility.ToJson(AnimationUtility.GetAnimationClipSettings(c)));
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()))).Replace("-","");
    }
    public static Bounds BakedBounds(GameObject g)
    {
        bool found=false;var bounds=new Bounds();var mesh=new Mesh();
        try
        {
            foreach(var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.BakeMesh(mesh,true);foreach(var v in mesh.vertices)
                {var w=r.transform.TransformPoint(v);if(!found){bounds=new Bounds(w,Vector3.zero);found=true;}else bounds.Encapsulate(w);}
                mesh.Clear();
            }
        }
        finally{Object.DestroyImmediate(mesh);}
        if(!found)throw new InvalidOperationException("No skinned mesh vertices: "+g.name);
        return bounds;
    }
    static Bounds SkinLocalBounds(SkinnedMeshRenderer renderer,Mesh mesh)
    {
        renderer.BakeMesh(mesh,true);
        var space=renderer.rootBone!=null?renderer.rootBone:renderer.transform;
        bool found=false;var bounds=new Bounds();
        foreach(var vertex in mesh.vertices)
        {
            var point=space.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
            if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
        }
        return bounds;
    }
    static GameObject Clone(GameObject asset,Scene scene)
    {
        // Instantiate directly into the preview scene; never dirty the user's open scene.
        var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
        foreach(var a in g.GetComponentsInChildren<Animator>(true))a.enabled=false;
        g.SetActive(true);return g;
    }
    static float ReferenceHeight(string gender,Scene scene,out string reference)
    {
        var path="Assets/Resources/Multiplayer_"+gender+".prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var name=gender=="Female"?"UNIFORM_FEMALE":"eka_costume1";
        var skin=source.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        var g=Clone(skin.gameObject,scene);
        try
        {
            g.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var a=g.GetComponent<Animator>();
            var clip=a.runtimeAnimatorController.animationClips.First(c=>c.name.ToUpperInvariant().Contains("IDLE"));
            clip.SampleAnimation(g,0);reference=path+" :: "+name+" :: "+clip.name;
            return BakedBounds(g).size.y;
        }
        finally{Object.DestroyImmediate(g);}
    }
    static void CopyClip(AnimationClip source,string path,string name)
    {
        var copy=Object.Instantiate(source);copy.name=name;copy.hideFlags=HideFlags.None;
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing!=null){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);EditorUtility.SetDirty(existing);}
        else AssetDatabase.CreateAsset(copy,path);
    }
    static AnimatorController Controller(string path,List<ClipInfo> clips)
    {
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
        var sm=controller.layers[0].stateMachine;
        foreach(var c in sm.states)sm.RemoveState(c.state);
        foreach(var c in clips)
        {
            var state=sm.AddState(c.action);state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(c.path);state.speed=1;state.writeDefaultValues=true;
            if(c.action=="Idle")sm.defaultState=state;
        }
        EditorUtility.SetDirty(controller);return controller;
    }

    [MenuItem("Logic Legends/Prepared Skins/Prepare LATEST_SKINS (No Gameplay Integration)")]
    public static void PrepareAll()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
        if(!File.Exists(Reports+"/Baseline.json"))CaptureBaseline();
        var result=new Result();var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(var gender in new[]{"Female","Male"})
            {
                string reference;float referenceHeight=ReferenceHeight(gender,scene,out reference);
                foreach(var folder0 in Directory.GetDirectories(Root+"/"+gender+"_Skins").OrderBy(p=>p))
                {
                    var folder=folder0.Replace('\\','/');var category=Path.GetFileName(folder)=="Elte"?"Elite":Path.GetFileName(folder);
                    var id=gender+"_"+category;var item=new SkinInfo{gender=gender,category=category,folder=folder,reference=reference,referenceHeight=referenceHeight};
                    var model=folder+"/"+id+"_Skin.fbx";var old=folder+"/Idle.fbx";
                    if(AssetDatabase.LoadAssetAtPath<GameObject>(model)==null)
                    {var error=AssetDatabase.RenameAsset(old,id+"_Skin");if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);}
                    item.model=model;item.modelGuid=AssetDatabase.AssetPathToGUID(model);
                    Folder(folder+"/Animations");Folder(folder+"/Materials");Folder(folder+"/Prefabs");Folder(folder+"/Previews");
                    foreach(var action in Actions)
                    {
                        var sourcePath=action=="Idle"?model:folder+"/"+action+".fbx";
                        var clip=ClipAt(sourcePath);
                        if(clip==null){item.missing.Add(action+".fbx / "+action+" clip");continue;}
                        var name=id+"_"+action;var dest=folder+"/Animations/"+name+".anim";
                        CopyClip(clip,dest,name);
                        item.clips.Add(new ClipInfo{action=action,source=sourcePath,sourceGuid=AssetDatabase.AssetPathToGUID(sourcePath),path=dest,duration=clip.length,frameRate=clip.frameRate,bindings=AnimationUtility.GetCurveBindings(clip).Length,fingerprint=Fingerprint(clip)});
                    }
                    AssetDatabase.SaveAssets();
                    var wrapper=new GameObject(id+"_Skin");SceneManager.MoveGameObjectToScene(wrapper,scene);
                    try
                    {
                        var feet=new GameObject("Feet Offset");SceneManager.MoveGameObjectToScene(feet,scene);feet.transform.SetParent(wrapper.transform,false);
                        var modelGO=Clone(AssetDatabase.LoadAssetAtPath<GameObject>(model),scene);modelGO.transform.SetParent(feet.transform,false);
                        modelGO.transform.localPosition=Vector3.zero;modelGO.transform.localRotation=Quaternion.identity;modelGO.transform.localScale=Vector3.one;
                        var renderer=modelGO.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        item.validBones=renderer.All(r=>r.rootBone!=null&&r.rootBone.IsChildOf(modelGO.transform)&&r.bones.All(b=>b!=null&&b.IsChildOf(modelGO.transform)));
                        item.bones=renderer.Sum(r=>r.bones.Length);item.vertexCount=renderer.Sum(r=>r.sharedMesh==null?0:r.sharedMesh.vertexCount);item.validMesh=item.vertexCount>0;
                        var texturePath=Directory.GetFiles(folder,"*_basecolor.png").SingleOrDefault();
                        if(texturePath==null)throw new InvalidOperationException("Missing basecolor: "+folder);
                        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath.Replace('\\','/'));
                        var matPath=folder+"/Materials/"+id+"_Skin.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if(mat==null){mat=new Material(renderer[0].sharedMaterial);mat.name=id+"_Skin";AssetDatabase.CreateAsset(mat,matPath);}
                        mat.shader=Shader.Find("Universal Render Pipeline/Lit");mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);
                        mat.SetFloat("_Surface",0);mat.SetFloat("_AlphaClip",0);mat.SetFloat("_Smoothness",renderer[0].sharedMaterial.GetFloat("_Smoothness"));mat.SetFloat("_Metallic",renderer[0].sharedMaterial.GetFloat("_Metallic"));
                        mat.renderQueue=-1;EditorUtility.SetDirty(mat);item.material=matPath;
                        foreach(var r in renderer){r.enabled=true;r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();r.updateWhenOffscreen=false;}
                        item.validMaterials=texture!=null&&mat.shader!=null&&mat.shader.isSupported;
                        var envelopes=renderer.ToDictionary(r=>r,r=>new Bounds());var sampled=new HashSet<SkinnedMeshRenderer>();var mesh=new Mesh();
                        try
                        {
                            foreach(var info in item.clips)
                            {
                                var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(info.path);
                                foreach(var binding in AnimationUtility.GetCurveBindings(c))
                                    if(!string.IsNullOrEmpty(binding.path)&&modelGO.transform.Find(binding.path)==null)throw new InvalidOperationException("Unmatched binding: "+id+"/"+info.action+"/"+binding.path);
                                info.pathsMatch=true;info.curveCopyExact=Fingerprint(c)==info.fingerprint;
                                for(int sample=0;sample<=24;sample++)
                                {
                                    c.SampleAnimation(modelGO,c.length*sample/24f);
                                    foreach(var r in renderer)
                                    {var b=SkinLocalBounds(r,mesh);if(!sampled.Contains(r)){envelopes[r]=b;sampled.Add(r);}else{var combined=envelopes[r];combined.Encapsulate(b);envelopes[r]=combined;}mesh.Clear();}
                                    info.framesSampled++;
                                }
                            }
                        }
                        finally{Object.DestroyImmediate(mesh);}
                        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(item.clips.Single(c=>c.action=="Idle").path);
                        idle.SampleAnimation(modelGO,0);var native=BakedBounds(modelGO);item.nativeHeight=native.size.y;
                        item.uniformMultiplier=referenceHeight/native.size.y;wrapper.transform.localScale=Vector3.one*item.uniformMultiplier;
                        item.footOffset=-native.min.y;feet.transform.localPosition=Vector3.up*item.footOffset;
                        foreach(var r in renderer){var b=envelopes[r];b.Expand(Mathf.Max(.0005f,b.size.magnitude*.03f));r.localBounds=b;}
                        item.boundsContainAnimation=true;
                        var final=BakedBounds(modelGO);item.finalHeight=final.size.y;item.groundedMinY=final.min.y;
                        item.uniformScale=wrapper.transform.localScale.x==wrapper.transform.localScale.y&&wrapper.transform.localScale.y==wrapper.transform.localScale.z;
                        var animator=modelGO.GetComponent<Animator>();if(animator==null)animator=modelGO.AddComponent<Animator>();
                        animator.avatar=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                        animator.runtimeAnimatorController=Controller(folder+"/Animations/"+id+"_Preview.controller",item.clips);animator.enabled=true;
                        item.prefab=folder+"/Prefabs/"+id+"_Skin.prefab";PrefabUtility.SaveAsPrefabAsset(wrapper,item.prefab);
                    }
                    finally{Object.DestroyImmediate(wrapper);}
                    result.skins.Add(item);
                    File.WriteAllText(Reports+"/PreparedSkins.json",JsonUtility.ToJson(result,true));
                    Debug.Log("Prepared "+id+": "+item.clips.Count+" clips, uniform x"+item.uniformMultiplier.ToString("F3",CI)+", "+item.finalHeight.ToString("F3",CI)+" high"+(item.missing.Count>0?" — MISSING "+string.Join(", ",item.missing):""));
                }
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);AssetDatabase.SaveAssets();}
        File.WriteAllText(Reports+"/PreparedSkins.json",JsonUtility.ToJson(result,true));
        AssetDatabase.Refresh();
    }

    public static string VerifyAndRender(string gender)
    {
        var result=JsonUtility.FromJson<Result>(File.ReadAllText(Reports+"/PreparedSkins.json"));
        var scene=EditorSceneManager.NewPreviewScene();int count=0;
        try
        {
            foreach(var item in result.skins.Where(s=>s.gender==gender))
            {
                var g=Clone(AssetDatabase.LoadAssetAtPath<GameObject>(item.prefab),scene);
                try
                {
                    var animator=g.GetComponentInChildren<Animator>(true);animator.enabled=true;
                    var model=animator.gameObject;var bones=animator.GetComponentsInChildren<Transform>(true);
                    foreach(var info in item.clips)
                    {
                        animator.enabled=true;
                        var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(info.path);
                        if(Fingerprint(c)!=Fingerprint(ClipAt(info.source)))throw new InvalidOperationException("Curve/timing/settings mismatch: "+info.path);
                        var graph=PlayableGraph.Create("Prepared Skin Isolated Verification");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        try
                        {
                            var playable=AnimationClipPlayable.Create(graph,c);
                            var output=AnimationPlayableOutput.Create(graph,"Preview",animator);output.SetSourcePlayable(playable);
                            graph.Play();playable.SetTime(0);graph.Evaluate(0);
                            var first=bones.Select(t=>t.localToWorldMatrix).ToArray();
                            for(int sample=1;sample<=24;sample++)
                            {
                                playable.SetTime(c.length*sample/24f);graph.Evaluate(0);
                                if(bones.Where((t,i)=>t.localToWorldMatrix!=first[i]).Any())info.animationMoves=true;
                                foreach(var r in g.GetComponentsInChildren<SkinnedMeshRenderer>())
                                {
                                    var mesh=new Mesh();try{var b=SkinLocalBounds(r,mesh);
                                    if(!r.localBounds.Contains(b.min)||!r.localBounds.Contains(b.max))throw new InvalidOperationException("Animated bounds do not contain "+info.path);}
                                    finally{Object.DestroyImmediate(mesh);}
                                }
                            }
                            info.playableWorks=info.animationMoves;
                            if(!info.playableWorks)throw new InvalidOperationException("No animation movement on actual prefab: "+info.path);
                        }
                        finally{graph.Destroy();}
                        animator.enabled=false;
                        c.SampleAnimation(model,c.length*.35f);
                        Render(item,info,g);
                        count++;
                    }
                    var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(item.clips.Single(c=>c.action=="Idle").path);idle.SampleAnimation(model,0);
                    var actual=BakedBounds(g);
                    if(Mathf.Abs(actual.min.y)>.005f||Mathf.Abs(actual.size.y-item.referenceHeight)>.01f)throw new InvalidOperationException("Ground/size mismatch: "+item.prefab+" "+actual);
                }
                finally{Object.DestroyImmediate(g);}
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
        var baseline=JsonUtility.FromJson<Baseline>(File.ReadAllText(Reports+"/Baseline.json"));
        result.changedLiveFiles=baseline.live.Where(s=>!File.Exists(s.path)||Hash(s.path)!=s.sha).Select(s=>s.path).ToList();
        result.changedSourceBytes=baseline.sources.Where(s=>{var p=AssetDatabase.GUIDToAssetPath(s.path);return !File.Exists(p)||Hash(p)!=s.sha;}).Select(s=>s.path).ToList();
        result.verifiedClips=result.skins.Sum(s=>s.clips.Count(c=>c.playableWorks));
        File.WriteAllText(Reports+"/PreparedSkins.json",JsonUtility.ToJson(result,true));AssetDatabase.Refresh();
        return gender+": "+count+" clips verified through actual Animator Playables and rendered. Live files changed="+result.changedLiveFiles.Count+"; original FBX/texture bytes changed="+result.changedSourceBytes.Count;
    }

    static void Render(SkinInfo item,ClipInfo info,GameObject g)
    {
        var preview=new PreviewRenderUtility();
        try
        {
            // A separate preview clone keeps the verification model and all user scenes untouched.
            var copy=Object.Instantiate(g);preview.AddSingleGO(copy);
            var a=copy.GetComponentInChildren<Animator>();if(a!=null)a.enabled=false;
            copy.transform.position=Vector3.zero;copy.transform.rotation=Quaternion.identity;
            var b=BakedBounds(copy);
            preview.cameraFieldOfView=32;
            var center=new Vector3(b.center.x,b.center.y,b.center.z);
            float distance=Mathf.Max(b.size.y*2.1f,b.size.x*2.1f);
            preview.camera.transform.position=center+new Vector3(.25f,.07f,1f).normalized*distance;
            preview.camera.transform.LookAt(center);
            preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=200;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.08f,.1f,.13f,1);
            preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);
            preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(20,30,0);
            preview.ambientColor=new Color(.45f,.45f,.45f,1);
            preview.BeginStaticPreview(new Rect(0,0,320,480));preview.Render(true,true);
            var image=preview.EndStaticPreview();
            File.WriteAllBytes(item.folder+"/Previews/"+item.gender+"_"+item.category+"_"+info.action+".png",image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
        finally{preview.Cleanup();}
    }
}
#endif
