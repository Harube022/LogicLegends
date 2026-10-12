using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors connected variants and the temporary multiplayer stage without unpacking anything.</summary>
[InitializeOnLoad]
public static class MultiplayerStageSetup
{
    public const string ScenePath = "Assets/Scenes/MultiplayerStage.unity";
    private const string World = "Assets/Prefabs/PREFABS/WORLD_UI/";
    private const string RequestPath = "Temp/MultiplayerStageSetup.request";
    static MultiplayerStageSetup()
    {
        EditorApplication.update += RequestedSetup;
        // ParrelSync shares Assets, but each editor owns its copied build settings.
        EditorApplication.delayCall += EnsureBuildScene;
    }
    private static void EnsureBuildScene()
    {
        if (!File.Exists(ScenePath)) return;
        var scenes=EditorBuildSettings.scenes.ToList();
        int index=scenes.FindIndex(s=>s.path==ScenePath);
        if(index>=0&&scenes[index].enabled)return;
        if(index<0)scenes.Add(new EditorBuildSettingsScene(ScenePath,true));else scenes[index]=new EditorBuildSettingsScene(ScenePath,true);
        EditorBuildSettings.scenes=scenes.ToArray();
    }
    private static void RequestedSetup()
    {
        if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        // Clone assets are shared; only the editor whose Temp folder contains the explicit request may author them.
        string request = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        try
        {
            if (request == "camera") FixStageCamera(); else Build();
            File.WriteAllText("Temp/MultiplayerStageSetup.result", "PASS\n" + Validate());
        }
        catch (Exception e) { File.WriteAllText("Temp/MultiplayerStageSetup.result", "FAIL\n" + e); Debug.LogException(e); }
    }
    [MenuItem("Logic Legends/Multiplayer/Fix MultiplayerStage Camera")]
    public static void FixStageCamera()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before changing the saved camera.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        ConfigureStageCamera(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
    private static void ConfigureStageCamera(Scene scene)
    {
        var camera = FindRoot(scene, "Main Camera").GetComponent<Camera>();
        var brain = camera.GetComponent<CinemachineBrain>();
        var cm = FindRoot(scene, "CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        Undo.RecordObjects(new UnityEngine.Object[] { camera, brain, cm }, "Set Multiplayer Camera Perspective And Skybox");
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.orthographic = false;
        brain.LensModeOverride.Enabled = true;
        brain.LensModeOverride.DefaultMode = LensSettings.OverrideModes.Perspective;
        var lens = cm.Lens;
        lens.ModeOverride = LensSettings.OverrideModes.Perspective;
        cm.Lens = lens;
        Record(camera); Record(brain); Record(cm);
    }
    [MenuItem("Logic Legends/Multiplayer/Build MultiplayerStage And Character Variants")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building the stage.");
        MultiplayerUISetup.ConfigureArtwork();
        MakeVariant("Player", "Multiplayer_Male");
        MakeVariant("Player_Female", "Multiplayer_Female");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        Scene previous = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
        try
        {
            var cameraObject = FindRoot(scene, "Main Camera") ?? NewRoot(scene, "Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = Ensure<Camera>(cameraObject);
            camera.nearClipPlane = .05f; camera.farClipPlane = 1000f;
            camera.cullingMask = ~0;
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            var brain = Ensure<CinemachineBrain>(cameraObject);
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var lightObject = FindRoot(scene, "Directional Light") ?? NewRoot(scene, "Directional Light");
            var light = Ensure<Light>(lightObject);
            light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45,-25,0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.65f,.73f,.82f);
            RenderSettings.ambientEquatorColor = new Color(.42f,.49f,.50f);
            RenderSettings.ambientGroundColor = new Color(.25f,.30f,.25f);
            RenderSettings.sun = light;

            Instantiate(scene, World + "GameInput.prefab");
            var canvas = Instantiate(scene, World + "Canvas 1.prefab");
            var safe = Child(canvas, "SafeArea"); var gameplay = Child(canvas, "Gameplay_Interface");
            foreach (Transform child in canvas.transform) if (child != safe && child.name != "PausePanel") Active(child.gameObject, false);
            foreach (Transform child in safe) if (child != gameplay) Active(child.gameObject, false);
            foreach (Transform child in gameplay) if (!new[] { "LookArea", "Joystick", "JumpButton", "InteractButton", "SettingsButton" }.Contains(child.name)) Active(child.gameObject, false);
            Active(safe.gameObject, true); Active(gameplay.gameObject, true);
            var backing = gameplay.GetComponent<Image>(); if (backing != null) { backing.color = Color.clear; backing.raycastTarget = false; Record(backing); }
            var mobile = Instantiate(scene, World + "MobileInputUI.prefab").GetComponent<MobileInputUI>();
            var joystick = new SerializedObject(canvas.GetComponentInChildren<MobileInputUI>(true));
            Set(mobile,"joystickBackground",joystick.FindProperty("joystickBackground").objectReferenceValue);
            Set(mobile,"joystickHandle",joystick.FindProperty("joystickHandle").objectReferenceValue);
            Click(Child(canvas,"JumpButton").GetComponent<Button>(), mobile.Jump);
            Click(Child(canvas,"InteractButton").GetComponent<Button>(), mobile.Interact);
            var menu = Instantiate(scene, World + "LevelMenuManager.prefab").GetComponent<LevelMenu>();
            var pause = Child(canvas,"PausePanel"); Active(pause.gameObject,false); Set(menu,"pausePanel",pause.gameObject);
            Click(Child(canvas,"SettingsButton").GetComponent<Button>(),menu.OpenPause);
            Click(Child(canvas,"ResumeButton").GetComponent<Button>(),menu.ClosePause);
            Click(Child(canvas,"ReturnButton").GetComponent<Button>(),menu.ReturnToMainMenu);
            Click(Child(canvas,"QuitButton").GetComponent<Button>(),menu.QuitGame);

            var cm = Instantiate(scene,World+"CM_ThirdPersonCam.prefab");
            cm.transform.SetPositionAndRotation(new Vector3(0,7,-12),Quaternion.Euler(18,0,0)); Record(cm.transform);
            ConfigureStageCamera(scene);
            var teleport = Instantiate(scene,World+"TeleportManager.prefab").GetComponent<TeleportManager>();
            // The current covered transition creates its own topmost fade surface at runtime.
            Set(teleport,"fadeCanvasGroup",(UnityEngine.Object)null); Set(teleport,"cameraScript",(UnityEngine.Object)null);
            var eventObject = FindRoot(scene,"EventSystem") ?? NewRoot(scene,"EventSystem");
            if (eventObject.GetComponent<EventSystem>() == null) eventObject.AddComponent<EventSystem>();
            if (eventObject.GetComponent<InputSystemUIInputModule>() == null) eventObject.AddComponent<InputSystemUIInputModule>();

            var environment = FindRoot(scene,"Temporary Spawn Garden") ?? NewRoot(scene,"Temporary Spawn Garden");
            if (environment.transform.childCount == 0)
            {
                var ground = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHEATCODE/SimpleNaturePack/Prefabs/Ground_01.prefab"),environment.transform);
                ground.name = "Temporary Grass Platform"; ground.transform.localPosition = Vector3.zero;
                var bounds = ground.GetComponent<MeshFilter>().sharedMesh.bounds;
                ground.transform.localScale = new Vector3(36f/Mathf.Max(.01f,bounds.size.x),1f,36f/Mathf.Max(.01f,bounds.size.z));
                ground.transform.position = new Vector3(-bounds.center.x*ground.transform.localScale.x,-bounds.max.y,-bounds.center.z*ground.transform.localScale.z);
                ground.tag = "Grass"; Record(ground); Record(ground.transform);
                var treeAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CHEATCODE/SimpleNaturePack/Prefabs/Tree_01.prefab");
                for (int i=0;i<4;i++)
                {
                    var tree=(GameObject)PrefabUtility.InstantiatePrefab(treeAsset,environment.transform);
                    tree.transform.position=new Vector3(i%2==0?-14:14,0,i<2?-12:12); Record(tree.transform);
                }
                // Invisible rails keep temporary testers safely on the platform.
                for(int i=0;i<4;i++)
                {
                    var rail = new GameObject("Platform Boundary "+i,typeof(BoxCollider)); rail.transform.SetParent(environment.transform,false);
                    rail.transform.position=i<2?new Vector3(i==0?-17:17,5,0):new Vector3(0,5,i==2?-17:17);
                    rail.GetComponent<BoxCollider>().size=i<2?new Vector3(.5f,12,35):new Vector3(35,12,.5f);
                }
            }
            ResizePlatform(environment);
            AddTemporaryDesigns(environment);
            MultiplayerGameChat.Build(safe);
            var spawn = FindRoot(scene,"Multiplayer Spawn Point") ?? NewRoot(scene,"Multiplayer Spawn Point");
            spawn.transform.SetPositionAndRotation(new Vector3(0,2,0),Quaternion.identity);
            var spawner=Instantiate(scene,World+"NetworkSpawner.prefab").GetComponent<PlayerSpawner>();
            Set(spawner,"malePrefabName","Multiplayer_Male"); Set(spawner,"femalePrefabName","Multiplayer_Female"); Set(spawner,"spawnPoint",spawn.transform);
            var scenes=EditorBuildSettings.scenes.ToList(); int index=scenes.FindIndex(s=>s.path==ScenePath);
            if(index<0) scenes.Add(new EditorBuildSettingsScene(ScenePath,true)); else scenes[index]=new EditorBuildSettingsScene(ScenePath,true);
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            UpdateMainMenu(); AssetDatabase.SaveAssets();
            Debug.Log("MultiplayerStage ready: connected player variants, owned network spawns, shared UI/camera/teleport prefabs and temporary grass platform.");
        }
        finally { if(previous.isLoaded) SceneManager.SetActiveScene(previous); }
    }
    private static void ResizePlatform(GameObject environment)
    {
        var ground=environment.transform.Find("Temporary Grass Platform");
        var bounds=ground.GetComponent<MeshFilter>().sharedMesh.bounds;
        ground.localScale=new Vector3(100f/Mathf.Max(.01f,bounds.size.x),1f,100f/Mathf.Max(.01f,bounds.size.z));
        ground.position=new Vector3(-bounds.center.x*ground.localScale.x,-bounds.max.y,-bounds.center.z*ground.localScale.z);Record(ground);
        for(int i=0;i<4;i++)
        {
            var rail=environment.transform.Find("Platform Boundary "+i);
            rail.position=i<2?new Vector3(i==0?-49:49,5,0):new Vector3(0,5,i==2?-49:49);
            rail.GetComponent<BoxCollider>().size=i<2?new Vector3(.5f,12,100):new Vector3(100,12,.5f);
        }
        var originalTrees=environment.transform.Cast<Transform>().Where(t=>t.name=="Tree_01").ToArray();
        for(int i=0;i<originalTrees.Length;i++){originalTrees[i].position=new Vector3(i%2==0?-40:40,0,i<2?-35:35);Record(originalTrees[i]);}
    }
    private static void AddTemporaryDesigns(GameObject environment)
    {
        var designs=environment.transform.Find("Temporary Forest Meeting Area");
        if(designs==null){designs=new GameObject("Temporary Forest Meeting Area").transform;designs.SetParent(environment.transform,false);}
        const string nature="Assets/CHEATCODE/SimpleNaturePack/Prefabs/";
        // Keep the central eight-unit clearing free for the four spawn slots.
        for(int i=0;i<24;i++)
        {
            float angle=i*Mathf.PI/12;var p=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*12f;
            var stone=Place(nature+"Rock_05.prefab",designs,p,1.8f,i*15f,"Clearing Path Stone "+i);
            stone.transform.localScale=new Vector3(stone.transform.localScale.x,stone.transform.localScale.y*.18f,stone.transform.localScale.z);Record(stone.transform);
            GroundAt(stone,p);
        }
        for(int i=0;i<20;i++)
        {
            float angle=i*Mathf.PI/10;float radius=i%2==0?35f:42f;
            Place(nature+"Tree_0"+(i%3+1)+".prefab",designs,new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius),12f+i%4, i*47f,"Border Tree "+i);
        }
        for(int i=0;i<12;i++)
        {
            float angle=(i+.5f)*Mathf.PI/6;var p=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*25f;
            Place(nature+"Bush_0"+(i%3+1)+".prefab",designs,p,3f,i*31f,"Garden Bush "+i);
            Place(nature+"Flowers_0"+(i%2+1)+".prefab",designs,p+new Vector3(2,0,1),1.6f,i*15f,"Wildflowers "+i);
            Place(nature+"Rock_0"+(i%4+1)+".prefab",designs,p*1.3f,2.8f,i*37f,"Forest Rock "+i);
        }
        for(int i=0;i<4;i++)
        {
            float angle=i*Mathf.PI/2;var p=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*16f;
            Place("Assets/Prefabs/PREFABS/BENCH.prefab",designs,p,5f,i*90f+180f,"Rest Bench "+i);
            var lamp=Place("Assets/Prefabs/STREETLAMP2 Variant.prefab",designs,new Vector3(i%2==0?-10:10,0,i<2?-10:10),7f,i*90,"Clearing Lamp "+i);
            if(lamp.GetComponentInChildren<Light>()==null)
            {
                var glow=new GameObject("Warm Lamp Glow",typeof(Light));glow.transform.SetParent(lamp.transform,false);
                glow.transform.position=lamp.transform.position+Vector3.up*5.5f;
                var light=glow.GetComponent<Light>();light.type=LightType.Point;light.color=new Color(1f,.75f,.4f);light.intensity=1.4f;light.range=12;light.shadows=LightShadows.None;
            }
            var warmGlow=lamp.transform.Find("Warm Lamp Glow");
            if(warmGlow!=null)warmGlow.position=new Vector3(i%2==0?-10:10,5.5f,i<2?-10:10);
        }
        Place("Assets/Prefabs/Book_Statue.prefab",designs,new Vector3(22,0,22),8f,225,"Temporary Book Landmark");
        for(int i=0;i<6;i++)
        {
            var stone=Place(nature+"Rock_05.prefab",designs,new Vector3(10+i*1.7f,0,10+i*1.7f),2f,45,"Book Trail Stone "+i);
            stone.transform.localScale=new Vector3(stone.transform.localScale.x,stone.transform.localScale.y*.18f,stone.transform.localScale.z);Record(stone.transform);
            GroundAt(stone,new Vector3(10+i*1.7f,0,10+i*1.7f));
        }
    }
    private static GameObject Place(string path,Transform parent,Vector3 position,float size,float yaw,string name)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new Exception("Missing decoration "+path);
        var existing=parent.Find(name);var go=existing!=null?existing.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);go.name=name;
        // Imported props carry axis corrections and scale ratios in their source prefab.
        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,yaw,0)*asset.transform.rotation);go.transform.localScale=asset.transform.localScale;
        var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("Decoration has no visible renderer: "+path);
        Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        go.transform.localScale=asset.transform.localScale*(size/Mathf.Max(.01f,Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z))));
        GroundAt(go,position);
        Record(go);Record(go.transform);return go;
    }
    private static void GroundAt(GameObject go,Vector3 position)
    {
        var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
        go.transform.position+=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);Record(go.transform);
    }
    private static void MakeVariant(string sourceName,string targetName)
    {
        string sourcePath="Assets/Prefabs/PREFABS/CHARACTERS/"+sourceName+".prefab";
        string targetPath="Assets/Resources/"+targetName+".prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if(source==null) throw new InvalidOperationException("Missing source character "+sourcePath);
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,preview); instance.name=targetName;
            var view=Ensure<PhotonView>(instance);
            var sync=Ensure<MultiplayerAvatarSync>(instance);
            view.Synchronization=ViewSynchronization.UnreliableOnChange;
            view.observableSearch=PhotonView.ObservableSearch.Manual;
            view.ObservedComponents=new List<Component>{sync};
            if(PrefabUtility.SaveAsPrefabAsset(instance,targetPath)==null)throw new InvalidOperationException("Could not save variant "+targetPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
    private static void UpdateMainMenu()
    {
        const string path="Assets/Scenes/Main Menu.unity";
        var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            foreach(var menu in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MultiplayerMenuManager>(true))) Set(menu,"multiplayerScene","MultiplayerStage");
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        finally { if(opened)EditorSceneManager.CloseScene(scene,true); }
    }
    public static string Validate()
    {
        var lines=new List<string>();
        foreach(var name in new[]{"Multiplayer_Male","Multiplayer_Female"})
        {
            var prefab=Resources.Load<GameObject>(name);
            if(PrefabUtility.GetPrefabAssetType(prefab)!=PrefabAssetType.Variant)throw new Exception(name+" is not a variant.");
            var source=PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            lines.Add(name+" inherits "+AssetDatabase.GetAssetPath(source));
            if(prefab.GetComponent<Player>()==null||prefab.GetComponent<PlayerEquipmentLoader>()==null||prefab.GetComponent<PhotonView>().ObservedComponents.OfType<MultiplayerAvatarSync>().Count()!=1)throw new Exception(name+" network configuration is incomplete.");
            if(prefab.GetComponentsInChildren<CameraTargetController>(true).Length!=1)throw new Exception("Missing inherited camera target.");
        }
        var scene=SceneManager.GetSceneByPath(ScenePath);
        foreach(var name in new[]{"Canvas 1","GameInput","MobileInputUI","TeleportManager","CM_ThirdPersonCam","NetworkSpawner","LevelMenuManager"})
        {
            var root=FindRoot(scene,name);
            if(root==null||!PrefabUtility.IsPartOfPrefabInstance(root))throw new Exception(name+" lost its prefab connection.");
            lines.Add(name+" linked to "+PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root));
        }
        var outputCamera=FindRoot(scene,"Main Camera").GetComponent<Camera>();
        var brain=outputCamera.GetComponent<CinemachineBrain>();
        var cm=FindRoot(scene,"CM_ThirdPersonCam").GetComponent<CinemachineCamera>();
        if(outputCamera.orthographic||outputCamera.clearFlags!=CameraClearFlags.Skybox||!brain.LensModeOverride.Enabled||brain.LensModeOverride.DefaultMode!=LensSettings.OverrideModes.Perspective||cm.Lens.ModeOverride!=LensSettings.OverrideModes.Perspective)
            throw new Exception("Multiplayer camera must use perspective projection and the scene skybox.");
        lines.Add("Main Camera and linked CM_ThirdPersonCam use perspective; background uses the existing scene skybox.");
        if(!EditorBuildSettings.scenes.Any(s=>s.path==ScenePath&&s.enabled))throw new Exception("Scene is not enabled in build settings.");
        var environment=FindRoot(scene,"Temporary Spawn Garden");
        var platform=environment.transform.Find("Temporary Grass Platform").GetComponent<Renderer>().bounds;
        if(Mathf.Abs(platform.size.x-100f)>.1f||Mathf.Abs(platform.size.z-100f)>.1f)throw new Exception("Platform must be 100 by 100 units.");
        var designs=environment.transform.Find("Temporary Forest Meeting Area");
        if(designs==null||designs.childCount<95)throw new Exception("Temporary forest decorations are incomplete.");
        foreach(Transform decoration in designs)
        {
            if(!PrefabUtility.IsPartOfPrefabInstance(decoration.gameObject))throw new Exception("Decoration lost its prefab connection: "+decoration.name);
            var renderers=decoration.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
            if(Mathf.Abs(bounds.min.y)>.02f)throw new Exception("Decoration is not grounded: "+decoration.name);
        }
        lines.Add("100 x 100 platform; "+designs.childCount+" linked, grounded decorations; no unpacked prefab instances.");
        lines.Add("MultiplayerStage enabled in build settings; Main Menu destination updated.");
        return string.Join("\n",lines);
    }
    private static T Ensure<T>(GameObject go) where T : Component { var component=go.GetComponent<T>(); return component!=null?component:go.AddComponent<T>(); }
    private static GameObject FindRoot(Scene scene,string name)=>scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);
    private static GameObject NewRoot(Scene scene,string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
    private static GameObject Instantiate(Scene scene,string path)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(asset==null)throw new Exception("Missing prefab "+path);
        var go=FindRoot(scene,asset.name)??(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
        if(!(go.transform is RectTransform)){go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Record(go.transform);}return go;
    }
    private static Transform Child(GameObject root,string name)=>root.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    private static void Active(GameObject go,bool active){go.SetActive(active);Record(go);}
    private static void Record(UnityEngine.Object obj){EditorUtility.SetDirty(obj);PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();Record(target);}
    private static void Set(UnityEngine.Object target,string field,string value){var so=new SerializedObject(target);so.FindProperty(field).stringValue=value;so.ApplyModifiedPropertiesWithoutUndo();Record(target);}
    private static void Click(Button button,UnityAction callback)
    {
        var so=new SerializedObject(button);so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").ClearArray();so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddPersistentListener(button.onClick,callback);Record(button);
    }
}
