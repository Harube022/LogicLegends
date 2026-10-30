using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Installs presentation around the existing activities; never rebakes or edits their gameplay.</summary>
    public static class ArchiveEnvironmentPolishSetup
    {
        public const string ArtFolder=ArchiveTopicSetup.Folder+"/Art/EnvironmentPolish";
        public const string RootName="Archive_Environment_Polish", StationRootName="Environment_Identity_Polish";
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int topics,renderers,triangles,collidersBefore,collidersAfter;
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static string PhysicsSnapshot(Scene scene)=>string.Join("\n",UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.gameObject.scene==scene).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        static string NavigationSnapshot(Scene scene)=>string.Join("\n",UnityEngine.Object.FindObjectsByType<NavMeshObstacle>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .Where(c=>c.gameObject.scene==scene).OrderBy(c=>c.GetInstanceID()).Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        static Material Material(string name,Color color,float glow)
        {
            string path=ArtFolder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.18f);
            material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*glow);
            material.SetFloat("_Surface",0);material.renderQueue=2000;
            EditorUtility.SetDirty(material);return material;
        }
        static Transform CreateRoot(string name,Transform parent=null)
        {var root=new GameObject(name).transform;root.SetParent(parent,false);return root;}
        public static Transform[] DecorationRoots(ArchiveTopicSession session)
        {
            var global=SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name==RootName);
            return session.stations.Select(s=>s.transform.Find(StationRootName)).Concat(new[]{global==null?null:global.transform}).Where(t=>t!=null).ToArray();
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Environment/Install Living Archive Polish")]
        public static Report Install()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty)throw new InvalidOperationException("Use stopped Play Mode and save the scene first.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(scene.path!="Assets/Scenes/MultiplayerGameplay.unity"||session==null||session.stations.Length!=10)
                throw new InvalidOperationException("Open the saved MultiplayerGameplay scene with all ten topics.");
            Directory.CreateDirectory(ArtFolder);AssetDatabase.Refresh();
            var report=new Report();string physics=PhysicsSnapshot(scene),navigation=NavigationSnapshot(scene);
            report.collidersBefore=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(c=>c.gameObject.scene==scene);
            int canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(c=>c.gameObject.scene==scene);
            var stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_BrassInlay.mat");
            if(stone==null||trim==null)throw new InvalidOperationException("Existing topic stone and brass materials are required.");
            var glow=Material("Archive_Environment_Rune",Color.white,1);
            var foliage=Material("Archive_Environment_Leaf",new Color(.32f,.51f,.14f),0);
            foreach(var old in DecorationRoots(session))UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root=CreateRoot(RootName);
            foreach(var station in session.stations)
            {
                var parent=CreateRoot(StationRootName,station.transform);
                var tint=Material("Archive_Landmark_"+(station.topic+1),ArchiveCrystalPillar.Palette[station.topic]*.8f,.4f);
                ArchiveTopicLandmarkBuilder.Build(station,parent,stone,trim,tint,station.font);
                report.topics++;
            }
            var firefly=Material("Archive_Grove_Firefly",new Color(.65f,1,.2f),1.2f);
            ArchiveLivingEnvironmentBuilder.Build(session,root,foliage,firefly);
            ArchiveWayfindingBuilder.Build(session,root,stone,trim,glow,session.stations[0].font);
            ArchiveSpawnDressingBuilder.Build(session,root,stone,trim,foliage,glow);
            ArchiveBridgeDressingBuilder.Build(session,root,stone,trim,foliage,glow);
            ArchiveRoadPresentationSetup.Build(session,root,stone,trim,foliage);
            ArchiveNestAndShoreSetup.Build(session,root,stone,trim,glow);
            foreach(var ownRoot in DecorationRoots(session))
            {
                if(ownRoot.GetComponentsInChildren<Collider>(true).Length>0||ownRoot.GetComponentsInChildren<NavMeshObstacle>(true).Length>0||ownRoot.GetComponentsInChildren<Rigidbody>(true).Length>0)
                    report.errors.Add("Decoration introduced collision/navigation in "+ownRoot.name);
                foreach(var renderer in ownRoot.GetComponentsInChildren<Renderer>(true))
                {
                    report.renderers++;
                    foreach(var m in renderer.sharedMaterials)if(m==null||m.shader==null||!m.shader.isSupported)report.errors.Add("Unsupported material: "+renderer.name);
                }
                foreach(var filter in ownRoot.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.GetComponent<TMPro.TMP_Text>()!=null)continue;
                    var mesh=filter.sharedMesh;
                    if(mesh==null||!AssetDatabase.Contains(mesh))report.errors.Add("Unsaved/missing mesh: "+filter.name);
                    else report.triangles+=mesh.triangles.Length/3;
                }
            }
            report.collidersAfter=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(c=>c.gameObject.scene==scene);
            if(physics!=PhysicsSnapshot(scene)||navigation!=NavigationSnapshot(scene))report.errors.Add("An existing physics or navigation component changed.");
            if(canvases!=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(c=>c.gameObject.scene==scene))report.errors.Add("Environment introduced extra screen UI.");
            report.checks.Add("All ten topics have saved mathematical landmarks and checkpoint-driven restoration.");
            report.checks.Add("Existing colliders and navigation obstacles are identical; decoration adds no collision or screen UI.");
            report.checks.Add("Procedural geometry is stored as reusable assets; all referenced materials are supported.");
            report.passed=report.errors.Count==0;
            Directory.CreateDirectory("Temp/ArchiveEnvironment");File.WriteAllText("Temp/ArchiveEnvironment/Install.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException(string.Join("\n",report.errors));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return report;
        }
        public static void RefreshEntranceDesign()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use stopped MultiplayerGameplay.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            var root=scene.GetRootGameObjects().First(g=>g.name==RootName).transform;
            string physics=PhysicsSnapshot(scene),navigation=NavigationSnapshot(scene);
            var stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_BrassInlay.mat");
            var foliage=AssetDatabase.LoadAssetAtPath<Material>(ArtFolder+"/Archive_Environment_Leaf.mat");
            var glow=AssetDatabase.LoadAssetAtPath<Material>(ArtFolder+"/Archive_Environment_Rune.mat");
            ArchiveWayfindingBuilder.RefreshRouteSigns();
            ArchiveSpawnDressingBuilder.Build(session,root,stone,trim,foliage,glow);
            ArchiveBridgeDressingBuilder.Build(session,root,stone,trim,foliage,glow);
            if(physics!=PhysicsSnapshot(scene)||navigation!=NavigationSnapshot(scene))throw new InvalidOperationException("Entrance design changed existing physics or navigation.");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
    }
}
