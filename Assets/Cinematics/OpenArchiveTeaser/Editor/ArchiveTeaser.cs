// Offline, deterministic cinematic authoring. Never runs gameplay or Photon.
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
using LogicLegends.OpenArchive.Topics;
using Process=System.Diagnostics.Process;
using ProcessStartInfo=System.Diagnostics.ProcessStartInfo;

public static partial class ArchiveTeaser
{
    public const string Folder="Assets/Cinematics/OpenArchiveTeaser",Output="output/Cinematics/OpenArchiveTeaser";
    public const string ScenePath=Folder+"/OpenArchive_Teaser.unity";
    static Material skyMaterial; static Scene film,original; static Camera camera; static Light sun,fill; static ColorAdjustments grade;
    static Timeline timeline; static RenderTexture a,b,composite; static Texture2D pixels; static Material blendMaterial;
    static Process encoder;static byte[] readback;static int frame,total,width,height;static string error="";static double began;
    static readonly List<Actor> cast=new List<Actor>();static Actor primal,proof;
    static readonly List<Book> books=new List<Book>();static readonly List<Crystal> crystals=new List<Crystal>();
    static Transform gate,formation,inner,outer,runes;static List<Material> gateMaterials=new List<Material>();
    static readonly List<Transform> circles=new List<Transform>();static readonly List<Light> practicals=new List<Light>();
    static Transform[] guardianSeals=new Transform[2]; static ParticleSystem[] particles;static float particleTime=-1;
    public static bool Rendering=>encoder!=null;
    public static string Status=>JsonUtility.ToJson(new Progress{frame=frame,total=total,rendering=Rendering,error=error,elapsed=EditorApplication.timeSinceStartup-began});
    [Serializable]public class Progress{public int frame,total;public bool rendering;public string error;public double elapsed;}
    [Serializable]public class Timeline{public float duration;public Shot[] shots;}
    [Serializable]public class Shot{public int scene;public string label;public float start,end,fov0=48,fov1=48,dissolve,fadeIn,fadeOut;public Vector3 from,to,aimFrom,aimTo;}

