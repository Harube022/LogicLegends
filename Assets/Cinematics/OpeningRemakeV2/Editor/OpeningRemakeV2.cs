// Review-only Editor renderer. No runtime hooks, scene-start playback or save writes.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using Process=System.Diagnostics.Process;
using ProcessStartInfo=System.Diagnostics.ProcessStartInfo;

public static partial class OpeningRemakeV2
{
    public const string Folder="Assets/Cinematics/OpeningRemakeV2", Output="output/Cinematics/OpeningRemakeV2";
    public const string ScenePath=Folder+"/LogicLegends_Opening_Remake.unity";
    static Scene film,original;static Camera camera;static Renderer pillar;
    static Transform portal,portalVisual,outer,inner,runes,seal;
    static List<Actor> cast=new List<Actor>();static Actor wizard;
    static List<Crack> cracks=new List<Crack>();static List<Light> lights=new List<Light>();static List<float> intensities=new List<float>();
    static List<Material> portalMaterials=new List<Material>();static ParticleSystem[] particles;
    static Dictionary<int,float> lightLevels=new Dictionary<int,float>();static Dictionary<int,float> simulatedAt=new Dictionary<int,float>();
    static Light sun,crystalLight;static ColorAdjustments grade;static Material blendMaterial;static RenderTexture a,b,composite;static Texture2D pixels;
    static Timeline timeline;static Process encoder;static byte[] readback;static int frame,total,width=1920,height=1080;static string error="";static double began;
    static readonly Vector3 Crystal=new Vector3(-233.81f,3991.54f,-320.99f);
    static readonly Vector3 GardenWizard=new Vector3(-.47f,.02f,-47.3f);
    static readonly Vector3 SanctuaryWizard=new Vector3(-238.6f,3975.23f,-276.8f);
    static readonly Vector3 SanctuaryPortal=new Vector3(-233.8f,3975.23f,-284.6f);
    static readonly Vector3 GardenPortal=new Vector3(0,.02f,-49);
    public static bool Rendering=>encoder!=null;
    public static string Status=>JsonUtility.ToJson(new Progress{frame=frame,total=total,seconds=frame/30f,rendering=Rendering,error=error,elapsed=EditorApplication.timeSinceStartup-began});
    [Serializable]public class Progress{public int frame,total;public float seconds;public bool rendering;public string error;public double elapsed;}
    [Serializable]public class Timeline{public float duration,firstCrack,spreadCrack,portalCall;public Shot[] shots;}
    [Serializable]public class Shot{public int scene;public string label;public float start,end,fov0=48,fov1=48,dissolve,fadeIn,fadeOut;public Vector3 from,to,aimFrom,aimTo,control1,control2,tangentFrom,tangentTo,aimTangentFrom,aimTangentTo;public bool continuous,curved;}

    [MenuItem("Logic Legends/Cinematics/Build Movie 002 Story Remake")]
    public static void Build()
    {
        if(Application.isPlaying||Rendering)throw new Exception("Build only in Edit Mode, outside an active render.");
        original=SceneManager.GetActiveScene();
        var source=SceneManager.GetSceneByPath("Assets/Scenes/LogicGarden.unity");
        if(!source.isLoaded)source=EditorSceneManager.OpenScene("Assets/Scenes/LogicGarden.unity",OpenSceneMode.Additive);
        if(source.isDirty)throw new Exception("Fresh source scene must be saved before cloning.");
        var sanctuarySource=SceneManager.GetSceneByPath("Assets/Cinematics/OpeningReview/LogicLegends_Opening_Review.unity");
        if(!sanctuarySource.isLoaded)sanctuarySource=EditorSceneManager.OpenScene("Assets/Cinematics/OpeningReview/LogicLegends_Opening_Review.unity",OpenSceneMode.Additive);
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output+"/EditSource");Directory.CreateDirectory("Library/OpeningRemakeV2");
        
