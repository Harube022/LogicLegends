using System;
using System.Linq;
using LogicLegends.Graph;
using LogicLegends.Induction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class InductionStageSetup
{
    const string Root = "Assets/FINAL_ASSETS/MAPS/REMADE/Induction";
    const string Boards = Root + "/InductionBoards";
    const string TabletPath = Root + "/FormulaTablet.prefab";
    [MenuItem("Tools/Logic Legends/Setup Induction Tower Gameplay")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Induction.unity") throw new InvalidOperationException("Open Induction first.");
        if (Object.FindFirstObjectByType<InductionStageManager>() != null) throw new InvalidOperationException("The Induction stage is already configured.");
        var roots = scene.GetRootGameObjects();
        var stage = roots.Single(g=>g.name=="Stage");
        var book = roots.Single(g=>g.name=="Book_Statue");
        var stations = new[] {"BaseCase","InductiveHypothesis","InductiveStep"}.Select(name=>roots.Single(g=>g.name==name)).ToArray();
        // Bounds, rather than imported pivots, preserve the authored station layout.
        foreach(var station in stations) foreach(var mesh in station.GetComponentsInChildren<MeshFilter>())
        {
            if(mesh.GetComponent<Collider>()==null) { var collider=Undo.AddComponent<MeshCollider>(mesh.gameObject);collider.sharedMesh=mesh.sharedMesh; }
        }
        var board=Instance(Boards+"/Prefabs/InformationBoard.prefab","Information Board",new Vector3(-120,28.03f,265),Quaternion.Euler(0,180,0),Vector3.one*2.2f);
        var rack=Instance(Boards+"/Prefabs/FormulaScrollsStand.prefab","Formula Scrolls",new Vector3(-80,28.03f,263),Quaternion.Euler(0,180,0),Vector3.one*2);
        PhysicsBox(book,"Induction Book Collision",BoundsOf(book));
        PhysicsBox(board,"Information Board Collision",BoundsOf(board));
        PhysicsBox(rack,"Formula Scrolls Collision",BoundsOf(rack));
        var managerObject=new GameObject("Induction Stage");Undo.RegisterCreatedObjectUndo(managerObject,"Setup Induction gameplay");
        var manager=managerObject.AddComponent<InductionStageManager>();manager.timer=managerObject.AddComponent<GraphStageClock>();
        manager.completion=managerObject.AddComponent<GraphCompletion>();manager.completion.stageNumber=6;
        manager.ui=managerObject.AddComponent<InductionUI>();manager.book=book.transform;manager.informationBoard=board.transform;manager.scrollStand=rack.transform;
        manager.boardView=managerObject.AddComponent<InductionBoardView>();
        manager.help=managerObject.AddComponent<InductionHelpUI>();
        manager.altarIndicators=managerObject.AddComponent<InductionAltarIndicators>();
        manager.altarIndicators.arrowShader=Shader.Find("Universal Render Pipeline/Unlit");
        manager.onboarding=managerObject.AddComponent<InductionControlsOnboarding>();
        manager.stations=stations.Select(s=>s.transform).ToArray();
        manager.uiHost=roots.Single(g=>g.name=="Canvas 1").GetComponentsInChildren<Transform>(true).Single(t=>t.name=="SafeArea");
        manager.boardTitle=board.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="ChallengeTitleLabel");
        manager.boardFormula=board.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="ChallengeFormulaLabel");
        manager.boardInstructions=board.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="ChallengeInstructionsLabel");
        manager.bookPrompt=WorldText("Induction Book Prompt",managerObject.transform,BoundsOf(book).max+new Vector3(-BoundsOf(book).extents.x,.6f,-BoundsOf(book).extents.z),"Interact to start Challenge 1",new Vector2(6,1),4);
        string[] prefixes={"Base:","Hypothesis:","Step:"};var slots=new System.Collections.Generic.List<InductionSlot>();
        for(int stationIndex=0;stationIndex<3;stationIndex++)
        {
            var bounds=BoundsOf(stations[stationIndex]);
            var title=WorldText("Station Label",stations[stationIndex].transform,new Vector3(bounds.center.x,bounds.max.y+.7f,bounds.center.z),new[]{"BASE CASE","INDUCTIVE HYPOTHESIS","INDUCTIVE STEP"}[stationIndex],new Vector2(6,1),4);
            int count=stationIndex==2?4:1;
            for(int i=0;i<count;i++)
            {
                var position=new Vector3(bounds.center.x+(count==1?0:(i%2==0?-1.8f:1.8f)),29.1f,bounds.min.z-1.6f-(count==1?0:i/2*3.7f));
                slots.Add(Slot(stations[stationIndex].transform,prefixes[stationIndex]+(i+1),position));
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(stations[stationIndex]);
        }
        manager.slots=slots.ToArray();
        var homes=new Transform[6];
        for(int i=0;i<6;i++)
        {
            var home=new GameObject("Tablet Choice Point "+(i+1)).transform;home.SetParent(managerObject.transform,false);
            home.position=new Vector3(-103+i%3*5,29.25f,255+i/3*4);homes[i]=home;
        }
        manager.tabletSpawns=homes;manager.tabletPrefab=BuildTablet();
        manager.completionSeals=new Light[5];
        for(int i=0;i<5;i++)
        {
            var seal=new GameObject("Challenge "+(i+1)+" Seal").AddComponent<Light>();seal.transform.SetParent(managerObject.transform,false);
            seal.transform.position=new Vector3(-111+i*5,29.8f,279);seal.type=LightType.Point;seal.range=8;seal.intensity=3;seal.color=new Color(.15f,.8f,1);seal.enabled=false;manager.completionSeals[i]=seal;
        }
        var stairs=Instance("Assets/CHEATCODE/DUNGEON/LowPolyDungeon/Prefabs/Tiles/Staircase.prefab","Stairway of Infinity",new Vector3(-101,28.03f,284),Quaternion.identity,Vector3.one*2);
        foreach(var renderer in stairs.GetComponentsInChildren<MeshRenderer>())
        {
            // Temporary CHEATCODE staircase uses the existing tower stone material.
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>stage.GetComponent<Renderer>().sharedMaterials[1]).ToArray();
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        var light=new GameObject("Staircase Completion Light").AddComponent<Light>();light.transform.SetParent(stairs.transform,true);light.transform.position=new Vector3(-101,32,287);
        light.type=LightType.Point;light.range=20;light.intensity=8;light.color=new Color(.5f,.85f,1);light.enabled=false;manager.staircaseLight=light;
        manager.nextScene=""; // Stage 7 is not implemented in the current build; retain menu/retry navigation.
        ConfigureTypography();
        manager.boardTitle.text="Challenge 1 — Verify the Base Case";manager.boardFormula.text="The Induction Tower\nStairway of Infinity";
        manager.boardInstructions.text="Interact with the Book Statue to begin.\nThe shared countdown is paused.";
        foreach(var slot in slots)slot.gameObject.SetActive(slot.key=="Base:1");
        var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==scene.path))build.Add(new EditorBuildSettingsScene(scene.path,true));else build.First(s=>s.path==scene.path).enabled=true;
        EditorBuildSettings.scenes=build.ToArray();
        EditorUtility.SetDirty(manager);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Induction Tower configured: five puzzles, connected scene prefabs, one shared timer.");
    }
    static GameObject Instance(string path,string name,Vector3 position,Quaternion rotation,Vector3 scale)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new InvalidOperationException("Missing asset: "+path);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(instance,"Place Induction asset");
        instance.name=name;instance.transform.SetPositionAndRotation(position,rotation);instance.transform.localScale=scale;PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);return instance;
    }
    static Bounds BoundsOf(GameObject obj)
    {var renderers=obj.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;}
    static void PhysicsBox(GameObject obj,string name,Bounds bounds)
    {
        var collider=new GameObject(name);collider.transform.position=bounds.center;collider.transform.SetParent(obj.transform,true);collider.AddComponent<BoxCollider>().size=bounds.size;
    }
    static TMP_Text WorldText(string name,Transform parent,Vector3 position,string value,Vector2 size,float font)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,true);go.transform.position=position;go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;
        // Parent models can have large import scale; keep world labels at unit scale.
        var scale=parent.lossyScale;go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        var text=go.AddComponent<TextMeshPro>();text.font=StageUITheme.ReadingFont;text.text=value;text.fontSize=font;text.enableAutoSizing=true;text.fontSizeMax=font;text.fontSizeMin=2;
        text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=size;text.color=StageUITheme.Ink;text.outlineWidth=.15f;text.outlineColor=Color.black;text.raycastTarget=false;
        return text;
    }
    static InductionSlot Slot(Transform parent,string key,Vector3 position)
    {
        var go=new GameObject(key.Replace(':',' ') + " Placement Slot");go.transform.SetParent(parent,true);go.transform.SetPositionAndRotation(position,Quaternion.identity);go.transform.localScale=Vector3.one;
        var slot=go.AddComponent<InductionSlot>();slot.key=key;slot.displayName=key.StartsWith("Step:")?"Step Slot "+key.Substring(5):key.StartsWith("Base:")?"Base Case Slot":"Hypothesis Slot";
        var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Slot Rim";pad.transform.SetParent(go.transform,false);pad.transform.localScale=new Vector3(2.8f,.14f,2.4f);Object.DestroyImmediate(pad.GetComponent<Collider>());
        slot.rim=pad.GetComponent<MeshRenderer>();slot.rim.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Boards+"/Materials/Chamber_Stone.mat");
        var trigger=go.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,.5f,0);trigger.size=new Vector3(2.8f,1.5f,2.4f);
        slot.tabletAnchor=new GameObject("Tablet Anchor").transform;slot.tabletAnchor.SetParent(go.transform,false);slot.tabletAnchor.localPosition=Vector3.up*.5f;
        slot.label=WorldText("Slot Label",go.transform,position+Vector3.up*3.1f,slot.displayName,new Vector2(3.4f,1),4);slot.MarkError(false);return slot;
    }
    static GameObject BuildTablet()
    {
        var existing=AssetDatabase.LoadAssetAtPath<GameObject>(TabletPath);if(existing!=null)return existing;
        var go=new GameObject("Formula Tablet");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Boards+"/Prefabs/ReusableFormulaScroll.prefab"));model.transform.SetParent(go.transform,false);model.transform.localScale=Vector3.one*1.5f;
            foreach(var text in model.GetComponentsInChildren<TMP_Text>())text.gameObject.SetActive(false);
            var tablet=go.AddComponent<InductionTablet>();var collider=go.GetComponent<BoxCollider>();collider.size=new Vector3(1.8f,.6f,1.2f);collider.isTrigger=true;
            tablet.label=WorldText("Assigned Formula",go.transform,Vector3.up*1.1f,"Formula tablet",new Vector2(4.4f,1.1f),4.2f);
            return PrefabUtility.SaveAsPrefabAsset(go,TabletPath);
        }
        finally{Object.DestroyImmediate(go);}
    }

    public static void ConfigureTypography()
    {
        var stage=Object.FindFirstObjectByType<InductionStageManager>();
        var math=FontAsset("Induction Math", "Assets/TextMesh Pro/Fonts/NotoSansMath-Regular.ttf", "⋯≥⇒²−–—");
        string characters=new string(Enumerable.Range(32,95).Select(i=>(char)i).ToArray())+"²−–—≥";
        var reading=FontAsset("Induction Reading", "Assets/TextMesh Pro/Fonts/LiberationSans.ttf", characters);
        reading.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{math};EditorUtility.SetDirty(reading);
        stage.readingFont=reading;EditorUtility.SetDirty(stage);
        foreach(var text in stage.stations.SelectMany(t=>t.GetComponentsInChildren<TMP_Text>(true)).Concat(stage.informationBoard.GetComponentsInChildren<TMP_Text>(true)).Concat(stage.scrollStand.GetComponentsInChildren<TMP_Text>(true)))
        { text.font=reading;EditorUtility.SetDirty(text);PrefabUtility.RecordPrefabInstancePropertyModifications(text); }
        stage.bookPrompt.font=reading;
        var tablet=AssetDatabase.LoadAssetAtPath<GameObject>(TabletPath);tablet.GetComponent<InductionTablet>().label.font=reading;EditorUtility.SetDirty(tablet.GetComponent<InductionTablet>().label);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
    }
    static TMP_FontAsset FontAsset(string name,string source,string characters)
    {
        var path=Root+"/"+name+".asset";var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font!=null)return font;
        font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(source),64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,false);
        font.name=name;string missing;font.TryAddCharacters(characters,out missing);font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();return font;
    }
}