    public static void Build()
    {
        if(Application.isPlaying||Rendering)throw new Exception("Build only in Edit Mode, outside rendering.");
        original=SceneManager.GetActiveScene();var source=SceneManager.GetSceneByPath("Assets/Scenes/MultiplayerGameplay.unity");
        if(!source.isLoaded)source=EditorSceneManager.OpenScene("Assets/Scenes/MultiplayerGameplay.unity",OpenSceneMode.Additive);
        if(source.isDirty)throw new Exception("Preserve unsaved production scene changes before cloning.");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Output+"/EditSource");
        var existing=SceneManager.GetSceneByPath(ScenePath);if(existing.isLoaded)EditorSceneManager.CloseScene(existing,true);
        SceneManager.SetActiveScene(source);var sky=RenderSettings.skybox;
        film=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);film.name="OpenArchive_Teaser";
        EditorSceneManager.SetSceneCullingMask(film,EditorSceneManager.CalculateAvailableSceneCullingMask());
        books.Clear();crystals.Clear();cast.Clear();circles.Clear();practicals.Clear();gateMaterials.Clear();
        foreach(var src in source.GetRootGameObjects().Where(r=>new[]{"OpenArchive","Archive_Environment_Polish","Archive_Distant_Archipelago"}.Contains(r.name)))
        {
            var clone=UnityEngine.Object.Instantiate(src);clone.name=src.name;SceneManager.MoveGameObjectToScene(clone,film);
            foreach(var book in clone.GetComponentsInChildren<ArchiveAnimatedBook>(true))books.Add(new Book(book));
            foreach(var crystal in clone.GetComponentsInChildren<ArchiveCrystalPillar>(true))crystals.Add(new Crystal(crystal));
            // Human-readable story captions are composited once; no gameplay HUD or floating diagnostics.
            foreach(var c in clone.GetComponentsInChildren<Canvas>(true))c.gameObject.SetActive(false);
            foreach(var t in clone.GetComponentsInChildren<TMP_Text>(true))t.gameObject.SetActive(false);
            foreach(var t in clone.GetComponentsInChildren<Transform>(true))
                if(t.name.StartsWith("Topic_Header")||t.name.Contains("Board")||t.name.Contains("Instruction")||t.name.Contains("Direction_Sign")||t.name.Contains("Topic_Sign")||t.name=="Active_Test_Monster"||t.name=="Night_Second_Monster")t.gameObject.SetActive(false);
            Strip(clone);
        }
        skyMaterial=new Material(sky);RenderSettings.skybox=skyMaterial;RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Trilight;
        sun=new GameObject("Teaser Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.shadows=LightShadows.Soft;sun.shadowStrength=.8f;
        fill=new GameObject("Teaser Camera Fill").AddComponent<Light>();fill.type=LightType.Point;fill.range=65;fill.shadows=LightShadows.None;
        camera=new GameObject("Archive Teaser Camera").AddComponent<Camera>();camera.enabled=false;camera.nearClipPlane=.15f;camera.farClipPlane=1100;camera.allowHDR=true;
        var data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var volume=new GameObject("Teaser Grade").AddComponent<Volume>();volume.isGlobal=true;volume.priority=200;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.30f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.55f);
        grade=profile.Add<ColorAdjustments>(true);grade.contrast.Override(12);grade.saturation.Override(-4);
        var vignette=profile.Add<Vignette>(true);vignette.intensity.Override(.20f);vignette.smoothness.Override(.6f);
        string vp=Folder+"/TeaserVolume.asset";var old=AssetDatabase.LoadAssetAtPath<VolumeProfile>(vp);
        if(old==null){AssetDatabase.CreateAsset(profile,vp);old=profile;}else{foreach(var c in old.components)if(c!=null)UnityEngine.Object.DestroyImmediate(c,true);old.components.Clear();foreach(var c in profile.components)old.components.Add(c);}
        foreach(var c in old.components)if(!EditorUtility.IsPersistent(c))AssetDatabase.AddObjectToAsset(c,old);volume.sharedProfile=old;old.TryGet(out grade);EditorUtility.SetDirty(old);
        string basePath="Assets/Characters_NEW/LATEST_SKINS/";
        string[] paths={"Male_Skins/Uniform/Prefabs/Male_Uniform_Gameplay.prefab","Female_Skins/Uniform/Prefabs/Female_Uniform_Gameplay.prefab","Male_Skins/Common/Prefabs/Male_Common_Gameplay.prefab","Female_Skins/Common/Prefabs/Female_Common_Gameplay.prefab"};
        for(int i=0;i<4;i++)cast.Add(MakeActor(basePath+paths[i],"Chosen Student "+(i+1),false));
        primal=MakeActor("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/PrimalAxiom/Prefabs/PrimalAxiom.prefab","The Primal Axiom",true);
        proof=MakeActor("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/MONSTERS/ProofDevourer/Prefabs/ProofDevourer.prefab","The Proof Devourer",true);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Exit/Archive_Victory_Portal.prefab");
        var portal=(GameObject)PrefabUtility.InstantiatePrefab(prefab,film);portal.name="Teaser Return Portal";Strip(portal);gate=portal.transform;gate.position=new Vector3(0,.15f,222);gate.rotation=Quaternion.identity;
        formation=gate.Find("Portal_Formation");formation.gameObject.SetActive(true);
        inner=gate.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Inner_Clockwise_Arc");outer=gate.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Outer_Counterclockwise_Arc");runes=gate.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Ten_Topic_Runes");
        int mi=0;foreach(var r in gate.GetComponentsInChildren<Renderer>(true))
        {
            if(r is TMP_SubMesh||r.GetComponent<TMP_Text>()!=null)continue;
            var m=new Material(r.sharedMaterial);if(m.HasProperty("_Membrane"))m.shader=Shader.Find("LogicLegends/Cinematics/OpeningPortal");
            string path=Folder+"/Portal_"+(mi++)+".mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(path);if(saved==null){AssetDatabase.CreateAsset(m,path);saved=m;}else{EditorUtility.CopySerialized(m,saved);UnityEngine.Object.DestroyImmediate(m);}r.sharedMaterial=saved;gateMaterials.Add(saved);
        }
        var energy=Mat("SummonEnergy",new Color(.15f,2.8f,2.4f));
        for(int i=0;i<4;i++)
        {
            var parent=new GameObject("Student Summoning Seal "+i).transform;circles.Add(parent);
            Ring(parent,3.0f,.055f,energy,"Outer");Ring(parent,2.75f,.035f,energy,"Inner");Ring(parent,1.6f,.025f,energy,"Core");
            for(int n=0;n<12;n++){float an=n*Mathf.PI/6;var line=new GameObject("Radial rune").AddComponent<LineRenderer>();line.transform.SetParent(parent);line.useWorldSpace=false;line.sharedMaterial=energy;line.positionCount=3;line.startWidth=line.endWidth=.055f;line.SetPositions(new[]{new Vector3(Mathf.Cos(an)*2,.02f,Mathf.Sin(an)*2),new Vector3(Mathf.Cos(an)*2.4f,.02f,Mathf.Sin(an)*2.4f),new Vector3(Mathf.Cos(an+.13f)*2.4f,.02f,Mathf.Sin(an+.13f)*2.4f)});}
        }
        for(int i=0;i<2;i++){var seal=new GameObject(i==0?"Primal Awakening Seal":"Proof Awakening Seal").transform;guardianSeals[i]=seal;var mat=Mat(i==0?"PrimalEnergy":"ProofEnergy",i==0?new Color(3.8f,1.1f,.1f):new Color(4,.1f,.2f));Ring(seal,9,.07f,mat,"Runic Boundary");Ring(seal,8.5f,.03f,mat,"Inner Orbit");for(int n=0;n<16;n++){var fragment=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.Object.DestroyImmediate(fragment.GetComponent<Collider>());fragment.name="Rising Fragment";fragment.transform.SetParent(seal);fragment.GetComponent<Renderer>().sharedMaterial=mat;fragment.transform.localScale=new Vector3(.09f,.4f,.09f);}}
        foreach(var root in film.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        camera.cullingMask=1<<31;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(film);data.volumeLayerMask=1<<31;
        foreach(var l in film.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)))if(l.type==LightType.Directional&&l!=sun)l.enabled=false;
        practicals.AddRange(film.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)).Where(l=>l!=sun&&l!=fill&&l.type!=LightType.Directional));foreach(var light in practicals){light.cullingMask=1<<31;light.shadows=LightShadows.None;}
        sun.cullingMask=fill.cullingMask=1<<31;
        particles=film.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ParticleSystem>(true)).ToArray();foreach(var p in particles){p.useAutoRandomSeed=false;p.randomSeed=42;p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}particleTime=-1;
        timeline=JsonUtility.FromJson<Timeline>(File.ReadAllText(Output+"/EditSource/timeline.json"));
        AssetDatabase.SaveAssets();Evaluate(0);EditorSceneManager.SaveScene(film,ScenePath);SceneManager.SetActiveScene(original);
    }
    static void Strip(GameObject root)
    {
        // Remove dependent scripts before their required components. Destroying
        // the state owner first logs an error and silently leaves gameplay code.
        Func<MonoBehaviour,bool> keep=m=>m is TMP_Text||m is TMP_SubMesh||m is UniversalAdditionalLightData;
        var pending=root.GetComponentsInChildren<MonoBehaviour>(true).Where(m=>m!=null&&!keep(m)).ToList();
        foreach(var behaviour in pending)behaviour.enabled=false;
        while(pending.Count>0)
        {
            var leaves=pending.Where(candidate=>!candidate.GetComponents<MonoBehaviour>().Any(other=>other!=null&&other!=candidate&&other.GetType().GetCustomAttributes(typeof(RequireComponent),true).Cast<RequireComponent>().Any(requirement=>new[]{requirement.m_Type0,requirement.m_Type1,requirement.m_Type2}.Any(type=>type!=null&&type.IsAssignableFrom(candidate.GetType()))))).ToArray();
            if(leaves.Length==0)throw new Exception("Cyclic gameplay dependency in isolated cinematic clone: "+root.name);
            foreach(var leaf in leaves){pending.Remove(leaf);UnityEngine.Object.DestroyImmediate(leaf);}
        }
        foreach(var c in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Camera>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<AudioSource>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(c);
        foreach(var c in root.GetComponentsInChildren<Animator>(true))c.enabled=false;
    }
    static Actor MakeActor(string path,string name,bool monster)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new Exception("Missing existing character: "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,film);instance.name=name;Strip(instance);instance.SetActive(true);
        if(monster)instance.transform.localScale=Vector3.one*2.5f;
        foreach(var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
        return new Actor(instance,monster);
    }
    static Material Mat(string name,Color tint)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",tint);return m;
    }
    static void Ring(Transform parent,float radius,float width,Material mat,string name)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>();line.transform.SetParent(parent);line.useWorldSpace=false;line.sharedMaterial=mat;line.loop=true;line.positionCount=96;line.startWidth=line.endWidth=width;
        line.SetPositions(Enumerable.Range(0,96).Select(i=>new Vector3(Mathf.Cos(i*Mathf.PI/48)*radius,0,Mathf.Sin(i*Mathf.PI/48)*radius)).ToArray());
    }
    static float Smooth(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
    public static void Bind(){if(camera==null||!film.isLoaded)Build();}
    public static void Evaluate(float time){if(timeline!=null)EvaluateShot(timeline.shots.LastOrDefault(s=>s.start<=time)??timeline.shots[0],time);}
    static void EvaluateShot(Shot s,float t)
    {
        SceneManager.SetActiveScene(film);float u=Smooth((t-s.start)/(s.end-s.start)),local=t-s.start;
        camera.transform.position=Vector3.Lerp(s.from,s.to,u);camera.transform.LookAt(Vector3.Lerp(s.aimFrom,s.aimTo,u));camera.fieldOfView=Mathf.Lerp(s.fov0,s.fov1,u);
        float night=s.scene>=7?1:s.scene==6?Smooth(local/4):s.scene==11?1:0;
        RenderSettings.skybox=skyMaterial;if(skyMaterial.HasProperty("_Exposure"))skyMaterial.SetFloat("_Exposure",Mathf.Lerp(.8f,.22f,night));if(skyMaterial.HasProperty("_Tint"))skyMaterial.SetColor("_Tint",Color.Lerp(Color.gray,new Color(.4f,.5f,.8f),night));
        sun.transform.rotation=Quaternion.Euler(Mathf.Lerp(38,12,night),Mathf.Lerp(315,195,night),0);sun.color=Color.Lerp(new Color(1,.86f,.65f),new Color(.62f,.75f,1),night);sun.intensity=Mathf.Lerp(1.15f,.58f,night);
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.48f,.61f,.72f),new Color(.18f,.25f,.38f),night);RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.33f,.39f,.36f),new Color(.12f,.17f,.25f),night);RenderSettings.ambientGroundColor=Color.Lerp(new Color(.23f,.22f,.16f),new Color(.075f,.10f,.15f),night);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=Color.Lerp(new Color(.46f,.67f,.76f),new Color(.055f,.095f,.15f),night);RenderSettings.fogDensity=Mathf.Lerp(.00085f,.0023f,night);
        grade.postExposure.Override(Mathf.Lerp(.05f,.25f,night));fill.transform.position=camera.transform.position+Vector3.up*3;fill.color=Color.Lerp(new Color(1,.83f,.66f),new Color(.35f,.67f,.9f),night);fill.intensity=s.scene==6||s.scene==7?120:night*80;
        foreach(var l in practicals)l.intensity=1.5f+night*4;
        foreach(var c in cast)c.root.SetActive(false);primal.root.SetActive(false);proof.root.SetActive(false);foreach(var c in circles)c.gameObject.SetActive(false);foreach(var c in guardianSeals)c.gameObject.SetActive(false);
        foreach(var book in books)book.Set(t,s.scene==3?Smooth(local/.8f):s.scene>3?1:0);
        float restored=s.scene<4?0:s.scene==4?Smooth(local/3.7f):1;foreach(var crystal in crystals)crystal.Set(t,restored);
        gate.gameObject.SetActive(s.scene>=9);float form=s.scene==9?Mathf.Max(.001f,Smooth(local/2.5f)):1;formation.localPosition=Vector3.up*13*form;formation.localScale=new Vector3(11*form,13*form,1);
        inner.localRotation=Quaternion.Euler(0,0,-t*16);outer.localRotation=Quaternion.Euler(0,0,t*11);runes.localRotation=Quaternion.Euler(0,0,t*5);
        foreach(var m in gateMaterials){if(m.HasProperty("_ReviewTime"))m.SetFloat("_ReviewTime",t);if(m.HasProperty("_Formation"))m.SetFloat("_Formation",1);}
        if(s.scene==1||s.scene==2)
        {
            for(int i=0;i<4;i++)
            {
                var pos=new Vector3(i%2==0?-4:4,.4f,-208+(i/2)*7);var c=cast[i];c.root.SetActive(s.scene==2||local>i*.23f+.45f);
                if(s.scene==1){c.Place(pos,0,"Idle",t+i);circles[i].gameObject.SetActive(true);circles[i].position=pos-Vector3.up*.3f;circles[i].localRotation=Quaternion.Euler(0,t*12+i*30,0);circles[i].localScale=Vector3.one*Mathf.Max(.01f,Smooth((local-i*.23f)/.8f));}
                else c.Travel(pos,pos+Vector3.forward*18,local/4,t+i,false);
            }
        }
        if(s.scene==3){cast[0].Place(new Vector3(-4.7f,.05f,165.2f),120,"Idle",t);cast[1].Place(new Vector3(4.7f,.05f,165.2f),240,"Idle",t+1);}
        if(s.scene==4||s.scene==9)
        {
            for(int i=0;i<4;i++){float an=(i*80+40)*Mathf.Deg2Rad;var pos=new Vector3(25+Mathf.Cos(an)*7,.05f,185+Mathf.Sin(an)*7);cast[i].Place(pos,Quaternion.LookRotation(new Vector3(25,0,185)-pos).eulerAngles.y,"Idle",t+i);}
        }
        if(s.scene==5)primal.Place(new Vector3(0,.7f,0),0,"Sleep",t);
        if(s.scene==6)primal.Place(new Vector3(0,.7f,0),0,"Full_Awaken",Mathf.Min(local,2.97f));
        if(s.scene==7)proof.Place(new Vector3(0,.7f,0),0,"Full_Awaken",Mathf.Min(local*.85f,2.97f));
        if(s.scene==6||s.scene==7){var seal=guardianSeals[s.scene-6];seal.gameObject.SetActive(true);seal.position=new Vector3(0,.77f,0);seal.rotation=Quaternion.Euler(0,t*10,0);int n=0;foreach(Transform child in seal)if(child.name=="Rising Fragment"){float an=n*Mathf.PI/8+t*.3f;child.localPosition=new Vector3(Mathf.Cos(an)*8,.2f+Mathf.Repeat(local*1.5f+n*.35f,6),Mathf.Sin(an)*8);n++;}}
        if(s.scene==8)
        {
            for(int i=0;i<4;i++)cast[i].Travel(new Vector3((i%2)*5-15.5f,.05f,127+(i/2)*8),new Vector3((i%2)*5-15.5f,.05f,170+(i/2)*8),local/6,t+i*.4f,true);
            primal.Travel(new Vector3(-13,.7f,101),new Vector3(-13,.7f,146),local/6,t,true);
        }
        if(s.scene==10)for(int i=0;i<4;i++){cast[i].Travel(new Vector3((i%2)*4-2,.05f,198+(i/2)*6),new Vector3((i%2)*4-2,.05f,225+(i/2)*6),local/5,t+i*.4f,true);if(cast[i].root.transform.position.z>222)cast[i].root.SetActive(false);}
        foreach(var p in particles){if(p==null||!p.gameObject.activeInHierarchy)continue;float stamp=Mathf.Min(t,6);p.Simulate(stamp,true,true,true);}particleTime=t;
    }
    sealed class Book
    {
        Transform rig,hinge;Vector3 rest;MeshFilter paper;Mesh mesh;Vector3[] vertices;int segments=12;
        public Book(ArchiveAnimatedBook source)
        {
            rig=source.bookRig;hinge=source.coverHinge;rest=rig.localPosition;paper=source.turningPage;mesh=new Mesh{name="Teaser turning page"};vertices=new Vector3[52];var uv=new Vector2[52];var tri=new List<int>();
            for(int side=0;side<2;side++)for(int i=0;i<=segments;i++)for(int edge=0;edge<2;edge++)uv[side*26+i*2+edge]=new Vector2(i/12f,edge);
            for(int side=0;side<2;side++)for(int i=0;i<segments;i++){int a=side*26+i*2,b=a+1,c=a+2,d=a+3;tri.AddRange(side==0?new[]{a,b,c,b,d,c}:new[]{a,c,b,b,c,d});}mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=tri.ToArray();paper.sharedMesh=mesh;
        }
        public void Set(float t,float open)
        {
            rig.localPosition=rest+new Vector3(.55f*open,(.38f+Mathf.Sin(t*1.7f)*.035f)*open,0);rig.localRotation=Quaternion.Euler(Mathf.Sin(t*.7f)*2*open,Mathf.Sin(t*.45f)*4*open,0);hinge.localRotation=Quaternion.Euler(0,0,155*open);
            paper.gameObject.SetActive(open>.95f);float angle=Smooth(Mathf.Repeat(t*.48f,1))*Mathf.PI*.86f;
            for(int side=0;side<2;side++)for(int i=0;i<=12;i++)for(int edge=0;edge<2;edge++){float u=i/12f;vertices[side*26+i*2+edge]=new Vector3(-.58f+Mathf.Cos(angle)*u*1.10f,.255f+Mathf.Sin(angle)*u*1.10f+Mathf.Sin(u*Mathf.PI)*Mathf.Sin(angle)*.12f+(side==0?.002f:-.002f),edge==0?-.38f:.38f);}mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        }
    }
    sealed class Crystal
    {
        Transform root;Transform[] shards,rings,motes,runes;Vector3[] rest;Renderer[] renders;Color tint;int topic;MaterialPropertyBlock block=new MaterialPropertyBlock();
        public Crystal(ArchiveCrystalPillar c){root=c.transform;shards=c.shards;rings=c.pulseRings;motes=c.motes;runes=c.runes;rest=c.restPositions;renders=c.shardRenderers;topic=c.station==null?0:c.station.topic;tint=ArchiveCrystalPillar.Palette[topic];}
        public void Set(float t,float progress)
        {
            for(int i=0;i<shards.Length;i++){float part=Smooth(progress*shards.Length-i);shards[i].localPosition=Vector3.Lerp(ArchiveCrystalPillar.BrokenPosition(i,topic),rest[i],part);shards[i].localRotation=Quaternion.Slerp(Quaternion.Euler(25+i%3*19,i*41,32-i*13),Quaternion.identity,part);block.Clear();block.SetColor("_BaseColor",Color.Lerp(tint*.35f,tint,part));block.SetColor("_EmissionColor",tint*(.05f+part*2));renders[i].SetPropertyBlock(block);}
            foreach(var rune in runes)rune.gameObject.SetActive(progress>.98f);
            for(int i=0;i<rings.Length;i++){rings[i].gameObject.SetActive(progress>.98f);float phase=Mathf.Repeat(t*.35f+i*.34f,1);rings[i].localScale=new Vector3(.7f+phase*1.9f,.02f,.7f+phase*1.9f);}
            for(int i=0;i<motes.Length;i++){motes[i].gameObject.SetActive(progress>.98f);float a=t*.55f+i*.7854f;motes[i].localPosition=new Vector3(Mathf.Cos(a)*1.15f,.8f+Mathf.Repeat(a*.5f+i*.23f,3.5f),Mathf.Sin(a)*1.15f);}
        }
    }
    sealed class Actor
    {
        public GameObject root;Transform model,hips;Transform[] bones;Vector3[] restP,restS;Quaternion[] restR;AnimationClip[] clips;bool monster;Vector3 modelPosition;Quaternion modelRotation;AnimationCurve rootCurve;float homeZ;
        public Actor(GameObject instance,bool guardian)
        {
            root=instance;monster=guardian;var animator=instance.GetComponentInChildren<Animator>(true);model=animator.transform;modelPosition=model.localPosition;modelRotation=model.localRotation;clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();bones=model.GetComponentsInChildren<Transform>(true);restP=new Vector3[bones.Length];restR=new Quaternion[bones.Length];restS=new Vector3[bones.Length];
            for(int i=1;i<bones.Length;i++){var src=PrefabUtility.GetCorrespondingObjectFromOriginalSource(bones[i])??bones[i];restP[i]=src.localPosition;restR[i]=src.localRotation;restS[i]=src.localScale;}
            hips=bones.FirstOrDefault(b=>b.name.EndsWith(":Hips"));if(hips!=null){Sample("Idle",0);homeZ=hips.localPosition.z;}
        }
        void Sample(string name,float t)
        {
            for(int i=1;i<bones.Length;i++){bones[i].localPosition=restP[i];bones[i].localRotation=restR[i];bones[i].localScale=restS[i];}
            var clip=clips.FirstOrDefault(c=>c.name==name)??clips.FirstOrDefault(c=>c.name.Contains("_"+name)&&!c.name.Contains("Carry"))??clips.First();
            float phase=Mathf.Repeat(Mathf.Max(0,t),clip.length);clip.SampleAnimation(model.gameObject,phase);
            if(hips!=null&&(name=="Run"||name=="Walk")){var binding=AnimationUtility.GetCurveBindings(clip).FirstOrDefault(c=>c.path.EndsWith(":Hips")&&c.propertyName=="m_LocalPosition.z");rootCurve=AnimationUtility.GetEditorCurve(clip,binding);if(rootCurve!=null){var p=hips.localPosition;p.z-=rootCurve.Evaluate(phase)-homeZ;hips.localPosition=p;}}
        }
        public void Place(Vector3 position,float yaw,string clip,float t){root.SetActive(true);Sample(clip,t);root.transform.position=position;root.transform.rotation=Quaternion.Euler(0,yaw,0);}
        public void Travel(Vector3 from,Vector3 to,float u,float t,bool run){Place(Vector3.Lerp(from,to,Mathf.Clamp01(u)),Quaternion.LookRotation(to-from).eulerAngles.y,monster?"Chase":run?"Run":"Walk",t);}
    }
}

