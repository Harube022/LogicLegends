using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveExitAuthoring
    {
        public const string Folder=ArchiveTopicSetup.Folder+"/Exit";
        public const string PrefabPath=Folder+"/Archive_Victory_Portal.prefab";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Team Victory Portal")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before saving portal assets.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ArchiveTopicSetup.ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved, clean MultiplayerGameplay topic scene first.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(session==null)throw new InvalidOperationException("Expected the existing team topic session.");
            if(session.exitPortal!=null)throw new InvalidOperationException("Portal is already installed; edit the saved instance in place.");
            Directory.CreateDirectory(Folder+"/Meshes");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
            var root=new GameObject("Archive_Victory_Portal");
            var portal=root.AddComponent<ArchiveVictoryPortal>();
            var shader=Shader.Find("LogicLegends/OpenArchive/ExitPortal");
            if(shader==null)throw new InvalidOperationException("Portal shader must compile first.");
            var veil=Material("Archive_Portal_Membrane",shader,portal.energyColor,1);
            var cyan=Material("Archive_Portal_Energy",shader,portal.energyColor,0);
            var gold=Material("Archive_Portal_Gold",shader,portal.runeColor,0);
            var disc=Disc();var ring=Ring("Archive_Portal_Ring",.96f,1,false);var broken=Ring("Archive_Portal_Segmented_Ring",.94f,1,true);
            var visuals=new GameObject("Portal_Formation").transform;visuals.SetParent(root.transform,false);portal.visuals=visuals;
            portal.membrane=Surface("Swirling_Aperture",visuals,disc,veil,Vector3.zero,1);
            portal.outerRing=Surface("Outer_Counterclockwise_Arc",visuals,broken,gold,new Vector3(0,0,-.065f),1.035f).transform;
            portal.innerRing=Surface("Inner_Clockwise_Arc",visuals,broken,cyan,new Vector3(0,0,-.09f),.92f).transform;
            var outline=Surface("Continuous_Luminous_Rim",visuals,ring,cyan,new Vector3(0,0,-.045f),1).transform;
            var inner=Surface("Inner_Gold_Seal",visuals,ring,gold,new Vector3(0,0,-.12f),.77f);
            portal.runeOrbit=new GameObject("Ten_Topic_Runes").transform;portal.runeOrbit.SetParent(visuals,false);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArchiveTopicSetup.Folder+"/Art/Archive_T7_Reading.asset");
            if(font==null)throw new InvalidOperationException("Existing mathematical reading font is missing.");
            string[] glyphs={"∧","⊤","∴","∈","⇒","Σ","∪","→","∩","∀"};
            for(int i=0;i<glyphs.Length;i++)
            {
                float a=i*Mathf.PI*2/glyphs.Length;
                var text=new GameObject("Topic_"+(i+1)+"_Rune",typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                text.transform.SetParent(portal.runeOrbit,false);text.transform.localPosition=new Vector3(Mathf.Sin(a)*.855f,Mathf.Cos(a)*.855f,-.14f);
                text.transform.localRotation=Quaternion.Euler(0,0,-i*36);
                text.font=font;text.fontSharedMaterial=RuneFontMaterial(font);text.text=glyphs[i];text.fontSize=1.75f;text.alignment=TextAlignmentOptions.Center;
                text.rectTransform.sizeDelta=new Vector2(.23f,.23f);text.color=new Color(1,.78f,.28f);text.raycastTarget=false;
                text.renderer.shadowCastingMode=ShadowCastingMode.Off;text.renderer.receiveShadows=false;
                text.ForceMeshUpdate();
            }
            portal.groundSeal=Surface("Ground_Summoning_Seal",root.transform,broken,gold,new Vector3(0,.035f,0),11).transform;
            portal.groundSeal.localRotation=Quaternion.Euler(90,0,0);
            portal.energyRenderers=new[]{outline.GetComponent<Renderer>(),portal.innerRing.GetComponent<Renderer>()};
            portal.runeRenderers=new[]{portal.outerRing.GetComponent<Renderer>(),inner,portal.groundSeal.GetComponent<Renderer>()};
            var particleObject=new GameObject("Rising_Energy_Fragments");particleObject.transform.SetParent(root.transform,false);particleObject.transform.localPosition=Vector3.up*13;
            var particles=particleObject.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=particles.main;main.playOnAwake=false;main.loop=true;main.duration=3;main.maxParticles=48;main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.3f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.12f,.3f);main.startColor=new Color(.3f,1,.95f,.75f);main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=particles.emission;emission.rateOverTime=17;
            var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(20,24,.6f);
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;velocity.x=new ParticleSystem.MinMaxCurve(-.35f,.35f);velocity.y=new ParticleSystem.MinMaxCurve(1.2f,3.2f);velocity.z=new ParticleSystem.MinMaxCurve(-.1f,.1f);
            var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,0),new Keyframe(.15f,1),new Keyframe(.85f,.7f),new Keyframe(1,0)));
            var pr=particles.GetComponent<ParticleSystemRenderer>();pr.sharedMaterial=Material("Archive_Portal_Fragments",Shader.Find("Universal Render Pipeline/Particles/Unlit"),new Color(.2f,1,.9f),0);
            pr.renderMode=ParticleSystemRenderMode.Mesh;pr.mesh=Diamond();pr.shadowCastingMode=ShadowCastingMode.Off;pr.receiveShadows=false;
            portal.motes=particles;
            var sound=root.AddComponent<AudioSource>();sound.playOnAwake=false;sound.loop=false;sound.volume=.32f;sound.spatialBlend=.6f;sound.rolloffMode=AudioRolloffMode.Linear;sound.minDistance=12;sound.maxDistance=250;
            sound.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");portal.openingSound=sound;
            var aperture=root.AddComponent<BoxCollider>();aperture.isTrigger=true;aperture.center=Vector3.up*13;aperture.size=new Vector3(22,26,5.5f);aperture.enabled=false;portal.aperture=aperture;
            visuals.gameObject.SetActive(false);portal.groundSeal.gameObject.SetActive(false);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.position=new Vector3(0,.15f,231);
            portal=instance.GetComponent<ArchiveVictoryPortal>();portal.session=session;session.exitPortal=portal;
            var ui=new GameObject("Archive_Escape_Interface",typeof(RectTransform)).AddComponent<ArchiveExitUI>();ui.session=session;
            EditorUtility.SetDirty(portal);EditorUtility.SetDirty(session);EditorUtility.SetDirty(ui);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Team victory portal saved inside the existing northern arch. No environment or navigation sources changed.");
        }
        static Material Material(string name,Shader shader,Color color,float membrane)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}
            if(m.HasProperty("_Tint"))m.SetColor("_Tint",color);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Membrane"))m.SetFloat("_Membrane",membrane);
            EditorUtility.SetDirty(m);return m;
        }
        public static Material RuneFontMaterial(TMP_FontAsset font)
        {
            string path=Folder+"/Materials/Archive_Portal_Rune_Font.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(font.material){name="Archive_Portal_Rune_Font",renderQueue=3050};AssetDatabase.CreateAsset(material,path);}
            return material;
        }
        static Renderer Surface(string name,Transform parent,Mesh mesh,Material mat,Vector3 at,float scale)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=Vector3.one*scale;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        static Mesh SaveMesh(string name,List<Vector3> vertices,List<int> triangles)
        {
            string path=Folder+"/Meshes/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing!=null)return existing;
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
            mesh.SetUVs(0,vertices.Select(v=>new Vector2(v.x*.5f+.5f,v.y*.5f+.5f)).ToList());mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Disc()
        {
            var v=new List<Vector3>{Vector3.zero};var t=new List<int>();
            for(int i=0;i<=96;i++){float a=i*Mathf.PI*2/96;v.Add(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0));if(i>0)t.AddRange(new[]{0,i,i+1});}
            return SaveMesh("Archive_Portal_Disc",v,t);
        }
        static Mesh Ring(string name,float inner,float outer,bool gaps)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<120;i++)
            {
                if(gaps&&i%12<3)continue;
                float a=i*Mathf.PI*2/120,b=(i+1)*Mathf.PI*2/120;int n=v.Count;
                v.Add(new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,0));v.Add(new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,0));
                v.Add(new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,0));v.Add(new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,0));t.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3});
            }
            return SaveMesh(name,v,t);
        }
        static Mesh Diamond()=>SaveMesh("Archive_Portal_Fragment",new List<Vector3>{Vector3.up,Vector3.right*.5f,Vector3.down,Vector3.left*.5f},new List<int>{0,1,2,0,2,3,2,1,0,3,2,0});
    }
}
