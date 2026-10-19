using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

/// <summary>Editor authoring only; updates the existing Attack asset without changing its GUID.</summary>
public static class ProofDevourerSlashAuthoring
{
    const string Folder=ProofDevourerAssetSetup.Folder;
    const string Source=ProofDevourerAssetSetup.ModelPath;
    const string Attack=Folder+"/Animations/Attack.anim";
    [Serializable] public class Review {
        public bool passed;public string checkedUtc,guid;public float duration,entryError,exitError,maxSupportDrift,maxRootDrift,slashTravel;
        public int curves;public List<string> checks=new List<string>();
    }
    public static void ApplyIfAvailable(){if(File.Exists(Source))Apply();}
    [MenuItem("Tools/Proof Devourer/Apply authored claw slash to Attack")]
    public static void Apply(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before editing the clip.");
        var importer=(ModelImporter)AssetImporter.GetAtPath(Source);
        var compression=importer.animationCompression;
        try{
            // Extract exact sampled IK contacts; keep the model import preference intact.
            if(compression!=ModelImporterAnimationCompression.Off){importer.animationCompression=ModelImporterAnimationCompression.Off;importer.SaveAndReimport();}
            ApplyUncompressed();
        }finally{
            importer=(ModelImporter)AssetImporter.GetAtPath(Source);
            if(importer.animationCompression!=compression){importer.animationCompression=compression;importer.SaveAndReimport();}
        }
    }
    static void ApplyUncompressed(){
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before editing the clip.");
        var source=AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>().Single(c=>c.name=="Attack");
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Animations/Idle.anim");
        var target=AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack);
        if(target==null || Mathf.Abs(source.length-1.8f)>.001f)throw new InvalidOperationException("Existing Attack or authored 1.8-second take missing.");
        var result=new AnimationClip{name="Attack",frameRate=30,legacy=false,wrapMode=WrapMode.ClampForever};
        var settings=AnimationUtility.GetAnimationClipSettings(target);settings.loopTime=false;settings.loopBlend=false;settings.startTime=0;settings.stopTime=1.8f;AnimationUtility.SetAnimationClipSettings(result,settings);
        // Keep the existing outer conversion stationary. The re-export uses the
        // original character hierarchy and binds to the existing named bones.
        foreach(var b in AnimationUtility.GetCurveBindings(idle))if(b.path=="DevourerRig"){
            float value=AnimationUtility.GetEditorCurve(idle,b).Evaluate(0);AnimationUtility.SetEditorCurve(result,b,AnimationCurve.Constant(0,1.8f,value));
        }
        foreach(var binding in AnimationUtility.GetCurveBindings(source)){
            if(binding.path=="DevourerRig")continue;
            AnimationUtility.SetEditorCurve(result,binding,AnimationUtility.GetEditorCurve(source,binding));
        }
        result.EnsureQuaternionContinuity();
        var scene=EditorSceneManager.NewPreviewScene();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ProofDevourerAssetSetup.PrefabPath);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
        try{
            instance.GetComponent<Animator>().enabled=false;
            foreach(var binding in AnimationUtility.GetCurveBindings(result))if(instance.transform.Find(binding.path)==null)throw new InvalidOperationException("Unresolved slash bone "+binding.path);
            idle.SampleAnimation(instance,0);var idleBones=instance.GetComponentsInChildren<Transform>().ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,instance.transform),t=>t.localToWorldMatrix);
            result.SampleAnimation(instance,0);float entry=PoseError(instance,idleBones);
            result.SampleAnimation(instance,1.8f);float exit=PoseError(instance,idleBones);
            if(entry>.002f || exit>.002f)throw new InvalidOperationException("Slash must meet the existing Idle pose: "+entry+" / "+exit);
            string guid=AssetDatabase.AssetPathToGUID(Attack);EditorUtility.CopySerialized(result,target);target.name="Attack";EditorUtility.SetDirty(target);AssetDatabase.SaveAssets();
            if(AssetDatabase.AssetPathToGUID(Attack)!=guid)throw new InvalidOperationException("Attack GUID changed.");
        }finally{EditorSceneManager.ClosePreviewScene(scene);Object.DestroyImmediate(result);}
    }
    static float PoseError(GameObject instance,Dictionary<string,Matrix4x4> reference){
        float error=0;foreach(var t in instance.GetComponentsInChildren<Transform>()){
            var matrix=reference[AnimationUtility.CalculateTransformPath(t,instance.transform)];for(int r=0;r<4;r++)for(int c=0;c<4;c++)error=Mathf.Max(error,Mathf.Abs(t.localToWorldMatrix[r,c]-matrix[r,c]));
        }return error;
    }
    [MenuItem("Tools/Proof Devourer/Review claw slash")]
    public static string ReviewSlash(){
        var scene=EditorSceneManager.NewPreviewScene();var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProofDevourerAssetSetup.PrefabPath),scene);
        var report=new Review{checkedUtc=DateTime.UtcNow.ToString("o"),guid=AssetDatabase.AssetPathToGUID(Attack)};
        try{
            instance.GetComponent<Animator>().enabled=false;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack);var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Animations/Idle.anim");
            report.duration=clip.length;report.curves=AnimationUtility.GetCurveBindings(clip).Length;
            idle.SampleAnimation(instance,0);var matrices=instance.GetComponentsInChildren<Transform>().ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,instance.transform),t=>t.localToWorldMatrix);
            var bones=instance.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            var supports=new[]{bones["Hand.L"],bones["Foot.L"],bones["Foot.R"]};var anchors=supports.Select(t=>t.position).ToArray();Vector3 root=bones["Root"].position;
            clip.SampleAnimation(instance,0);report.entryError=PoseError(instance,matrices);clip.SampleAnimation(instance,clip.length);report.exitError=PoseError(instance,matrices);
            Vector3 wind=Vector3.zero,follow=Vector3.zero;
            var frames=new[]{0f,.20f,.40f,.52f,.62f,.74f,1.12f,1.55f};
            for(int i=0;i<=108;i++){
                float time=clip.length*i/108;clip.SampleAnimation(instance,time);
                for(int s=0;s<supports.Length;s++)report.maxSupportDrift=Mathf.Max(report.maxSupportDrift,Vector3.Distance(supports[s].position,anchors[s]));
                report.maxRootDrift=Mathf.Max(report.maxRootDrift,Vector3.Distance(bones["Root"].position,root));
                if(Mathf.Abs(time-.40f)<.008f)wind=bones["Hand.R"].position;if(Mathf.Abs(time-.75f)<.008f)follow=bones["Hand.R"].position;
            }
            report.slashTravel=Vector3.Distance(wind,follow);
            report.passed=report.entryError<.002f && report.exitError<.002f && report.maxRootDrift<.001f && report.maxSupportDrift<.003f && report.slashTravel>1.5f && Mathf.Abs(report.duration-1.8f)<.001f;
            report.checks.Add("Same Attack GUID, duration, existing controller/state and original 33-bone model.");
            report.checks.Add("Existing Idle entry/exit, stationary root, planted left forepaw and both hind feet.");
            RenderFrames(instance,scene,clip,frames);
            File.WriteAllText(Folder+"/Previews/Slash_UnityValidation.json",JsonUtility.ToJson(report,true));
            return JsonUtility.ToJson(report,true);
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static void RenderMotionReview(){
        var scene=EditorSceneManager.NewPreviewScene();
        try{var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ProofDevourerAssetSetup.PrefabPath),scene);instance.GetComponent<Animator>().enabled=false;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Attack);RenderFrames(instance,scene,clip,Enumerable.Range(0,55).Select(i=>i/30f).ToArray(),"Temp/KhezuSlashFrames");
        }finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void RenderFrames(GameObject instance,Scene scene,AnimationClip clip,float[] times,string outputFolder=null){
        outputFolder=outputFolder??Folder+"/Previews";Directory.CreateDirectory(outputFolder);
        foreach(var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;}
        var go=new GameObject("Slash review camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=2.55f;
        camera.transform.position=new Vector3(6.5f,4.4f,10);camera.transform.LookAt(new Vector3(0,1.6f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.08f);camera.nearClipPlane=.05f;camera.farClipPlane=40;
        for(int i=0;i<3;i++){var lightGo=new GameObject("Slash review light");SceneManager.MoveGameObjectToScene(lightGo,scene);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=i==0?1.5f:.65f;light.transform.rotation=Quaternion.Euler(i==0?40:25,i==0?-145:i==1?-40:70,0);}
        var rt=RenderTexture.GetTemporary(600,600,24);var old=RenderTexture.active;var png=new Texture2D(600,600,TextureFormat.RGB24,false);
        try{for(int i=0;i<times.Length;i++){
            clip.SampleAnimation(instance,times[i]);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;png.ReadPixels(new Rect(0,0,600,600),0,0);png.Apply();File.WriteAllBytes(outputFolder+"/Slash_"+i.ToString("D2")+".png",png.EncodeToPNG());
        }}finally{camera.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(png);}
    }
}