        SceneManager.SetActiveScene(source);var sky=RenderSettings.skybox;
        var roots=source.GetRootGameObjects().Concat(sanctuarySource.GetRootGameObjects().Where(r=>r.name=="Sanctuary"||r.name.StartsWith("CLOUDS_PS"))).ToArray();
        film=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);film.name="LogicLegends_Opening_Remake";
        EditorSceneManager.SetSceneCullingMask(film,EditorSceneManager.CalculateAvailableSceneCullingMask());
        var include=new[]{"Sanctuary","Logic Garden","Garden Dressing (Reference Layout)","LogicGarden_Assets","LogicGarden Study Library","background_0","background_0 (1)","CEILING","BOOKSHELVES","BOOKSHELVES (1)","BOOKSHELVES (2)","BOOKSHELVES (3)","CLOUDS_PS (70)","CLOUDS_PS (71)","CLOUDS_PS (72)"};
        foreach(var root in roots.Where(r=>include.Contains(r.name)))
        {
            var clone=UnityEngine.Object.Instantiate(root);clone.name=root.name;SceneManager.MoveGameObjectToScene(clone,film);
            foreach(var actor in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))actor.enabled=false;
            foreach(var text in clone.GetComponentsInChildren<TMP_Text>(true))if(text.name.Contains("FloatingName")||text.name.Contains("Attention")||text.name.Contains("Indicator"))text.gameObject.SetActive(false);
            Strip(clone,false);
        }
        BuildStorySets(film);RenderSettings.skybox=sky;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.46f,.57f,.67f);RenderSettings.ambientEquatorColor=new Color(.34f,.38f,.38f);RenderSettings.ambientGroundColor=new Color(.22f,.20f,.17f);
        var lightRoot=new GameObject("Review Lighting");sun=new GameObject("Cinematic Sun").AddComponent<Light>();sun.transform.SetParent(lightRoot.transform);sun.type=LightType.Directional;
        sun.transform.rotation=Quaternion.Euler(42,320,0);sun.intensity=1.05f;sun.color=new Color(1,.91f,.75f);sun.shadows=LightShadows.Soft;sun.shadowStrength=.7f;
        var cam=new GameObject("Opening Review Camera");camera=cam.AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.12f;camera.farClipPlane=700;camera.fieldOfView=48;camera.allowHDR=true;
        var data=cam.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var volume=new GameObject("Review Color and Bloom").AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.18f);bloom.threshold.Override(1.1f);bloom.scatter.Override(.5f);
        var color=profile.Add<ColorAdjustments>(true);color.contrast.Override(7);color.saturation.Override(1);var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.13f);vignette.smoothness.Override(.5f);
        Asset(profile,Folder+"/ReviewVolume.asset");volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder+"/ReviewVolume.asset");
        foreach(var component in volume.sharedProfile.components)if(component!=null&&!EditorUtility.IsPersistent(component))AssetDatabase.AddObjectToAsset(component,volume.sharedProfile);
        EditorUtility.SetDirty(volume.sharedProfile);
        var actorParent=new GameObject("Cinematic Cast — 12 Distinct Skins");
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Characters_NEW/LATEST_SKINS"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Gameplay.prefab")).OrderBy(p=>p))
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),film);instance.transform.SetParent(actorParent.transform,true);
            instance.name=Path.GetFileNameWithoutExtension(path).Replace("_Gameplay","");PrepareActor(instance,false);
        }
        if(actorParent.transform.childCount!=12)throw new Exception("Expected exactly 12 distinct prepared player skins.");
        var wiz=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters_NEW/LATEST_SKINS/Wizard/Prefabs/Wizard.prefab"),film);wiz.name="Cinematic Lefyahj";PrepareActor(wiz,true);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Exit/Archive_Victory_Portal.prefab");
        var gate=(GameObject)PrefabUtility.InstantiatePrefab(prefab,film);gate.name="Cinematic Portal — Multiplayer Visuals";gate.transform.localScale=Vector3.one*.4f;Strip(gate,true);
        var crystal=film.GetRootGameObjects().Single(r=>r.name=="Sanctuary").GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Cylinder.106");
        var collider=crystal.GetComponent<MeshCollider>();if(collider==null)collider=crystal.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=crystal.GetComponent<MeshFilter>().sharedMesh;
        var fractureRoot=new GameObject("Reversible Crystal Fractures");
        var dark=MakeMaterial("Fracture Dark",new Color(.015f,.025f,.08f));var glow=MakeMaterial("Fracture Glow",new Color(.5f,2.6f,3.5f));
        float[][][] paths={new[]{new[]{-.55f,7.1f},new[]{.2f,5.5f},new[]{-.4f,3.6f},new[]{.65f,1.9f},new[]{.15f,.2f},new[]{-.7f,-1.7f},new[]{.1f,-3.3f},new[]{-.3f,-5.5f}},new[]{new[]{-.4f,3.6f},new[]{-1.3f,3f},new[]{-1.6f,1.8f}},new[]{new[]{.65f,1.9f},new[]{1.6f,.9f},new[]{1.7f,-.6f}},new[]{new[]{-.7f,-1.7f},new[]{-1.5f,-2.4f},new[]{-1.3f,-4}},new[]{new[]{.15f,.2f},new[]{1.2f,-1},new[]{1.7f,-2.4f}}};
        for(int i=0;i<paths.Length;i++)
        {
            var positions=new List<Vector3>();foreach(var pair in paths[i]){RaycastHit hit;var ray=new Ray(Crystal+new Vector3(pair[0],pair[1],15),Vector3.back);if(collider.Raycast(ray,out hit,30))positions.Add(hit.point+hit.normal*.035f);}
            foreach(bool core in new[]{false,true}){var line=new GameObject("Fracture "+i+(core?" Core":" Shadow")).AddComponent<LineRenderer>();line.transform.SetParent(fractureRoot.transform);line.sharedMaterial=core?glow:dark;line.positionCount=positions.Count;line.SetPositions(positions.ToArray());line.startWidth=line.endWidth=core?.035f:.09f;line.numCapVertices=3;line.enabled=false;}
        }
        var fill=new GameObject("Pillar Magic Light").AddComponent<Light>();fill.transform.SetParent(lightRoot.transform);fill.type=LightType.Point;fill.transform.position=Crystal+Vector3.forward*4;fill.color=new Color(.1f,.72f,1);fill.intensity=3;fill.range=32;fill.shadows=LightShadows.None;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(film,ScenePath);Bind();Evaluate(0);EditorSceneManager.SaveScene(film);SceneManager.SetActiveScene(original);
        File.WriteAllText(Output+"/EditSource/scene-manifest.json",JsonUtility.ToJson(new Manifest{source=source.path,workingScene=ScenePath,sourceWasDirty=source.isDirty,cast=cast.Select(c=>c.root.name).ToArray(),crystal="Sanctuary/ALTAR/Cylinder.106",portal=AssetDatabase.GetAssetPath(prefab)},true));
    }
    [Serializable]class Manifest{public string source,workingScene,crystal,portal;public bool sourceWasDirty;public string[] cast;}
    static void Asset(UnityEngine.Object value,string path){var old=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);if(old==null)AssetDatabase.CreateAsset(value,path);else{EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);}}
    static Material MakeMaterial(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));m.name=name;m.SetColor("_BaseColor",color);Asset(m,Folder+"/"+name.Replace(" ","")+".mat");return AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name.Replace(" ","")+".mat");}
    static void Strip(GameObject root,bool keepSkin)
    {
        foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if(!(behaviour is TMP_Text)&&!(behaviour is TMP_SubMesh)&&!(behaviour is UniversalAdditionalLightData))UnityEngine.Object.DestroyImmediate(behaviour);
        foreach(var component in root.GetComponentsInChildren<AudioSource>(true))UnityEngine.Object.DestroyImmediate(component);
        foreach(var component in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(component);
        foreach(var component in root.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(component);
        foreach(var component in root.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(component);
        foreach(var animator in root.GetComponentsInChildren<Animator>(true))animator.enabled=false;
    }
    static void PrepareActor(GameObject root,bool wiz)
    {
        // Store original matching clips through controller references before stripping scripts.
        Strip(root,true);root.SetActive(false);
    }
    public static void Bind()
    {
        film=SceneManager.GetSceneByPath(ScenePath);if(!film.isLoaded)film=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var roots=film.GetRootGameObjects();camera=roots.Single(r=>r.name=="Opening Review Camera").GetComponent<Camera>();camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(film);
        // Camera.Render under URP can still include the other loaded scene.
        // Isolate only the review clones; the user's loaded scene stays intact.
        foreach(var root in roots)foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        camera.cullingMask=1<<31;camera.GetComponent<UniversalAdditionalCameraData>().volumeLayerMask=1<<31;
        foreach(var light in roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)))light.cullingMask=1<<31;
        roots.Single(r=>r.name=="Review Color and Bloom").GetComponent<Volume>().profile.TryGet(out grade);
        pillar=roots.Single(r=>r.name=="Sanctuary").GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Cylinder.106");
        sun=roots.Single(r=>r.name=="Review Lighting").GetComponentsInChildren<Light>().Single(l=>l.type==LightType.Directional);
        crystalLight=roots.Single(r=>r.name=="Review Lighting").GetComponentsInChildren<Light>().Single(l=>l.name=="Pillar Magic Light");
        lights=roots.SelectMany(r=>r.GetComponentsInChildren<Light>(true)).Where(l=>l!=sun&&l!=crystalLight).ToList();
        foreach(var light in lights)if(!lightLevels.ContainsKey(light.GetInstanceID()))lightLevels.Add(light.GetInstanceID(),light.intensity);
        intensities=lights.Select(l=>lightLevels[l.GetInstanceID()]).ToList();
        cast=roots.Single(r=>r.name=="Cinematic Cast — 12 Distinct Skins").transform.Cast<Transform>().Select(t=>new Actor(t.gameObject,false)).ToList();
        wizard=new Actor(roots.Single(r=>r.name=="Cinematic Lefyahj"),true);
        portal=roots.Single(r=>r.name.StartsWith("Cinematic Portal")).transform;
        portalVisual=portal.Find("Portal_Formation");
        if(portalVisual==null)portalVisual=portal.Cast<Transform>().First(t=>t.name.ToLower().Contains("visual"));
        outer=portal.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.ToLower().Contains("outer"));inner=portal.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name.ToLower().Contains("inner"));
        runes=portal.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Ten_Topic_Runes");seal=portal.Find("Ground_Summoning_Seal");
        foreach(var text in portal.GetComponentsInChildren<TMP_Text>(true))if(text.font!=null){text.fontSharedMaterial=text.font.material;text.ForceMeshUpdate(true,true);}
        portalMaterials.Clear();int portalMaterialIndex=0;
        foreach(var r in portal.GetComponentsInChildren<Renderer>(true))
        {
            if(r.GetComponent<TMP_Text>()!=null||r.GetComponent<TMP_SubMesh>()!=null)continue;
            // Persist review-owned materials before saving the review scene.
            // Transient clones otherwise become missing references after reload.
            string materialPath=Folder+"/PortalMeshReviewMaterial"+(portalMaterialIndex++)+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(m==null)
            {
                var originalRenderer=PrefabUtility.GetCorrespondingObjectFromOriginalSource(r) as Renderer;
                var source=r.sharedMaterial!=null?r.sharedMaterial:originalRenderer!=null?originalRenderer.sharedMaterial:null;
                if(source==null)throw new Exception("Missing source portal material: "+AnimationUtility.CalculateTransformPath(r.transform,portal));
                m=new Material(source);if(m.HasProperty("_Membrane"))m.shader=Shader.Find("LogicLegends/Cinematics/OpeningPortal");AssetDatabase.CreateAsset(m,materialPath);
            }
            r.sharedMaterial=m;portalMaterials.Add(m);
        }
        var lines=roots.Single(r=>r.name=="Reversible Crystal Fractures").GetComponentsInChildren<LineRenderer>(true);
        string canonical=Output+"/EditSource/fracture-control-points.json";
        if(!File.Exists(canonical))File.WriteAllText(canonical,JsonUtility.ToJson(new CrackPaths{paths=lines.Select(l=>{var points=new Vector3[l.positionCount];l.GetPositions(points);return new CrackPath{name=l.name,points=points};}).ToArray()},true));
        var saved=JsonUtility.FromJson<CrackPaths>(File.ReadAllText(canonical));
        cracks=lines.Select(l=>new Crack(l,saved.paths.First(p=>p.name==l.name).points)).ToList();
        particles=roots.SelectMany(r=>r.GetComponentsInChildren<ParticleSystem>(true)).ToArray();simulatedAt.Clear();foreach(var p in particles){p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);p.useAutoRandomSeed=false;p.randomSeed=42;}
        BindStorySets();if(File.Exists(Output+"/EditSource/timeline.json"))timeline=JsonUtility.FromJson<Timeline>(File.ReadAllText(Output+"/EditSource/timeline.json"));
    }
    static Vector3 Hermite(Vector3 a,Vector3 b,Vector3 va,Vector3 vb,float u){float v=u*u,w=v*u;return (2*w-3*v+1)*a+(w-2*v+u)*va+(-2*w+3*v)*b+(w-v)*vb;}
    static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float u){float v=1-u;return v*v*v*a+3*v*v*u*b+3*v*u*u*c+u*u*u*d;}
    static float Smooth(float x){return x*x*(3-2*x);}
    static Vector3 Lerp(Vector3 x,Vector3 y,float u){return Vector3.LerpUnclamped(x,y,u);}
    public static void Evaluate(float time)
    {
        if(timeline==null||timeline.shots==null)return;
        var shot=timeline.shots.LastOrDefault(s=>s.start<=time)??timeline.shots[0];EvaluateShot(shot,time);
    }
    static void EvaluateShot(Shot shot,float time){EvaluateStory(shot,time);}
    public static double Benchmark(int count=12)
    {
        Bind();CreateBuffers(1920,1080);double beganAt=EditorApplication.timeSinceStartup;
        for(int i=0;i<count;i++){RenderFrame(100+i/30f);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0,false);}
        double elapsed=EditorApplication.timeSinceStartup-beganAt;RenderTexture.active=null;camera.targetTexture=null;ReleaseBuffers();return elapsed;
    }
    public static string Preview(float seconds,string name="preview",int w=1280,int h=720)
    {
        Bind();CreateBuffers(w,h);RenderFrame(seconds);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,w,h),0,0,false);File.WriteAllBytes(Output+"/EditSource/"+name+".png",pixels.EncodeToPNG());RenderTexture.active=null;if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);return Output+"/EditSource/"+name+".png";
    }
    static void CreateBuffers(int w,int h)
    {
        ReleaseBuffers();width=w;height=h;
        a=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);b=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);composite=new RenderTexture(w,h,0,RenderTextureFormat.ARGB32);
        a.Create();b.Create();composite.Create();pixels=new Texture2D(w,h,TextureFormat.RGB24,false);readback=new byte[w*h*3];
        blendMaterial=new Material(Shader.Find("Hidden/LogicLegends/OpeningReviewBlend"));
    }
    static void RenderFrame(float time)
    {
        var shot=timeline.shots.LastOrDefault(s=>s.start<=time)??timeline.shots[0];
        EvaluateShot(shot,time);camera.targetTexture=b;camera.Render();
        float mix=1;if(shot.dissolve>0&&time-shot.start<shot.dissolve)
        {
            int index=Array.IndexOf(timeline.shots,shot);if(index>0){var previous=timeline.shots[index-1];EvaluateShot(previous,previous.end-shot.dissolve*.5f+(time-shot.start)*.5f);camera.targetTexture=a;camera.Render();mix=Smooth((time-shot.start)/shot.dissolve);}
        }
        if(mix==1)Graphics.Blit(b,a);
        float fade=1;if(shot.fadeIn>0)fade*=Smooth(Mathf.Clamp01((time-shot.start)/shot.fadeIn));if(shot.fadeOut>0)fade*=Smooth(Mathf.Clamp01((shot.end-time)/shot.fadeOut));
        blendMaterial.SetTexture("_NextTex",b);blendMaterial.SetFloat("_Blend",mix);blendMaterial.SetFloat("_Fade",fade);Graphics.Blit(a,composite,blendMaterial);camera.targetTexture=null;
    }
    public static string StartRender(int w=1920,int h=1080,string filename="UnityFootage.mp4",int endFrame=0,int startFrame=0)
    {
        if(Application.isPlaying||Rendering)throw new Exception("Use Edit Mode with no active render.");
        original=SceneManager.GetActiveScene();Bind();if(timeline==null)throw new Exception("Prepare narration timeline first.");CreateBuffers(w,h);
        string executable=JsonUtility.FromJson<Tools>(File.ReadAllText(Output+"/EditSource/tools.json")).ffmpeg;
        string path=Path.GetFullPath(Output+"/EditSource/"+Path.GetFileName(filename));
        var info=new ProcessStartInfo(executable,"-hide_banner -loglevel error -y -filter_threads 1 -threads 2 -thread_queue_size 2 -f rawvideo -pixel_format rgb24 -video_size "+w+"x"+h+" -framerate 30 -i pipe:0 -vf vflip -an -c:v libx264 -threads 2 -preset fast -crf 12 -pix_fmt yuv420p -movflags +faststart \""+path+"\"");
        info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;info.RedirectStandardError=true;
        encoder=new Process{StartInfo=info};error="";encoder.ErrorDataReceived+=(sender,args)=>{if(!string.IsNullOrEmpty(args.Data))error+=args.Data+"\n";};encoder.Start();encoder.BeginErrorReadLine();
        total=Mathf.CeilToInt(timeline.duration*30);if(endFrame>0)total=Mathf.Min(total,endFrame);frame=Mathf.Clamp(startFrame,0,total-1);System.GC.Collect();began=EditorApplication.timeSinceStartup;EditorApplication.update-=RenderTick;EditorApplication.update+=RenderTick;return Status;
    }
    [Serializable]class Tools{public string ffmpeg;}
    static void RenderTick()
    {
        try
        {
            for(int i=0;i<4&&frame<total;i++)
            {
                RenderFrame(frame/30f);RenderTexture.active=composite;pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);pixels.GetRawTextureData<byte>().CopyTo(readback);encoder.StandardInput.BaseStream.Write(readback,0,readback.Length);frame++;
            }
            RenderTexture.active=null;if(frame%60<4)File.WriteAllText(Output+"/EditSource/unity-render-status.json",Status);if(frame>=total)Finish();
        }
        catch(Exception e){error+=e.ToString();Finish();}
    }
    public static void Finish()
    {
        EditorApplication.update-=RenderTick;if(encoder!=null){try{encoder.StandardInput.Close();encoder.WaitForExit(15000);}catch(Exception e){error+=e.Message;}encoder.Dispose();encoder=null;}
        RenderTexture.active=null;if(camera!=null)camera.targetTexture=null;ReleaseBuffers();if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
        File.WriteAllText(Output+"/EditSource/unity-render-status.json",Status);
    }
    static void ReleaseBuffers(){foreach(var r in new[]{a,b,composite})if(r!=null){r.Release();UnityEngine.Object.DestroyImmediate(r);}a=b=composite=null;if(pixels!=null)UnityEngine.Object.DestroyImmediate(pixels);if(blendMaterial!=null)UnityEngine.Object.DestroyImmediate(blendMaterial);}
    [Serializable]class CrackPaths{public CrackPath[] paths;}
    [Serializable]class CrackPath{public string name;public Vector3[] points;}
    sealed class Crack
    {
        LineRenderer line;Vector3[] points;int order;
        public Crack(LineRenderer l,Vector3[] full){line=l;points=full;int.TryParse(l.name.Split(' ')[1],out order);}
        public void Set(float total)
        {
            float progress=Mathf.Clamp01((total-order*.13f)*2.5f);line.enabled=progress>0&&points.Length>1;if(!line.enabled)return;
            float count=progress*(points.Length-1);int whole=Mathf.FloorToInt(count);var partial=points.Take(whole+1).ToList();if(whole<points.Length-1)partial.Add(Vector3.Lerp(points[whole],points[whole+1],count-whole));line.positionCount=partial.Count;line.SetPositions(partial.ToArray());
        }
    }
    sealed class Actor
    {
        public GameObject root;public AnimationClip idle,walk,gesture;Transform model,hips;float homeZ,natural=2;Vector3[] poseP,poseS,restP,restS;Quaternion[] poseR,restR;Transform[] bones;AnimationCurve rootCurve;bool isWizard;
        public Actor(GameObject go,bool wiz)
        {
            root=go;isWizard=wiz;var animator=go.GetComponentInChildren<Animator>(true);model=animator.transform;var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            idle=wiz?AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters_NEW/LATEST_SKINS/Wizard/Animations/Wizard_Idle1.anim"):clips.First(c=>c.name.Contains("Idle")&&!c.name.Contains("Carry"));walk=clips.First(c=>wiz?c.name=="Wizard_Walking":c.name.Contains("_Walk"));
            gesture=wiz?clips.First(c=>c.name=="Wizard_HandsForwardGesture"):idle;bones=model.GetComponentsInChildren<Transform>(true);poseP=new Vector3[bones.Length];poseR=new Quaternion[bones.Length];poseS=new Vector3[bones.Length];
            restP=new Vector3[bones.Length];restS=new Vector3[bones.Length];restR=new Quaternion[bones.Length];
            for(int i=1;i<bones.Length;i++){var original=PrefabUtility.GetCorrespondingObjectFromOriginalSource(bones[i])??bones[i];restP[i]=original.localPosition;restR[i]=original.localRotation;restS[i]=original.localScale;}
            hips=bones.First(t=>t.name.EndsWith(":Hips"));idle.SampleAnimation(model.gameObject,0);homeZ=hips.localPosition.z;
            var binding=AnimationUtility.GetCurveBindings(walk).FirstOrDefault(c=>c.path.EndsWith(":Hips")&&c.propertyName=="m_LocalPosition.z");rootCurve=AnimationUtility.GetEditorCurve(walk,binding);
            if(rootCurve!=null)natural=Mathf.Abs(rootCurve.Evaluate(walk.length)-rootCurve.Evaluate(0))*hips.parent.lossyScale.z/walk.length;
            if(natural<.3f)natural=2.2f;
        }
        void Sample(AnimationClip clip,float time)
        {
            // Match Animator Write Defaults: clips need not key every property.
            // Always begin with the original rig's local transforms, never a
            // prior sample's positions/scales. This prevents accumulated poses.
            for(int i=1;i<bones.Length;i++){bones[i].localPosition=restP[i];bones[i].localRotation=restR[i];bones[i].localScale=restS[i];}
            float sample=isWizard&&clip==idle?Mathf.Min(Mathf.Repeat(Mathf.Max(0,time),5),clip.length-.001f):Mathf.Repeat(Mathf.Max(0,time),clip.length);
            clip.SampleAnimation(model.gameObject,sample);
            if(clip==walk&&rootCurve!=null){var p=hips.localPosition;p.z-=rootCurve.Evaluate(Mathf.Repeat(Mathf.Max(0,time),clip.length))-homeZ;hips.localPosition=p;}
        }
        public void Pose(AnimationClip clip,float time){Sample(clip,time);}
        public void Pose(AnimationClip clip,float time,AnimationClip other,float otherTime,float weight)
        {
            Sample(other,otherTime);for(int i=0;i<bones.Length;i++){poseP[i]=bones[i].localPosition;poseR[i]=bones[i].localRotation;poseS[i]=bones[i].localScale;}
            Sample(clip,time);for(int i=0;i<bones.Length;i++){bones[i].localPosition=Vector3.Lerp(poseP[i],bones[i].localPosition,weight);bones[i].localRotation=Quaternion.Slerp(poseR[i],bones[i].localRotation,weight);bones[i].localScale=Vector3.Lerp(poseS[i],bones[i].localScale,weight);}
        }
        public void Walk(Vector3 from,Vector3 to,float u,float duration,float time)
        {
            float e=Mathf.Clamp01(u);root.transform.position=Vector3.Lerp(from,to,e);root.transform.rotation=Quaternion.LookRotation(to-from);
            float length=Vector3.Distance(from,to);float phase=length*e/natural*1.18f;float blend=Mathf.Min(Mathf.Clamp01(u*18),Mathf.Clamp01((1-u)*18));Pose(walk,phase,idle,time,blend);
        }
        public void WalkFacing(Vector3 from,Vector3 to,float u,float duration,float time,float startYaw,float endYaw)
        {
            // The Wizard's authored stride is much longer than the students'.
            // Drive his clip from actual distance, without their 18% phase bias.
            float t=Mathf.Clamp01(u),travel=t*t*(3-2*t);
            root.transform.position=Vector3.Lerp(from,to,travel);
            var direction=Quaternion.LookRotation(to-from);
            root.transform.rotation=Quaternion.Slerp(Quaternion.Euler(0,startYaw,0),direction,Smooth(Mathf.Clamp01(t/.18f)));
            root.transform.rotation=Quaternion.Slerp(root.transform.rotation,Quaternion.Euler(0,endYaw,0),Smooth(Mathf.Clamp01((t-.72f)/.28f)));
            float blend=Mathf.Min(Smooth(Mathf.Clamp01(time/.18f)),Smooth(Mathf.Clamp01((duration-time)/.22f)));
            Pose(walk,Vector3.Distance(from,to)*travel/natural,idle,time,blend);
            var feet=bones.Where(b=>b.name.EndsWith(":LeftFoot")||b.name.EndsWith(":RightFoot")).ToArray();
            if(feet.Length==2){float ground=Mathf.Lerp(from.y,to.y,travel);float ankle=Mathf.Min(feet[0].position.y,feet[1].position.y);root.transform.position+=Vector3.up*Mathf.Clamp(ground+.185f-ankle,-.18f,.18f);}
        }
    }
}
