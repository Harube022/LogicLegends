using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Scene-scoped roadside rendering. Never edits a source collider or navigation asset.</summary>
    public static class ArchiveRoadPresentationSetup
    {
        const string Folder=ArchiveEnvironmentPolishSetup.ArtFolder;
        [Serializable] public sealed class Report
        {
            public bool passed,physicsPreserved,navigationPreserved;
            public ArchiveRoadSurfaceBuilder.Report roads;
            public ArchiveRuinVegetationCleanup.Report carvings;
            public ArchiveRoadsideDressingBuilder.Report dressing;
            public string scope="Production MultiplayerGameplay presentation only. Original physical walking surfaces, source meshes, player controls and encounter code are retained.";
        }
        static string Physics(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>(true)).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)+":"+c.transform.localToWorldMatrix));
        static string Navigation(Scene scene)=>string.Join("\n",scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true))
            .Where(c=>c is UnityEngine.AI.NavMeshAgent||c is UnityEngine.AI.NavMeshObstacle||c is Hunting.KhezuNavigationData).OrderBy(c=>c.GetInstanceID())
            .Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)));
        static Material RoadMaterial(Transform map)
        {
            var shader=Shader.Find("LogicLegends/OpenArchive/RoadShoulder");
            if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Road shoulder shader did not compile.");
            string path=Folder+"/Archive_Continuous_Weathered_Road.mat";
            var road=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(road==null){road=new Material(shader){name="Archive_Continuous_Weathered_Road",enableInstancing=true};AssetDatabase.CreateAsset(road,path);}
            road.shader=shader;road.renderQueue=2000;
            var ground=map.Find("Environment/Terrain/Island_Grassland_WithRiverHoles").GetComponent<Renderer>().sharedMaterial;
            var pavingFilter=map.Find("Environment/MainPaths/Continuous_Main_Ring_10m").GetComponent<MeshFilter>();
            var paving=pavingFilter.GetComponent<Renderer>().sharedMaterial;
            foreach(string name in new[]{"_DetailAtlas","_MacroMap","_GrassMap"})road.SetTexture(name,ground.GetTexture(name));
            road.SetTexture("_StoneMap",paving.GetTexture("_StoneMap"));
            road.SetColor("_GrassColor",ground.GetColor("_BaseColor"));road.SetVector("_GrassReference",ground.GetVector("_GrassReference"));
            road.SetFloat("_GrassRepeat",ground.GetFloat("_TileMeters"));road.SetFloat("_PavingRepeat",paving.GetFloat("_TileMeters"));
            road.SetFloat("_GrassStrength",ground.GetFloat("_GrassTextureStrength"));road.SetFloat("_StoneStrength",paving.GetFloat("_StoneTextureStrength"));
            var tint=new Color(.77f,.71f,.49f);
            var palette=paving.GetTexture("_BaseMap") as Texture2D;string palettePath=palette==null?"":AssetDatabase.GetAssetPath(palette);
            if(File.Exists(palettePath))
            {
                var readable=new Texture2D(2,2,TextureFormat.RGBA32,false);
                try{if(ImageConversion.LoadImage(readable,File.ReadAllBytes(palettePath))){var uv=pavingFilter.sharedMesh.uv[0];tint=readable.GetPixelBilinear(uv.x,uv.y);}}
                finally{UnityEngine.Object.DestroyImmediate(readable);}
            }
            road.SetColor("_PavingColor",tint);EditorUtility.SetDirty(road);return road;
        }
        public static Report Build(ArchiveTopicSession session,Transform parent,Material stone,Material trim,Material foliage)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Author road presentation in stopped Edit Mode.");
            var scene=SceneManager.GetActiveScene();var map=GameObject.Find("OpenArchive").transform;
            string physics=Physics(scene),navigation=Navigation(scene);
            var result=new Report();
            result.roads=ArchiveRoadSurfaceBuilder.Build(map,parent,RoadMaterial(map));
            if(!result.roads.passed)throw new InvalidOperationException("Road union validation failed.");
            result.carvings=ArchiveRuinVegetationCleanup.Apply(map);
            ArchiveRoadsideDressingBuilder.Build(session,parent,stone,trim,foliage);result.dressing=ArchiveRoadsideDressingBuilder.LastReport;
            result.physicsPreserved=physics==Physics(scene);result.navigationPreserved=navigation==Navigation(scene);
            result.passed=result.roads.passed&&result.carvings.passed&&result.physicsPreserved&&result.navigationPreserved;
            Directory.CreateDirectory("Temp/ArchiveRoads");File.WriteAllText("Temp/ArchiveRoads/Presentation.json",JsonUtility.ToJson(result,true));
            if(!result.passed)throw new InvalidOperationException("Road presentation changed existing physics or navigation.");
            return result;
        }
        public static Report Apply()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty||scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use the saved production MultiplayerGameplay scene outside Play Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            var parent=scene.GetRootGameObjects().First(g=>g.name==ArchiveEnvironmentPolishSetup.RootName).transform;
            var stone=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_Stone.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>(ArchiveTopicSetup.Folder+"/Art/Materials/Archive_Topic_BrassInlay.mat");
            var foliage=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Archive_Environment_Leaf.mat");
            var result=Build(session,parent,stone,trim,foliage);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return result;
        }
    }
}
