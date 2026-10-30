using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveCrystalSetup
    {
        const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Crystals";
        public const string PrefabPath=Folder+"/Archive_Restoration_Pillar.prefab";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Crystal Restoration")]
        public static void Install()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Use stopped Play Mode and a saved scene.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(session==null||session.stations.Length!=10)throw new InvalidOperationException("Open MultiplayerGameplay.");
            var prefab=BuildPrefab(session.stations[0]);session.crystalRestoration=true;
            foreach(var station in session.stations)
            {
                var legacy=station.GetComponent<ArchiveT1CoopTrial>();if(legacy!=null){legacy.enabled=false;EditorUtility.SetDirty(legacy);}
                var bonus=station.GetComponent<ArchiveCoopActivityView>();if(bonus!=null){bonus.enabled=false;EditorUtility.SetDirty(bonus);}
                foreach(Transform child in station.transform)if(child.name=="Coop_Relay_Root")child.gameObject.SetActive(false);
                var old=station.GetComponentInChildren<ArchiveCrystalPillar>(true);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,station.transform);go.name="T"+(station.topic+1)+"_Restoration_Crystal";
                go.transform.localPosition=new Vector3(-10,0,0);go.transform.localRotation=Quaternion.identity;go.transform.localScale=Vector3.one;
                var view=go.GetComponent<ArchiveCrystalPillar>();view.station=station;view.status.font=station.font;
                view.status.text="T"+(station.topic+1)+" - "+ArchiveCrystalPillar.ColorNames[station.topic]+" Crystal\n0% RESTORED\nRead the Book to begin";
                // Ten reusable material instances give the saved scene its unique colors before Play.
                var tint=Material("Crystal_"+ArchiveCrystalPillar.ColorNames[station.topic],ArchiveCrystalPillar.Palette[station.topic]*.55f,.22f);
                foreach(var renderer in view.shardRenderers)renderer.sharedMaterial=tint;
                EditorUtility.SetDirty(view);EditorUtility.SetDirty(station);
            }
            var hud=session.GetComponent<ArchiveCrystalHUD>();if(hud==null)hud=session.gameObject.AddComponent<ArchiveCrystalHUD>();hud.session=session;
            EditorUtility.SetDirty(hud);EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);EditorSceneManager.SaveScene(session.gameObject.scene);AssetDatabase.SaveAssets();
        }
        static Material Material(string name,Color color,float glow)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*glow);m.SetFloat("_Smoothness",.38f);
            EditorUtility.SetDirty(m);return m;
        }
        static Mesh SaveMesh(string name,List<Vector3> vertices,List<int> triangles)
        {
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
        static Mesh Shard(int index)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c){int at=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.AddRange(new[]{at,at+1,at+2});}
            float bottom=index==0?.53f:index<4?.68f:.68f-(index-3)*.08f;
            float top=index==7?0:index<3?.68f:.68f-(index-2)*.08f;
            float height=index==7?.8f:.38f;
            var lower=new Vector3[6];var upper=new Vector3[6];
            for(int i=0;i<6;i++){float angle=i*Mathf.PI/3;lower[i]=new Vector3(Mathf.Cos(angle)*bottom,0,Mathf.Sin(angle)*bottom);upper[i]=new Vector3(Mathf.Cos(angle)*top,height+(i%2==0?.035f:0),Mathf.Sin(angle)*top);}
            for(int i=0;i<6;i++){int j=(i+1)%6;Face(lower[i],upper[i],upper[j]);Face(lower[i],upper[j],lower[j]);Face(Vector3.zero,lower[i],lower[j]);Face(Vector3.up*height,upper[j],upper[i]);}
            return SaveMesh("Restoration_Shard_"+index,vertices,triangles);
        }
        static Mesh Ring()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;vertices.Add(new Vector3(Mathf.Cos(a)*.92f,0,Mathf.Sin(a)*.92f));vertices.Add(new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)));}
            for(int i=0;i<48;i++){int a=i*2,b=(i*2+2)%96;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            return SaveMesh("Restoration_Pulse_Ring",vertices,triangles);
        }
        static GameObject BuildPrefab(ArchiveTopicStation source)
        {
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder(Folder.Substring(0,Folder.LastIndexOf('/')),"Crystals");
            var glow=Material("Crystal_Glow",Color.white,1);var dark=Material("Crystal_Dormant",new Color(.10f,.16f,.20f),0);
            var stone=AssetDatabase.LoadAssetAtPath<Material>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Materials/Archive_Topic_Stone.mat");
            var brass=AssetDatabase.LoadAssetAtPath<Material>("Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Topics/Art/Materials/Archive_Topic_BrassInlay.mat");
            var root=new GameObject("Archive_Restoration_Pillar");var view=root.AddComponent<ArchiveCrystalPillar>();
            Transform MeshPart(string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
            {var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(root.transform,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return t;}
            Transform Primitive(string name,PrimitiveType type,Material material,Vector3 p,Vector3 scale,bool collide=false)
            {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
            try
            {
                Primitive("Pillar_Dais",PrimitiveType.Cylinder,stone,new Vector3(0,.16f,0),new Vector3(3.5f,.16f,3.5f),true);
                Primitive("Pillar_Collar",PrimitiveType.Cylinder,brass,new Vector3(0,.37f,0),new Vector3(2.1f,.08f,2.1f));
                var shards=new List<Transform>();float y=.55f;
                for(int i=0;i<8;i++){shards.Add(MeshPart("Restoring_Fragment_"+i,Shard(i),glow,new Vector3(0,y,0),Vector3.one));y+=i==7?.8f:.405f;}
                view.shards=shards.ToArray();view.shardRenderers=shards.Select(t=>t.GetComponent<Renderer>()).ToArray();
                view.restPositions=shards.Select(t=>t.localPosition).ToArray();
                view.runes=new Transform[4];
                for(int i=0;i<4;i++){float a=i*Mathf.PI*.5f;view.runes[i]=Primitive("Player_Rune_"+(i+1),PrimitiveType.Cube,dark,new Vector3(Mathf.Cos(a)*1.10f,.78f,Mathf.Sin(a)*1.10f),new Vector3(.32f,.50f,.18f));view.runes[i].localRotation=Quaternion.Euler(12,-a*Mathf.Rad2Deg,24);}
                view.runeRenderers=view.runes.Select(t=>t.GetComponent<Renderer>()).ToArray();
                var ring=Ring();view.pulseRings=Enumerable.Range(0,3).Select(i=>MeshPart("Full_Restoration_Pulse_"+i,ring,glow,new Vector3(0,.4f,0),Vector3.one)).ToArray();
                view.pulseRenderers=view.pulseRings.Select(t=>t.GetComponent<Renderer>()).ToArray();
                view.motes=Enumerable.Range(0,8).Select(i=>MeshPart("Restoration_Mote_"+i,shards[7].GetComponent<MeshFilter>().sharedMesh,glow,Vector3.zero,Vector3.one*.1f)).ToArray();
                view.moteRenderers=view.motes.Select(t=>t.GetComponent<Renderer>()).ToArray();foreach(var t in view.pulseRings.Concat(view.motes))t.gameObject.SetActive(false);
                Primitive("Crystal_Progress_Track",PrimitiveType.Cube,dark,new Vector3(0,4.34f,0),new Vector3(3.3f,.10f,.08f));
                view.progressFill=Primitive("Crystal_Progress_Fill",PrimitiveType.Cube,glow,new Vector3(-1.6f,4.34f,0),new Vector3(.001f,.075f,.06f));
                var label=new GameObject("Crystal_Progress_Label",typeof(TextMeshPro));label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,4.9f,0);
                view.status=label.GetComponent<TMP_Text>();view.status.font=source.font;view.status.text="TOPIC CRYSTAL\n0% RESTORED";view.status.fontSize=3;view.status.rectTransform.sizeDelta=new Vector2(5,1.05f);view.status.alignment=TextAlignmentOptions.Center;view.status.color=new Color(1,.96f,.84f);view.status.richText=false;
                view.status.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                view.completionAudio=root.AddComponent<AudioSource>();view.completionAudio.playOnAwake=false;view.completionAudio.spatialBlend=1;view.completionAudio.volume=.5f;view.completionAudio.maxDistance=35;
                view.completionAudio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");
                for(int i=0;i<shards.Count;i++){shards[i].localPosition=ArchiveCrystalPillar.BrokenPosition(i,0);shards[i].localRotation=Quaternion.Euler(25+(i%3)*19,i*41,32-i*13);}
                return PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
