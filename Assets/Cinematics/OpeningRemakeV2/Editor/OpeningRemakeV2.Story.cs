using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static partial class OpeningRemakeV2
{
    static GameObject worldMap,classroom,schoolRoot,spellRoot;
    static List<Actor> schoolCast=new List<Actor>();
    static ParticleSystem[] mapClouds;
    static ParticleSystem.Particle[] cloudParticles=new ParticleSystem.Particle[128];
    static Color[] cloudColors;
    static Transform[] mapGlyphs,spellOrbs;
    static Vector3[] glyphScales;
    static Quaternion[] glyphRotations;
    static LineRenderer[] schoolSeals;
    static LineRenderer spellBeam;
    static Light handLight;
    static Light classroomLight;
    static Transform leftHand,rightHand;
    static AnimationClip welcome;
    [Serializable]class StoryRest{public Color[] colors;public Vector3[] scales;public Quaternion[] rotations;}
    static readonly int[] Chosen={6,0,11,5};
    static Vector3 SchoolHome(int i){return new Vector3(-650+(i%3)*9.6f+(i/3)*4.1f,13.82f,398.2f+(i/3)*10.1f);}
    static Vector3 StoryPortal{get{return SanctuaryPortal+new Vector3(0,0,2.6f);}}
    static Vector3 GardenLanding(int i){return new Vector3((i-1.5f)*2.8f,.02f,-43.6f+(i%2)*.4f);}

    static void BuildStorySets(Scene target)
    {
        var context=SceneManager.GetSceneByPath("Assets/Scenes/CINEMATICS.unity");
        if(!context.isLoaded)context=EditorSceneManager.OpenScene("Assets/Scenes/CINEMATICS.unity",OpenSceneMode.Additive);
        foreach(string name in new[]{"LOGICREALM3_0 (1)","CLASSROOM"})
        {
            var source=context.GetRootGameObjects().Single(r=>r.name==name);
            var copy=UnityEngine.Object.Instantiate(source);copy.name=name.StartsWith("LOGICREALM")?"Cinematic World Map":"Cinematic Classroom";
            SceneManager.MoveGameObjectToScene(copy,target);Strip(copy,false);
            foreach(var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.enabled=false;
            if(name.StartsWith("LOGICREALM"))
                foreach(var cloud in copy.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var at=cloud.transform.position;
                    // Retain the original smoke corridor, with fewer off-screen particle grids.
                    cloud.gameObject.SetActive(at.y>0&&at.z>-1480&&at.z<-850&&at.x<-1100);
                    var main=cloud.main;main.maxParticles=Mathf.Min(main.maxParticles,100);
                }
        }
        var students=new GameObject("Cinematic School Classmates");SceneManager.MoveGameObjectToScene(students,target);
        string[] prefabs={
            "Assets/Characters_NEW/LATEST_SKINS/Male_Skins/Uniform/Prefabs/Male_Uniform_Gameplay.prefab",
            "Assets/Characters_NEW/LATEST_SKINS/Female_Skins/Uniform/Prefabs/Female_Uniform_Gameplay.prefab",
            "Assets/Characters_NEW/LATEST_SKINS/Male_Skins/Common/Prefabs/Male_Common_Gameplay.prefab",
            "Assets/Characters_NEW/LATEST_SKINS/Female_Skins/Common/Prefabs/Female_Common_Gameplay.prefab"};
        for(int i=0;i<6;i++)
        {
            var student=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabs[i%4]),target);
            student.name="Classmate "+(i+1);student.transform.SetParent(students.transform,true);PrepareActor(student,false);
            student.transform.localScale*=1.65f;
        }
        var fx=new GameObject("Cinematic Summoning Performance");SceneManager.MoveGameObjectToScene(fx,target);
        var gold=MakeMaterial("Summoning Gold",new Color(3f,1.65f,.3f));var cyan=MakeMaterial("Summoning Cyan",new Color(.3f,2.2f,3f));
        for(int i=0;i<6;i++)
        {
            var circle=new GameObject("School Seal "+i).AddComponent<LineRenderer>();circle.transform.SetParent(fx.transform);
            circle.sharedMaterial=gold;circle.useWorldSpace=false;circle.loop=true;circle.positionCount=64;circle.startWidth=circle.endWidth=.065f;
            for(int j=0;j<64;j++){float a=j*Mathf.PI/32;float r=j%8==0?1.32f:1.18f;circle.SetPosition(j,new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));}
        }
        var beam=new GameObject("Guardian Casting Thread").AddComponent<LineRenderer>();beam.transform.SetParent(fx.transform);
        beam.sharedMaterial=cyan;beam.positionCount=32;beam.startWidth=.07f;beam.endWidth=.025f;
        for(int i=0;i<9;i++)
        {
            var orb=GameObject.CreatePrimitive(PrimitiveType.Sphere);orb.name="Casting Spark "+i;orb.transform.SetParent(fx.transform);
            UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());orb.GetComponent<Renderer>().sharedMaterial=i%2==0?gold:cyan;
        }
        var fill=new GameObject("Guardian Hand Light").AddComponent<Light>();fill.transform.SetParent(fx.transform);fill.type=LightType.Point;fill.range=7;fill.color=new Color(.25f,.8f,1);fill.shadows=LightShadows.None;
        var roomFill=new GameObject("Classroom Soft Fill").AddComponent<Light>();roomFill.transform.SetParent(fx.transform);roomFill.type=LightType.Point;roomFill.range=35;roomFill.intensity=2.2f;roomFill.transform.position=new Vector3(-640,28,400);roomFill.color=new Color(1,.9f,.76f);roomFill.shadows=LightShadows.None;
        CreateChalkboard(target);
        CreateRoomCeiling(target);
    }

    public static void CreateChalkboard(Scene target)
    {
        if(target.GetRootGameObjects().Any(r=>r.name=="Cinematic Logic Blackboard"))return;
        var text=new GameObject("Cinematic Logic Blackboard").AddComponent<TextMeshPro>();SceneManager.MoveGameObjectToScene(text.gameObject,target);
        text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.fontSize=5.2f;text.color=new Color(.83f,.93f,.82f);text.alignment=TextAlignmentOptions.Left;
        text.text="DISCRETE MATHEMATICS 1\n\nAND: p AND q\nBoth statements must be true.\nOR: p OR q\nAt least one must be true.\nNOT: NOT p\nReverse the truth value.";
        text.rectTransform.sizeDelta=new Vector2(14.8f,8.6f);text.transform.SetPositionAndRotation(new Vector3(-640.4f,24,385.03f),Quaternion.Euler(0,180,0));
        var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name="Cinematic Chalk Surface";SceneManager.MoveGameObjectToScene(panel,target);UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());
        panel.transform.position=new Vector3(-640.4f,24,384.93f);panel.transform.localScale=new Vector3(16.1f,9.4f,.05f);panel.GetComponent<Renderer>().sharedMaterial=MakeMaterial("Chalkboard Green",new Color(.075f,.13f,.105f));
    }

    public static void CreateRoomCeiling(Scene target)
    {
        if(target.GetRootGameObjects().Any(r=>r.name=="Cinematic Classroom Ceiling"))return;
        var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Cinematic Classroom Ceiling";SceneManager.MoveGameObjectToScene(ceiling,target);UnityEngine.Object.DestroyImmediate(ceiling.GetComponent<Collider>());
        ceiling.transform.position=new Vector3(-640.4f,28.5f,400.74f);ceiling.transform.localScale=new Vector3(55,.35f,55);ceiling.GetComponent<Renderer>().sharedMaterial=MakeMaterial("Classroom Ceiling",new Color(.26f,.27f,.29f));
    }

    static void BindStorySets()
    {
        var roots=film.GetRootGameObjects();worldMap=roots.Single(r=>r.name=="Cinematic World Map");classroom=roots.Single(r=>r.name=="Cinematic Classroom");
        schoolRoot=roots.Single(r=>r.name=="Cinematic School Classmates");spellRoot=roots.Single(r=>r.name=="Cinematic Summoning Performance");
        schoolCast=schoolRoot.transform.Cast<Transform>().Select(t=>new Actor(t.gameObject,false)).ToList();
        mapClouds=worldMap.GetComponentsInChildren<ParticleSystem>(true).Where(p=>p.gameObject.activeSelf).ToArray();cloudColors=mapClouds.Select(p=>p.main.startColor.color).ToArray();
        mapGlyphs=worldMap.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.transform).ToArray();
        string restFile=Output+"/EditSource/story-rest-state.json";
        if(!File.Exists(restFile))
        {
            var sourceScene=SceneManager.GetSceneByPath("Assets/Scenes/CINEMATICS.unity");
            if(!sourceScene.isLoaded)sourceScene=EditorSceneManager.OpenScene("Assets/Scenes/CINEMATICS.unity",OpenSceneMode.Additive);
            var source=sourceScene.GetRootGameObjects().Single(r=>r.name=="LOGICREALM3_0 (1)").transform;
            var rest=new StoryRest{
                colors=mapClouds.Select(p=>source.Find(AnimationUtility.CalculateTransformPath(p.transform,worldMap.transform)).GetComponent<ParticleSystem>().main.startColor.color).ToArray(),
                scales=mapGlyphs.Select(p=>source.Find(AnimationUtility.CalculateTransformPath(p,worldMap.transform)).localScale).ToArray(),
                rotations=mapGlyphs.Select(p=>source.Find(AnimationUtility.CalculateTransformPath(p,worldMap.transform)).localRotation).ToArray()};
            File.WriteAllText(restFile,JsonUtility.ToJson(rest,true));
        }
        var stored=JsonUtility.FromJson<StoryRest>(File.ReadAllText(restFile));cloudColors=stored.colors;glyphScales=stored.scales;glyphRotations=stored.rotations;
        schoolSeals=spellRoot.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.name.StartsWith("School Seal")).OrderBy(l=>l.name).ToArray();
        spellBeam=spellRoot.GetComponentsInChildren<LineRenderer>(true).Single(l=>l.name=="Guardian Casting Thread");
        spellOrbs=spellRoot.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Casting Spark")).ToArray();
        handLight=spellRoot.GetComponentsInChildren<Light>(true).Single(l=>l.name=="Guardian Hand Light");
        classroomLight=spellRoot.GetComponentsInChildren<Light>(true).Single(l=>l.name=="Classroom Soft Fill");
        var bones=wizard.root.GetComponentsInChildren<Transform>(true);leftHand=bones.First(t=>t.name.EndsWith(":LeftHand"));rightHand=bones.First(t=>t.name.EndsWith(":RightHand"));
        welcome=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Characters_NEW/LATEST_SKINS/Wizard/Animations/Wizard_Wave.anim");
        BindSchoolPolish();
        BindRealm3D();
        BindPillarSymbolRings();
    }

    static void EvaluateStory(Shot shot,float time)
    {
        SceneManager.SetActiveScene(film);
        float duration=shot.end-shot.start,u=Mathf.Clamp01((time-shot.start)/duration),e=Smooth(u);
        var at=shot.continuous?Hermite(shot.from,shot.to,shot.tangentFrom*duration,shot.tangentTo*duration,u):shot.curved?Bezier(shot.from,shot.control1,shot.control2,shot.to,e):Lerp(shot.from,shot.to,e);
        var aim=shot.continuous?Hermite(shot.aimFrom,shot.aimTo,shot.aimTangentFrom*duration,shot.aimTangentTo*duration,u):Lerp(shot.aimFrom,shot.aimTo,e);
        camera.transform.position=at;camera.transform.rotation=Quaternion.LookRotation(aim-at,Vector3.up);camera.fieldOfView=Mathf.Lerp(shot.fov0,shot.fov1,e);camera.aspect=16f/9;
        bool map=shot.scene<=2,school=shot.label.StartsWith("Classroom"),summoning=shot.scene==9;
        if(map)camera.transform.rotation=Quaternion.LookRotation(aim-at,Vector3.right);
        camera.farClipPlane=map?3000:700;camera.clearFlags=map?CameraClearFlags.SolidColor:CameraClearFlags.Skybox;camera.backgroundColor=new Color(.045f,.085f,.12f);
        worldMap.SetActive(map);classroom.SetActive(school);schoolRoot.SetActive(school);
        float fracture=time<timeline.firstCrack?0:time<timeline.spreadCrack?Mathf.SmoothStep(0,.32f,Mathf.InverseLerp(timeline.firstCrack,timeline.spreadCrack,time)):shot.scene==6?Mathf.Lerp(.32f,.8f,e):1;
        float dim=shot.scene<5?0:shot.scene==5?Mathf.Lerp(0,.25f,fracture*3):shot.scene==6?Mathf.Lerp(.25f,.55f,e):shot.scene==7?.65f:shot.scene==8?.45f:.08f;
        float flicker=shot.scene>=5&&shot.scene<=8?1-.1f*Mathf.Sin(time*7.1f)*Mathf.Sin(time*3.3f):1;
        grade.postExposure.Override(-.8f*dim);grade.saturation.Override(Mathf.Lerp(2,-25,dim));sun.intensity=Mathf.Lerp(1.05f,.42f,dim)*flicker;
        bool sanctuary=!map&&!school&&shot.scene<10;sun.transform.rotation=Quaternion.Euler(42,sanctuary?145:330,0);
        RenderSettings.ambientSkyColor=Color.Lerp(new Color(.46f,.57f,.67f),new Color(.11f,.17f,.27f),dim);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.34f,.38f,.38f),new Color(.09f,.12f,.17f),dim);RenderSettings.ambientGroundColor=new Color(.2f,.19f,.17f)*(1-dim*.65f);
        for(int i=0;i<lights.Count;i++)lights[i].intensity=intensities[i]*(1-dim*.65f)*flicker;
        classroomLight.intensity=school?35:0;
        if(school){RenderSettings.ambientSkyColor=new Color(.8f,.82f,.84f);RenderSettings.ambientEquatorColor=new Color(.62f,.64f,.66f);grade.postExposure.Override(-.04f);}
        crystalLight.intensity=(shot.scene<5?3:Mathf.Lerp(2.7f,.5f,fracture))*flicker;foreach(var crack in cracks)crack.Set(fracture);
        EvaluatePillarSymbolRings(time);
        foreach(var actor in cast)actor.root.SetActive(false);wizard.root.SetActive(!school&&shot.scene>=9);portal.gameObject.SetActive(!school&&(shot.scene==9||shot.scene==10));
        foreach(var circle in schoolSeals)circle.gameObject.SetActive(false);
        EvaluateSchoolPolish(school,summoning,time);
        spellBeam.gameObject.SetActive(summoning&&!school&&time>timeline.portalCall-1.2f);foreach(var spark in spellOrbs)spark.gameObject.SetActive(spellBeam.gameObject.activeSelf);handLight.gameObject.SetActive(spellBeam.gameObject.activeSelf);
        if(map)
        {
            float smoke=Mathf.Lerp(.8f,.045f,Smooth(Mathf.InverseLerp(3,20,time)));
            for(int i=0;i<mapClouds.Length;i++){var main=mapClouds[i].main;var col=cloudColors[i];col.r*=.83f;col.g*=.92f;col.a*=smoke;main.startColor=col;}
            float glyphScale=1-Smooth(Mathf.InverseLerp(16,23,time));
            for(int i=0;i<mapGlyphs.Length;i++){mapGlyphs[i].localScale=glyphScales[i]*Mathf.Max(.001f,glyphScale);mapGlyphs[i].localRotation=glyphRotations[i]*Quaternion.Euler(Mathf.Sin(time*.24f+i)*5,0,0);}
        }
        if(school)
        {
            for(int i=0;i<schoolCast.Count;i++)
            {
                var student=schoolCast[i];student.root.SetActive(true);student.root.transform.SetPositionAndRotation(SchoolHome(i),Quaternion.Euler(0,180,0));student.Pose(student.idle,time*.85f+i*.43f);
            }
        }
        if(summoning&&!school)
        {
            float local=time-96.223333f;var initial=SanctuaryWizard+new Vector3(-4.6f,0,2.4f);
            if(local<2f)wizard.WalkFacing(initial,SanctuaryWizard,Mathf.Clamp01(local/2f),2f,local,118,148);
            else
            {
                wizard.root.transform.SetPositionAndRotation(SanctuaryWizard,Quaternion.Euler(0,148,0));
                float gestureTime=Mathf.Max(0,time-(timeline.portalCall-2f))*.35f;
                float weight=Smooth(Mathf.InverseLerp(timeline.portalCall-2.3f,timeline.portalCall-1.6f,time));
                wizard.Pose(wizard.gesture,Mathf.Min(gestureTime,wizard.gesture.length-.01f),wizard.idle,local,weight);
            }
            portal.position=StoryPortal;portal.rotation=Quaternion.identity;AnimatePortal(Mathf.SmoothStep(0,1,Mathf.Clamp01((time-timeline.portalCall)/2.1f)),time);
            if(spellBeam.gameObject.activeSelf)
            {
                var hand=(leftHand.position+rightHand.position)*.5f;var target=StoryPortal+Vector3.up*5.2f;
                for(int j=0;j<32;j++){float f=j/31f;var side=new Vector3(Mathf.Sin(f*13-time*5),Mathf.Cos(f*13-time*5),0)*(.07f+.22f*Mathf.Sin(f*Mathf.PI));spellBeam.SetPosition(j,Vector3.Lerp(hand,target,f)+side);}
                for(int j=0;j<spellOrbs.Length;j++){float angle=time*3+j*Mathf.PI*2/spellOrbs.Length;spellOrbs[j].position=hand+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),Mathf.Sin(angle*.7f))*.4f;spellOrbs[j].localScale=Vector3.one*(j==0?.25f:.13f);}
                handLight.transform.position=hand;handLight.intensity=2.1f+.4f*Mathf.Sin(time*7);
            }
        }
        if(shot.scene==10)
        {
            float local=time-shot.start;wizard.root.transform.SetPositionAndRotation(GardenWizard+new Vector3(-3.5f,0,2),Quaternion.Euler(0,28,0));wizard.Pose(welcome,local*.8f);
            portal.position=new Vector3(0,.02f,-39.8f);portal.rotation=Quaternion.identity;AnimatePortal(1-Smooth(Mathf.InverseLerp(5.25f,6.5f,local)),time);
            for(int order=0;order<Chosen.Length;order++)
            {
                var actor=cast[Chosen[order]];float moving=local-order*.42f;actor.root.SetActive(moving>=0);
                actor.Walk(new Vector3((order%2-.5f)*1.7f,.02f,-39.7f),GardenLanding(order),Mathf.Clamp01(moving/4.45f),4.45f,moving);
            }
        }
        if(shot.scene==11)
        {
            float local=time-timeline.shots.First(s=>s.scene==11).start;
            if(local<1.7f)wizard.WalkFacing(GardenWizard+new Vector3(-3.5f,0,2),GardenWizard,Mathf.Clamp01(local/1.7f),1.7f,local,28,180);
            else{wizard.root.transform.SetPositionAndRotation(GardenWizard,Quaternion.Euler(0,180,0));wizard.Pose(welcome,(local-1.7f)*.65f,wizard.idle,local,Smooth(Mathf.InverseLerp(1.7f,2.3f,local)));}
            for(int order=0;order<Chosen.Length;order++)
            {
                var actor=cast[Chosen[order]];actor.root.SetActive(true);var home=GardenLanding(order);var to=home+new Vector3(0,0,-1.2f);
                actor.Walk(home,to,Mathf.Clamp01(local/2.4f),2.4f,local);
            }
        }
        foreach(var p in particles)
        {
            if(!p.gameObject.activeInHierarchy){simulatedAt.Remove(p.GetInstanceID());continue;}
            float previous;bool advance=simulatedAt.TryGetValue(p.GetInstanceID(),out previous)&&time>=previous&&time-previous<.15f;
            p.Simulate(advance?time-previous:time,false,!advance,false);simulatedAt[p.GetInstanceID()]=time;
        }
        if(map)
        {
            // Updating startColor alone affects new particles only. Recolor the
            // live smoke too, so sequential renders match isolated previews.
            float smoke=Mathf.Lerp(.8f,.045f,Smooth(Mathf.InverseLerp(3,20,time)));
            for(int i=0;i<mapClouds.Length;i++)
            {
                var system=mapClouds[i];int count=system.GetParticles(cloudParticles);var color=cloudColors[i];color.r*=.83f;color.g*=.92f;color.a*=smoke;
                for(int j=0;j<count;j++)cloudParticles[j].startColor=color;
                system.SetParticles(cloudParticles,count);
            }
        }
        EvaluateRealm3D(map,time);
    }

    static void AnimatePortal(float formation,float time)
    {
        portalVisual.gameObject.SetActive(true);portalVisual.localPosition=Vector3.up*13;portalVisual.localScale=new Vector3(11,13,1)*Mathf.Max(.001f,formation);
        if(outer!=null)outer.localRotation=Quaternion.Euler(0,0,time*7);if(inner!=null)inner.localRotation=Quaternion.Euler(0,0,-time*4.7f);if(runes!=null)runes.localRotation=Quaternion.Euler(0,0,-time*1.96f);
        if(seal!=null){seal.gameObject.SetActive(true);seal.localScale=Vector3.one*11*Mathf.Max(.001f,formation);seal.localRotation=Quaternion.Euler(90,0,-time*4.9f);}
        foreach(var m in portalMaterials){if(m.HasProperty("_Formation"))m.SetFloat("_Formation",formation);if(m.HasProperty("_ReviewTime"))m.SetFloat("_ReviewTime",time);}
    }
}
