using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Installs presentation only; reward eligibility and inventory remain in the topic session.</summary>
    public static class ArchiveRewardSetup
    {
        public const string Folder=ArchiveTopicSetup.Folder+"/Art/Rewards";
        const string ChestSource="Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Prefabs/Furniture/Chest.prefab";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Install Topic Rewards And Supply Pack")]
        public static void Install()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.isDirty)throw new InvalidOperationException("Open the saved MultiplayerGameplay scene outside Play Mode.");
            var session=UnityEngine.Object.FindFirstObjectByType<ArchiveTopicSession>();
            if(session==null||session.stations==null||session.stations.Length!=10)throw new InvalidOperationException("Expected the existing ten-topic session.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var view=session.GetComponent<ArchiveRewardView>();if(view==null)view=session.gameObject.AddComponent<ArchiveRewardView>();
            view.session=session;view.chestPrefab=ChestAsset();
            view.glowMaterial=Glow();
            view.sparkMesh=AssetDatabase.LoadAssetAtPath<Mesh>(ArchiveTopicSetup.Folder+"/Art/Crystals/Restoration_Shard_0.asset");
            if(view.sparkMesh==null){var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);view.sparkMesh=temp.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(temp);}
            view.chestOpenSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Doors/Drs_Chest_Open_01.wav");
            view.formationSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Ball_Cast_01.wav");
            view.dartSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/Audio/Archive_Tranquilizer_Dry_Pneumatic_Shot.wav");
            EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static Material Glow()
        {
            string path=Folder+"/Reward_Rune_Gold.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("URP unlit shader is required.");
            material=new Material(shader){name="Reward_Rune_Gold",enableInstancing=true};material.SetColor("_BaseColor",new Color(1,.70f,.16f));AssetDatabase.CreateAsset(material,path);return material;
        }
        static GameObject ChestAsset()
        {
            string path=Folder+"/Archive_Solver_Reward_Chest.prefab";var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(existing!=null)return existing;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ChestSource);if(source==null)throw new FileNotFoundException("The existing dungeon chest is required.",ChestSource);
            var root=new GameObject("Archive_Solver_Reward_Chest");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(source);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform,false);model.name="Existing_Dungeon_Chest_Model";
                foreach(var t in model.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,0);
                var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP Lit shader is required.");
                foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats=renderer.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        var original=mats[i];string key=original==null?"Chest_Wood":original.name.Replace(" (Instance)","");
                        string materialPath=Folder+"/Chest_"+key.Replace('/','_').Replace('\\','_')+".mat";
                        var copy=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if(copy==null)
                        {
                            copy=new Material(shader){name="Chest_"+key,enableInstancing=true};
                            if(original!=null)
                            {
                                var texture=original.HasProperty("_BaseMap")?original.GetTexture("_BaseMap"):original.mainTexture;
                                if(texture!=null)copy.SetTexture("_BaseMap",texture);
                                var color=original.HasProperty("_BaseColor")?original.GetColor("_BaseColor"):original.HasProperty("_Color")?original.GetColor("_Color"):Color.white;
                                copy.SetColor("_BaseColor",color);copy.SetFloat("_Smoothness",.18f);
                            }
                            else copy.SetColor("_BaseColor",new Color(.42f,.23f,.10f));
                            AssetDatabase.CreateAsset(copy,materialPath);
                        }
                        mats[i]=copy;
                    }
                    renderer.sharedMaterials=mats;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
                }
                var renderers=model.GetComponentsInChildren<Renderer>(true);var bounds=renderers.First().bounds;
                foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                float scale=1.7f/Mathf.Max(.01f,bounds.size.x);model.transform.localScale*=scale;
                model.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
                foreach(var collider in model.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                // Reusable reward art is collision-free so it never blocks a chase or the Book's approach.
                return PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
