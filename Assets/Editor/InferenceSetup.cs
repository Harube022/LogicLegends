using System;
using System.Collections.Generic;
using System.Linq;
using LogicLegends.Inference;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// One-time authoring tool. Runtime content remains editable scene objects and ScriptableObjects.
public static class InferenceSetup
{
    const string DataPath = "Assets/Gameplay/RulesOfInference";
    const string PrefabPath = "Assets/Prefabs/PREFABS/RulesOfInference";
    static TMP_FontAsset font;

    static InferencePremise Row(params string[] tokens) => new InferencePremise { tokens = tokens };
    static InferenceForm Form(string conclusion, string[] wrong, params InferencePremise[] rows)
        => new InferenceForm { conclusion = conclusion, distractors = wrong, premises = rows };

    static InferenceExample[] Examples()
    {
        return new[] {
            new InferenceExample { title="Weather", statements=new[]{"It is raining", "The ground is wet", "The grass is slippery", "The path is closed"}, aliases=new[]{"It's raining", "", "", ""} },
            new InferenceExample { title="Temple", statements=new[]{"The torch is lit", "The gate is open", "The bell is ringing", "The bridge is lowered"} },
            new InferenceExample { title="Study", statements=new[]{"Sara studies", "Sara passes the quiz", "Lee practices", "Lee solves the puzzle"} },
            new InferenceExample { title="Garden", statements=new[]{"The seed is planted", "The flower grows", "The sun is shining", "The soil is warm"} },
            new InferenceExample { title="Library", statements=new[]{"I make tea", "I read a book", "I take notes", "I remember the lesson"} },
            new InferenceExample { title="Morning", statements=new[]{"Sara wakes up early", "Sara exercises", "Lee stays up late", "Lee drinks coffee"} }
        };
    }

    public static InferenceRule[] CreateRules()
    {
        EnsureFolder(DataPath);
        var result=new List<InferenceRule>();
        Action<string,string,string,InferenceForm[]> add=(abbr,name,instruction,forms)=> {
            string path=DataPath+"/"+abbr+".asset";
            var rule=AssetDatabase.LoadAssetAtPath<InferenceRule>(path);
            if(rule==null) {
                rule=ScriptableObject.CreateInstance<InferenceRule>();
                rule.ruleName=name; rule.abbreviation=abbr; rule.instruction=instruction;
                rule.forms=forms; rule.examples=Examples(); AssetDatabase.CreateAsset(rule,path);
            }
            result.Add(rule);
        };
        add("MP","Modus Ponens","Use the conditional and its antecedent to infer the consequent.",new[]{
            Form("{q}",new[]{"NOT ({p})","NOT ({q})","{r}"},Row("[IF]","{p}","[THEN]","{q}"),Row("{p}")) });
        add("MT","Modus Tollens","Negating the consequent lets you negate the antecedent.",new[]{
            Form("NOT ({p})",new[]{"{p}","{q}","NOT ({r})"},Row("[IF]","{p}","[THEN]","{q}"),Row("[NOT]","{q}")) });
        add("HS","Hypothetical Syllogism","Link the two conditionals through their shared proposition.",new[]{
            Form("IF {p} THEN {r}",new[]{"IF {r} THEN {p}","IF {p} THEN NOT ({r})","{s}"},Row("[IF]","{p}","[THEN]","{q}"),Row("[IF]","{q}","[THEN]","{r}")) });
        add("DS","Disjunctive Syllogism","Eliminate the false alternative from the disjunction.",new[]{
            Form("{q}",new[]{"{p}","NOT ({q})","{r}"},Row("{p}","[OR]","{q}"),Row("[NOT]","{p}")) });
        // Slide 16 uses one conjunctive premise containing both conditionals.
        add("CD","Constructive Dilemma","Combine the two conditionals with the choice of antecedents.",new[]{
            Form("{q} OR {s}",new[]{"{q} AND {s}","NOT ({q})","NOT ({s})"},
                Row("(","[IF]","{p}","[THEN]","{q}",")","\n","[AND]","(","[IF]","{r}","[THEN]","{s}",")"),Row("{p}","[OR]","{r}")) });
        add("SIMP","Simplification","A conjunction guarantees each of its parts. Infer p.",new[]{
            Form("{p}",new[]{"NOT ({p})","NOT ({q})","{r}"},Row("{p}","[AND]","{q}")) });
        add("CONJ","Conjunction","Join the two given propositions with AND.",new[]{
            Form("{p} AND {q}",new[]{"{p} AND NOT ({q})","NOT ({p})","{r}"},Row("{p}"),Row("{q}")) });
        add("ADD","Addition","Add q as an alternative to the given proposition.",new[]{
            Form("{p} OR {q}",new[]{"{p} AND {q}","NOT ({p})","{q}"},Row("{p}")) });
        add("DN","Double Negation","Two negations preserve the original proposition.",new[]{
            Form("{p}",new[]{"NOT ({p})","{q}","{r}"},Row("[NOT]","[NOT]","{p}")),
            Form("NOT (NOT ({p}))",new[]{"NOT ({p})","{q}","{r}"},Row("{p}")) });
        // Follow slide 27's TAU/idempotence examples, not an unsupported arbitrary assertion.
        add("TAU","Tautology","Repeating a proposition with OR or AND leaves its meaning unchanged.",new[]{
            Form("{p}",new[]{"NOT ({p})","{q}","{r}"},Row("{p}","[OR]","{p}")),
            Form("{p}",new[]{"NOT ({p})","{q}","{r}"},Row("{p}","[AND]","{p}")) });
        AssetDatabase.SaveAssets(); return result.ToArray();
    }

