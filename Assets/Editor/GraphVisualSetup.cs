using System;
using System.Linq;
using LogicLegends.Graph;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Graph-only overrides. Never unpacks or modifies the shared Canvas 1 prefab.</summary>
public static class GraphVisualSetup
{
    const string CanvasPath = "Assets/Prefabs/PREFABS/WORLD_UI/Canvas 1.prefab";
    [MenuItem("Tools/Logic Legends/Apply Graph Asset Setup")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Graph.unity") throw new InvalidOperationException("Open Graph first.");
        var stage = scene.GetRootGameObjects().Select(g=>g.GetComponent<GraphStageManager>()).First(m=>m!=null);
        var camera=scene.GetRootGameObjects().First(g=>g.name=="Main Camera").GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.Skybox;camera.backgroundColor=Color.clear;
        EditorUtility.SetDirty(camera);
        var oldCanvas = scene.GetRootGameObjects().First(g=>g.name=="Canvas 1");
        ConnectCanvas(oldCanvas, stage);
        ReplaceVertex(stage.vertexPrefab);
        ReplaceEdge(stage.edgePrefab);
        var legacy = stage.GetComponent<StageCompleteManager>();
        if (legacy != null) Undo.DestroyObjectImmediate(legacy);
        stage.completion = stage.GetComponent<GraphCompletion>();
        if (stage.completion == null) stage.completion = Undo.AddComponent<GraphCompletion>(stage.gameObject);
        EditorUtility.SetDirty(stage);
        foreach (Transform t in stage.arena.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(t.gameObject);
        // Refresh only the preview geometry. Scenery, catalog and book placement are preserved.
        var challenge = GraphGeometry.Layout(stage.catalog.challenges[0]);
        foreach(var v in challenge.vertices)
        { var view=(GraphVertex)PrefabUtility.InstantiatePrefab(stage.vertexPrefab,stage.arena); view.Configure(v); }
        foreach(var e in challenge.edges)
        { var view=(GraphEdge)PrefabUtility.InstantiatePrefab(stage.edgePrefab,stage.arena); view.Configure(e,challenge.Vertex(e.source).position,challenge.Vertex(e.destination).position); }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Graph now uses connected Canvas 1, Bilog vertices, Bridge edges and independent completion.");
    }
    public static GameObject ConnectCanvas(GameObject oldCanvas, GraphStageManager stage)
    {
        GameObject canvas = oldCanvas;
        if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldCanvas) != CanvasPath)
        {
            canvas=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath),oldCanvas.scene);
            canvas.name="Canvas 1"; Undo.RegisterCreatedObjectUndo(canvas,"Connect Graph canvas");
            EditorUtility.CopySerialized(oldCanvas.GetComponent<CanvasScaler>(),canvas.GetComponent<CanvasScaler>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.GetComponent<CanvasScaler>());
            foreach(var name in new[]{"Gameplay_Interface","LookArea","Joystick","JumpButton","InteractButton","SettingsButton"})
                CopyRect(Find(oldCanvas,name) as RectTransform,Find(canvas,name) as RectTransform);
        }
        var safe=Find(canvas,"SafeArea"); var gameplay=Find(canvas,"Gameplay_Interface");
        // Keep every prefab object; unused legacy systems are inactive instance overrides.
        foreach(Transform t in canvas.transform) if(t!=safe && t.name!="PausePanel") OverrideActive(t.gameObject,false);
        foreach(Transform t in safe) if(t!=gameplay) OverrideActive(t.gameObject,false);
        foreach(Transform t in gameplay)
            if(!new[]{"LookArea","Joystick","JumpButton","InteractButton","SettingsButton"}.Contains(t.name)) OverrideActive(t.gameObject,false);
        var backing=gameplay.GetComponent<Image>(); backing.color=Color.clear; backing.raycastTarget=false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(backing);
        var menu=UnityEngine.Object.FindFirstObjectByType<LevelMenu>();
        var pause=Find(canvas,"PausePanel"); OverrideActive(pause.gameObject,false);
        var serializedMenu=new SerializedObject(menu);serializedMenu.FindProperty("pausePanel").objectReferenceValue=pause.gameObject;serializedMenu.ApplyModifiedProperties();
        SetClick(Find(canvas,"SettingsButton").GetComponent<Button>(),menu.OpenPause);
        SetClick(Find(canvas,"ResumeButton").GetComponent<Button>(),menu.ClosePause);
        SetClick(Find(canvas,"ReturnButton").GetComponent<Button>(),menu.ReturnToMainMenu);
        SetClick(Find(canvas,"QuitButton").GetComponent<Button>(),menu.QuitGame);
        stage.ui.host=(RectTransform)safe;stage.ui.gameplayControls=gameplay.gameObject;
        stage.ui.settingsButton=(RectTransform)Find(canvas,"SettingsButton");
        stage.ui.pointer=AssetDatabase.LoadAllAssetsAtPath("Assets/Assets/Interface_Sprites/Tutorial/Pointer.png").OfType<Sprite>().First();
        EditorUtility.SetDirty(stage.ui);
        if(canvas!=oldCanvas) Undo.DestroyObjectImmediate(oldCanvas);
        return canvas;
    }
    static void SetClick(Button button,UnityEngine.Events.UnityAction action)
    { while(button.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(button.onClick,0);UnityEventTools.AddPersistentListener(button.onClick,action);PrefabUtility.RecordPrefabInstancePropertyModifications(button); }
    static void OverrideActive(GameObject go,bool active)
    { go.SetActive(active);PrefabUtility.RecordPrefabInstancePropertyModifications(go); }
    static Transform Find(GameObject go,string name)=>go.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
    static void CopyRect(RectTransform source,RectTransform target)
    {
        target.anchorMin=source.anchorMin;target.anchorMax=source.anchorMax;target.pivot=source.pivot;
        target.sizeDelta=source.sizeDelta;target.anchoredPosition=source.anchoredPosition;
        target.localRotation=source.localRotation;target.localScale=source.localScale;
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static void ReplaceVertex(GraphVertex prefab)
    {
        string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var platform=Find(root,"Vertex platform");platform.GetComponent<Renderer>().enabled=false;
            foreach(var col in platform.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(col);
            var previous=root.transform.Find("Bilog artwork");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/Graph/Bilog.fbx"),root.transform);
            model.name="Bilog artwork";var bounds=BoundsOf(model);float scale=7f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.transform.localScale=Vector3.one*scale;
            model.transform.localPosition=new Vector3(-bounds.center.x*scale,-.05f-bounds.max.y*scale,-bounds.center.z*scale);
            foreach(var col in model.GetComponentsInChildren<Collider>())col.enabled=true;
            var label=root.GetComponent<GraphVertex>().label;
            label.transform.localPosition=Vector3.up*(GraphGeometry.LabelHeight/GraphGeometry.VertexScale);
            label.transform.localScale=Vector3.one*(GraphGeometry.LabelScale/GraphGeometry.VertexScale);
            label.transform.localRotation=Quaternion.identity;
            if(label.GetComponent<FaceCamera>()==null)label.gameObject.AddComponent<FaceCamera>();
            label.rectTransform.sizeDelta=new Vector2(3.2f,3f);
            label.enableAutoSizing=true;label.fontSizeMin=10;label.fontSizeMax=14;
            label.overflowMode=TMPro.TextOverflowModes.Overflow;
            label.margin=Vector4.zero;
            // A Graph-local TMP overlay keeps the character/bridge from cutting through a label.
            const string labelMaterialPath="Assets/Prefabs/Graph/GraphVertexLabel.mat";
            var labelMaterial=AssetDatabase.LoadAssetAtPath<Material>(labelMaterialPath);
            if(labelMaterial==null)
            {
                labelMaterial=new Material(label.fontSharedMaterial);
                labelMaterial.shader=Shader.Find("TextMeshPro/Distance Field Overlay");
                labelMaterial.name="Graph vertex label";
                AssetDatabase.CreateAsset(labelMaterial,labelMaterialPath);
            }
            label.fontSharedMaterial=labelMaterial;
            ConfigureAnswerArea(root.GetComponent<GraphVertex>());
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static void ConfigureAnswerArea(GraphVertex vertex)
    {
        if (vertex.answerArea == null)
            vertex.answerArea = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lana Studio/Hyper Casual FX/Prefabs/Area/Area_circles_blue.prefab"), vertex.transform);
        var area = vertex.answerArea;
        area.name = "Answer area circles";
        area.transform.localPosition = new Vector3(0, .03f, 0);
        area.transform.localRotation = Quaternion.identity;
        // Fit the original ten-unit circle inside the white floor, clear of the dark rim.
        area.transform.localScale = Vector3.one * .5f;
        foreach (var particles in area.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main; main.playOnAwake = false;
            if (particles.name == "points_large" || particles.name == "points_small") particles.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles.gameObject);
        }
        area.SetActive(false);
        PrefabUtility.RecordPrefabInstancePropertyModifications(area.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(area);
    }
    static void ReplaceEdge(GraphEdge prefab)
    {
        string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var edge=root.GetComponent<GraphEdge>();edge.bridge.GetComponent<Renderer>().enabled=false;
            foreach(var col in edge.bridge.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(col);
            foreach(Transform t in edge.bridge.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            var old=root.transform.Find("Bridge artwork");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var holder=new GameObject("Bridge artwork");holder.transform.SetParent(root.transform,false);edge.artwork=holder.transform;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/Graph/Bridge.fbx"),holder.transform);
            // The source bridge runs along X. Rotate it to Graph's forward Z axis.
            model.transform.localRotation=Quaternion.Euler(0,90,0);var bounds=BoundsOf(model);
            float cross=4.4f/bounds.size.x;
            // Cross-section uses one uniform scale; only its modular length changes with the graph.
            // Local scaling happens before rotation: source X is the modular length.
            model.transform.localScale=new Vector3(1f/bounds.size.z,cross,cross);
            model.transform.localPosition=new Vector3(-bounds.center.x*cross,-.05f,-bounds.center.z/bounds.size.z);
            foreach(var col in model.GetComponentsInChildren<Collider>())col.enabled=true;
            var highlight=model.GetComponentsInChildren<Renderer>().First(r=>r.name=="Highlights");
            const string matPath="Assets/Prefabs/Graph/GraphPathLight.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(material==null)
            {
                material=new Material(highlight.sharedMaterial);material.name="Graph path light";
                material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",GraphEdge.AvailableColor*1.5f);
                material.SetColor("_BaseColor",GraphEdge.AvailableColor);AssetDatabase.CreateAsset(material,matPath);
            }
            highlight.sharedMaterial=material;edge.pathLighting=new[]{highlight};
            // Two small shadowless lights keep the effect inexpensive on mobile.
            edge.pathLights=new Light[2];
            for(int i=0;i<2;i++)
            {var light=new GameObject("Path light "+i).AddComponent<Light>();light.transform.SetParent(holder.transform,false);light.transform.localPosition=new Vector3(0,.5f,i==0?-.3f:.3f);light.range=3;light.intensity=.8f;light.shadows=LightShadows.None;edge.pathLights[i]=light;}
            edge.SetWalked(false);PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static Bounds BoundsOf(GameObject go)
    {var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;}
}
