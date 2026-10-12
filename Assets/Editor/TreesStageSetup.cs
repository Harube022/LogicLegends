using System;
using System.Linq;
using LogicLegends.Trees;
using LogicLegends.Graph;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Authors Trees only; copies a disposable PRELIM scene to preserve prefab links and cross-root references.</summary>
public static class TreesStageSetup
{
    const string Folder="Assets/Prefabs/Trees";
    [MenuItem("Tools/Logic Legends/Build Trees Stage")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath("Assets/Scenes/Trees.unity");
        if(!scene.isLoaded)scene=EditorSceneManager.OpenScene("Assets/Scenes/Trees.unity",OpenSceneMode.Additive);
        if(scene.GetRootGameObjects().Any(g=>g.GetComponent<TreeStageManager>()!=null))throw new InvalidOperationException("Trees already configured; edit its catalog and assigned prefabs.");
        const string temp="Assets/Scenes/Trees_SourceCopy.unity";
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(temp)!=null)throw new InvalidOperationException("Temporary scene path already exists.");
        // Copy the serialized source rather than instantiate scene objects (which loses prefab connections).
        if(!AssetDatabase.CopyAsset("Assets/Scenes/PRELIM.unity",temp))throw new InvalidOperationException("Could not create source snapshot.");
        var source=EditorSceneManager.OpenScene(temp,OpenSceneMode.Additive);
        var names=new[]{"Main Camera","Directional Light","CameraPivot","GameInput","EventSystem","LevelSpawner","LevelMenuManager","MobileInputUI","Canvas 1","CM_ThirdPersonCam","Inventory_Managers","TeleportManager"};
        try
        {
            var roots=source.GetRootGameObjects().Where(g=>names.Contains(g.name)).ToArray();
            foreach(var old in scene.GetRootGameObjects().Where(g=>names.Contains(g.name)))UnityEngine.Object.DestroyImmediate(old);
            foreach(var root in roots)SceneManager.MoveGameObjectToScene(root,scene);
        }
        finally {EditorSceneManager.CloseScene(source,true);AssetDatabase.DeleteAsset(temp);}
        SceneManager.SetActiveScene(scene);
        Func<string,GameObject> find=name=>scene.GetRootGameObjects().First(g=>g.name==name);
        var canvas=find("Canvas 1");var safe=Child(canvas,"SafeArea");var gameplay=Child(canvas,"Gameplay_Interface");
        foreach(Transform child in canvas.transform)if(child!=safe&&child.name!="PausePanel")Active(child.gameObject,false);
        foreach(Transform child in safe)if(child!=gameplay)Active(child.gameObject,false);
        foreach(Transform child in gameplay)if(!new[]{"LookArea","Joystick","JumpButton","InteractButton","SettingsButton"}.Contains(child.name))Active(child.gameObject,false);
        var backing=gameplay.GetComponent<Image>();backing.color=Color.clear;backing.raycastTarget=false;PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
        Active(find("Inventory_Managers"),false);
        var pause=Child(canvas,"PausePanel");Active(pause.gameObject,false);
        var menu=find("LevelMenuManager").GetComponent<LevelMenu>();Set(menu,"pausePanel",pause.gameObject);
        Click(Child(canvas,"SettingsButton").GetComponent<Button>(),menu.OpenPause);
        Click(Child(canvas,"ResumeButton").GetComponent<Button>(),menu.ClosePause);
        Click(Child(canvas,"ReturnButton").GetComponent<Button>(),menu.ReturnToMainMenu);
        Click(Child(canvas,"QuitButton").GetComponent<Button>(),menu.QuitGame);
        foreach(var text in canvas.GetComponentsInChildren<TMP_Text>(true))if(text.font==null){text.font=StageUITheme.ReadingFont;PrefabUtility.RecordPrefabInstancePropertyModifications(text);}
        var pivot=find("CameraPivot").GetComponent<ThirdPersonCameraController>();pivot.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(pivot);
        var camera=find("Main Camera").GetComponent<Camera>();camera.clearFlags=CameraClearFlags.Skybox;
        var light=find("Directional Light").GetComponent<Light>();light.intensity=1.25f;light.transform.rotation=Quaternion.Euler(50,-35,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.69f,.65f);
        EnsureFolder(Folder);
        var stage=new GameObject("Trees Stage").AddComponent<TreeStageManager>();
        stage.boardFocus=stage.gameObject.AddComponent<TreeBoardFocus>();stage.boardFocus.outputCamera=camera;
        stage.ui=stage.gameObject.AddComponent<TreeUIController>();stage.ui.host=(RectTransform)canvas.transform;stage.ui.gameplayControls=gameplay.gameObject;
        ConfigureUIArtwork(stage.ui);
        stage.ui.parchment=Sprite("Assets/Interface_Sprites/customizationsh_shop_interface/customization_scroll.png");
        stage.ui.nodeCircle=Sprite("Assets/Assets/Interface_Sprites/gameplay_interface/joystick_circle_handle.png");
        stage.timer=stage.gameObject.AddComponent<GraphStageClock>();
        stage.gameOver=stage.gameObject.AddComponent<GameOverManager>();
        stage.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");stage.wrongSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/DAMAGETAKEN1.mp3");
        Set(stage.gameOver,"gameOverSound",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/WORLDBGM/GAMEOVER1.mp3"));
        var catalog=ScriptableObject.CreateInstance<TreeChallengeCatalog>();catalog.challenges=TreeChallengeCatalog.Defaults();AssetDatabase.CreateAsset(catalog,Folder+"/TreeChallenges.asset");stage.catalog=catalog;
        stage.spawnPoint=new GameObject("Trees Spawn").transform;stage.spawnPoint.position=new Vector3(0,.15f,0);
        Set(find("LevelSpawner").GetComponent<LevelSpawner>(),"spawnPoint",stage.spawnPoint);
        Set(find("LevelSpawner").GetComponent<LevelSpawner>(),"cmCamera",find("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineCamera>());
        var follow=find("CM_ThirdPersonCam").GetComponent<Unity.Cinemachine.CinemachineThirdPersonFollow>();follow.CameraDistance=10;PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
        Set(find("TeleportManager").GetComponent<TeleportManager>(),"fadeCanvasGroup",find("TeleportManager").GetComponentInChildren<CanvasGroup>(true));
        Set(find("TeleportManager").GetComponent<TeleportManager>(),"cameraScript",null);
        stage.bookStatue=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Book_Statue.prefab"),scene)).transform;
        stage.bookStatue.name="Trees Book Statue";stage.bookStatue.position=Vector3.zero;var bounds=BoundsOf(stage.bookStatue.gameObject);float scale=2.1f/Mathf.Max(.1f,bounds.size.y);stage.bookStatue.localScale*=scale;bounds=BoundsOf(stage.bookStatue.gameObject);stage.bookStatue.position+=new Vector3(-2,0,1)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);PrefabUtility.RecordPrefabInstancePropertyModifications(stage.bookStatue);
        var room=new GameObject("Trees Garden (temporary project assets)").transform;
        var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/CHEATCODE/WOODTEXTURE/Materials/Wood3.mat");
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Garden floor";floor.transform.SetParent(room);floor.transform.position=new Vector3(0,-.3f,4);floor.transform.localScale=new Vector3(28,.6f,28);floor.GetComponent<Renderer>().sharedMaterial=wood;
        AddBounds(room);
        var background=new GameObject("Original wood tree board",typeof(SpriteRenderer));background.transform.SetParent(room);background.GetComponent<SpriteRenderer>().sprite=Sprite("Assets/Interface_Sprites/settings_interface/settings_background.png");var boardBounds=background.GetComponent<SpriteRenderer>().bounds;float boardScale=12/boardBounds.size.x;background.transform.localScale=Vector3.one*boardScale;background.transform.position=new Vector3(0,4.6f,10.3f);
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name="Trees calm teal";mat.color=new Color(.035f,.085f,.11f);AssetDatabase.CreateAsset(mat,Folder+"/TreeBoard.mat");
        var inset=GameObject.CreatePrimitive(PrimitiveType.Cube);inset.name="Tree content surface";inset.transform.SetParent(room);inset.transform.position=new Vector3(0,4.6f,10.1f);inset.transform.localScale=new Vector3(9.8f,7,.15f);inset.GetComponent<Renderer>().sharedMaterial=mat;
        stage.board=new GameObject("Tree Board").AddComponent<TreeBoardController>();stage.board.transform.position=new Vector3(0,0,9.85f);
        stage.boardFocus.board=stage.board.transform;
        ConfigureBoardCamera(stage,camera);
        stage.board.nodePrefab=MakeNode(mat);stage.board.edgePrefab=MakeEdge();
        foreach(var n in catalog.challenges[0].nodes){var node=(TreeNode)PrefabUtility.InstantiatePrefab(stage.board.nodePrefab,stage.board.transform);node.id=n.id;node.label.text=n.id;node.transform.localPosition=TreeBoardController.Position(n);}
        foreach(var edge in catalog.challenges[0].initial)
        {var line=(LineRenderer)PrefabUtility.InstantiatePrefab(stage.board.edgePrefab,stage.board.transform);line.SetPosition(0,TreeBoardController.Position(catalog.challenges[0].nodes.First(n=>n.id==edge.source))+Vector3.back*.08f);line.SetPosition(1,TreeBoardController.Position(catalog.challenges[0].nodes.First(n=>n.id==edge.destination))+Vector3.back*.08f);}
        var list=EditorBuildSettings.scenes.ToList();if(!list.Any(s=>s.path==scene.path))list.Add(new EditorBuildSettingsScene(scene.path,true));EditorBuildSettings.scenes=list.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Trees configured: five challenges, connected PRELIM prefabs, original UI artwork, shared clock and covered teleport transitions.");
    }
    public static void ConfigureUIArtwork(TreeUIController ui)
    {
        ui.host=(RectTransform)ui.host.GetComponentInParent<Canvas>().transform;
        ui.hudObjective=Sprite("Assets/Art/GraphHUD/GraphObjectiveFrame.png");
        ui.hudStatus=Sprite("Assets/Art/GraphHUD/GraphStatusFrame.png");
        ui.hudMap=Sprite("Assets/Art/GraphHUD/GraphMapFrame.png");
        EditorUtility.SetDirty(ui);
    }
    public static void ConfigureBoardCamera(TreeStageManager stage,Camera output)
    {
        var focus=stage.boardFocus;
        focus.outputCamera=output;focus.board=stage.board.transform;
        if(focus.focusCamera==null)
        {
            var go=new GameObject("CM_TreesBoardCam");SceneManager.MoveGameObjectToScene(go,stage.gameObject.scene);
            focus.focusCamera=go.AddComponent<Unity.Cinemachine.CinemachineCamera>();
        }
        var camera=focus.focusCamera;
        camera.transform.SetPositionAndRotation(stage.board.transform.TransformPoint(new Vector3(0,4.25f,-3)),stage.board.transform.rotation);
        camera.Priority=new Unity.Cinemachine.PrioritySettings{Enabled=true,Value=100};
        var lens=Unity.Cinemachine.LensSettings.Default;
        lens.ModeOverride=Unity.Cinemachine.LensSettings.OverrideModes.Orthographic;
        lens.OrthographicSize=6.2f*Mathf.Abs(stage.board.transform.lossyScale.y);
        lens.NearClipPlane=.05f;lens.FarClipPlane=1000;camera.Lens=lens;
        camera.gameObject.SetActive(false);
        var brain=output.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        brain.LensModeOverride.Enabled=true;
        brain.LensModeOverride.DefaultMode=Unity.Cinemachine.LensSettings.OverrideModes.Perspective;
        PrefabUtility.RecordPrefabInstancePropertyModifications(brain);
        EditorUtility.SetDirty(focus);EditorUtility.SetDirty(camera);EditorUtility.SetDirty(brain);
    }
    static TreeNode MakeNode(Material material)
    {
        var go=new GameObject("Tree Node");var node=go.AddComponent<TreeNode>();
        var face=GameObject.CreatePrimitive(PrimitiveType.Cylinder);face.name="Node face";face.transform.SetParent(go.transform,false);face.transform.localRotation=Quaternion.Euler(90,0,0);face.transform.localScale=new Vector3(1.05f,.12f,1.05f);face.GetComponent<Renderer>().sharedMaterial=material;node.face=face.GetComponent<Renderer>();
        var label=new GameObject("Editable node label",typeof(TextMeshPro));label.transform.SetParent(go.transform,false);label.transform.localPosition=Vector3.back*.2f;node.label=label.GetComponent<TextMeshPro>();node.label.font=StageUITheme.HeadingFont;node.label.fontSize=8;node.label.color=StageUITheme.Ink;node.label.alignment=TextAlignmentOptions.Center;node.label.rectTransform.sizeDelta=new Vector2(1,1);node.label.text="A";
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/TreeNode.prefab").GetComponent<TreeNode>();UnityEngine.Object.DestroyImmediate(go);return prefab;
    }
    public static void AddBounds(Transform room)
    {
        if(room.Find("Garden boundary")!=null)return;
        var root=new GameObject("Garden boundary").transform;root.SetParent(room,false);
        for(int i=0;i<4;i++)
        {
            var wall=new GameObject("Boundary "+i,typeof(BoxCollider));wall.transform.SetParent(root,false);
            wall.transform.position=i<2?new Vector3(i==0?-14:14,4,4):new Vector3(0,4,i==2?-10:18);
            wall.GetComponent<BoxCollider>().size=i<2?new Vector3(.5f,8,28):new Vector3(28,8,.5f);
        }
    }
    static LineRenderer MakeEdge()
    {
        var go=new GameObject("Tree Connection");var line=go.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=2;line.startWidth=line.endWidth=.055f;line.numCapVertices=4;
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.color=new Color(.2f,.7f,.9f);AssetDatabase.CreateAsset(material,Folder+"/TreeConnection.mat");line.sharedMaterial=material;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Folder+"/TreeConnection.prefab").GetComponent<LineRenderer>();UnityEngine.Object.DestroyImmediate(go);return prefab;
    }
    static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
    static Sprite Sprite(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    static Transform Child(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    static void Active(GameObject go,bool active){go.SetActive(active);PrefabUtility.RecordPrefabInstancePropertyModifications(go);}
    static void Set(UnityEngine.Object target,string field,UnityEngine.Object value){var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(target);}
    static void Click(Button button,UnityEngine.Events.UnityAction callback){while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);UnityEventTools.AddPersistentListener(button.onClick,callback);PrefabUtility.RecordPrefabInstancePropertyModifications(button);}
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
}