    [MenuItem("Tools/Logic Legends/Build Inference Area in PRELIM")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/PRELIM.unity"||EditorApplication.isPlaying) throw new InvalidOperationException("Open PRELIM outside Play mode.");
        var area=scene.GetRootGameObjects().Single(g=>g.name=="Rules_Of_Inference");
        if(area.transform.Find("InferenceGameplay")!=null) throw new InvalidOperationException("InferenceGameplay already exists; edit the existing setup instead of duplicating it.");
        var altar=area.transform.Find("FLOOR/ALTAR");
        if(altar==null) throw new InvalidOperationException("The existing altar is missing.");
        var truthCamera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CinemachineCamera>(true)).Single(c=>c.name=="VCam_TruthTable");
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        EnsureFolder(PrefabPath);
        var root=new GameObject("InferenceGameplay"); Undo.RegisterCreatedObjectUndo(root,"Build Rules of Inference");
        root.transform.SetParent(area.transform,false);
        root.transform.position=Vector3.zero;
        var puzzle=root.AddComponent<InferenceChallenge>(); puzzle.rules=CreateRules(); puzzle.font=font;
        puzzle.gameplayHud=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="Gameplay_Interface")?.gameObject;

        // The imported altar root has a collider but no mesh; give its visible meshes matching collisions.
        foreach(var filter in altar.GetComponentsInChildren<MeshFilter>(true)) {
            if(filter.GetComponent<Collider>()==null) { var c=Undo.AddComponent<MeshCollider>(filter.gameObject);c.sharedMesh=filter.sharedMesh; }
        }
        Physics.SyncTransforms();
        Vector3 boardPosition=new Vector3(100,171,760);
        var board=InferenceBoardUI.Rect("InferenceBoard",root.transform);
        board.position=boardPosition; board.rotation=Quaternion.Euler(90,0,0); board.localScale=Vector3.one*0.01f;
        board.sizeDelta=new Vector2(1600,1000);
        var canvas=board.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.worldCamera=Camera.main;
        board.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        puzzle.boardInput=board.gameObject.AddComponent<CanvasGroup>();
        var background=board.gameObject.AddComponent<UnityEngine.UI.Image>(); background.color=new Color(0.035f,0.085f,0.11f,1f);
        puzzle.board=board.gameObject;
        var body=InferenceBoardUI.Rect("BoardContent",board);InferenceBoardUI.Stretch(body,40);
        var layout=body.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing=12;layout.childControlHeight=layout.childControlWidth=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;
        puzzle.heading=Label(body,"RULES OF INFERENCE",40,65);
        puzzle.heading.color=new Color(0.89f,0.76f,0.46f);
        puzzle.legend=Label(body,"Proposition key",30,140);
        puzzle.wordBank=Label(body,"WORD BANK",28,48);
        var premises=InferenceBoardUI.Rect("Premises",body);
        var premiseLayout=premises.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        premiseLayout.spacing=14;premiseLayout.childControlWidth=premiseLayout.childControlHeight=true;premiseLayout.childForceExpandHeight=false;
        var premiseSize=premises.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();premiseSize.preferredHeight=285;
        puzzle.premiseContainer=premises;
        var conclusion=InferenceBoardUI.Rect("Conclusion",body);
        var conclusionLayout=conclusion.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        conclusionLayout.spacing=18;conclusionLayout.childControlWidth=conclusionLayout.childControlHeight=true;conclusionLayout.childForceExpandWidth=false;
        conclusion.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=100;
        var socketVisual=InferenceBoardUI.Text(conclusion,"[ CRYSTAL ]",font,24,170);socketVisual.color=new Color(0.5f,0.93f,0.84f);
        puzzle.conclusionLabel=InferenceBoardUI.Text(conclusion,"Therefore, __________________________",font,28,1240);
        puzzle.feedback=Label(body,"Complete the premises and place a conclusion crystal.",30,88);
        var buttons=InferenceBoardUI.Rect("Actions",body);
        var buttonLayout=buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        buttonLayout.spacing=16;buttonLayout.childControlWidth=buttonLayout.childControlHeight=true;buttonLayout.childForceExpandWidth=true;
        buttons.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=68;
        puzzle.placeButton=Button(buttons,"Place crystal");puzzle.validateButton=Button(buttons,"Check argument");
        puzzle.exploreButton=Button(buttons,"Explore / continue");puzzle.nextButton=Button(buttons,"Next challenge");
        puzzle.nextButton.gameObject.SetActive(false);

        // Use the same Cinemachine activation and existing Brain blend as TruthTableCameraTrigger.
        var cameraGo=UnityEngine.Object.Instantiate(truthCamera.gameObject,root.transform);
        cameraGo.name="VCam_RulesOfInference";
        cameraGo.transform.SetPositionAndRotation(boardPosition+Vector3.up*15.5f,Quaternion.Euler(90,0,0));
        var focus=cameraGo.GetComponent<CinemachineCamera>();focus.Priority=100;
        var lens=focus.Lens;lens.FieldOfView=40;focus.Lens=lens;
        focus.Follow=null;focus.LookAt=null;
        puzzle.focusCamera=cameraGo;cameraGo.SetActive(false);

        var socket=new GameObject("ConclusionSocket").transform;socket.SetParent(board,false);
        socket.localPosition=new Vector3(-680,-184,-90);socket.localRotation=Quaternion.Euler(-90,0,0);socket.localScale=Vector3.one;
        puzzle.conclusionSocket=socket;
        var zone=new GameObject("AltarTrigger");zone.transform.SetParent(root.transform,false);
        zone.transform.position=new Vector3(100,160,768);
        var trigger=zone.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(9,12,7);
        var rigid=zone.AddComponent<Rigidbody>();rigid.isKinematic=true;rigid.useGravity=false;
        zone.AddComponent<InferenceAltarTrigger>().challenge=puzzle;

        var crystalPrefab=CreateCrystalPrefab();puzzle.crystalPrefab=crystalPrefab;
        var points=new[]{new Vector3(100,0,801),new Vector3(72,0,792),new Vector3(130,0,795),new Vector3(121,0,727)};
        puzzle.crystalSpawns=new Transform[points.Length];
        for(int i=0;i<points.Length;i++) {
            var point=new GameObject("CrystalSpawn_"+(i+1)).transform;point.SetParent(root.transform,false);
            var ray=new Ray(new Vector3(points[i].x,230,points[i].z),Vector3.down);
            var hits=Physics.RaycastAll(ray,130).Where(h=>h.collider.transform.IsChildOf(area.transform)&&h.normal.y>0.65f).OrderBy(h=>h.distance).ToArray();
            if(hits.Length==0) throw new InvalidOperationException("No walkable floor for crystal "+i);
            point.position=hits[0].point+Vector3.up*1.1f;puzzle.crystalSpawns[i]=point;
        }
        var marker=new GameObject("InferenceComplete");marker.transform.SetParent(root.transform,false);
        marker.transform.position=new Vector3(100,167,761);
        var completeText=marker.AddComponent<TextMeshPro>();completeText.font=font;completeText.text="INFERENCE COMPLETE";completeText.fontSize=6;completeText.alignment=TextAlignmentOptions.Center;
        completeText.color=new Color(0.6f,1f,0.78f);completeText.rectTransform.sizeDelta=new Vector2(35,4);
        marker.transform.rotation=Quaternion.Euler(0,180,0);marker.SetActive(false);puzzle.completionMarker=marker;
        board.gameObject.SetActive(false);area.SetActive(false);
        EditorUtility.SetDirty(puzzle);EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
    }

    static InferenceCrystal CreateCrystalPrefab()
    {
        string path=PrefabPath+"/InferenceCrystal.prefab";
        var existing=AssetDatabase.LoadAssetAtPath<InferenceCrystal>(path);if(existing!=null)return existing;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FINAL_ASSETS/MAPS/CINEMATIC/CRYSTAL.fbx");
        var sourceMesh=source.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="CRYSTAL");
        var root=new GameObject("InferenceCrystal");
        root.AddComponent<Rigidbody>().isKinematic=true;root.GetComponent<Rigidbody>().useGravity=false;
        var collider=root.AddComponent<SphereCollider>();collider.radius=0.85f;
        root.AddComponent<GrabbableObject>();
        var crystal=root.AddComponent<InferenceCrystal>();
        var visual=new GameObject("CrystalMesh");visual.transform.SetParent(root.transform,false);
        visual.transform.localScale=Vector3.one*0.5f;visual.transform.localPosition=Vector3.up*0.45f;
        visual.AddComponent<MeshFilter>().sharedMesh=sourceMesh.sharedMesh;
        visual.AddComponent<MeshRenderer>().sharedMaterials=sourceMesh.GetComponent<MeshRenderer>().sharedMaterials;
        var label=new GameObject("ConclusionLabel");label.transform.SetParent(root.transform,false);label.transform.localPosition=Vector3.up*1.8f;
        var text=label.AddComponent<TextMeshPro>();text.font=font;text.text="Conclusion";text.fontSize=2.5f;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(8,3);text.richText=false;
        crystal.label=text;
        var saved=PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
        return saved.GetComponent<InferenceCrystal>();
    }

    static TMP_Text Label(Transform parent,string text,float size,float height)
    {
        var label=InferenceBoardUI.Text(parent,text,font,size);label.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=height;return label;
    }
    static UnityEngine.UI.Button Button(Transform parent,string text)
    {
        var r=InferenceBoardUI.Rect(text,parent);r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredWidth=320;
        var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(0.16f,0.31f,0.33f);
        var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var label=InferenceBoardUI.Text(r,text,font,25);InferenceBoardUI.Stretch(label.rectTransform,8);label.alignment=TextAlignmentOptions.Center;
        return button;
    }
    static void EnsureFolder(string path)
    {
        var parts=path.Split('/');string current=parts[0];
        for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
    }

    public static string ValidateContent()
    {
        var rules=AssetDatabase.FindAssets("t:InferenceRule",new[]{DataPath}).Select(g=>AssetDatabase.LoadAssetAtPath<InferenceRule>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        if(rules.Length!=10)throw new Exception("Expected ten rules.");
        int cases=0;
        foreach(var rule in rules) for(int f=0;f<rule.forms.Length;f++)for(int e=0;e<rule.examples.Length;e++) {
            var q=new InferenceQuestion(rule,f,e);
            if(string.IsNullOrWhiteSpace(q.Conclusion)||q.Conclusion.Contains("{"))throw new Exception("Unresolved conclusion: "+rule.name);
            foreach(var premise in q.Form.premises)foreach(var token in premise.tokens) if(InferenceQuestion.IsBlank(token)) {
                string answer=token.StartsWith("[")?token.Trim('[',']'):q.Resolve(token);
                if(!q.Accepts(token,"  "+answer.ToUpperInvariant()+". ")||q.Accepts(token,"")||q.Accepts(token,"unrelated answer"))throw new Exception("Validation error: "+rule.name);
            }
            if(q.Form.distractors.Select(q.Resolve).Distinct().Count()!=3||q.Form.distractors.Select(q.Resolve).Contains(q.Conclusion))throw new Exception("Ambiguous crystals: "+rule.name);
            cases++;
        }
        var deck=new InferenceDeck(431);InferenceRule previous=null;
        var previousExamples=new Dictionary<InferenceRule,InferenceExample>();
        for(int cycle=0;cycle<20;cycle++) {
            var seen=new HashSet<InferenceRule>();
            for(int i=0;i<10;i++) {
                var q=deck.Draw(rules);
                if(!seen.Add(q.Rule)||q.Rule==previous)throw new Exception("Repeated rule.");
                if(previousExamples.TryGetValue(q.Rule,out var example)&&example==q.Example)throw new Exception("Repeated example.");
                previous=q.Rule;previousExamples[q.Rule]=q.Example;
            }
        }
        string result=cases+" rule/form/example combinations and 200 randomized draws passed.";
        Debug.Log(result);return result;
    }
}
