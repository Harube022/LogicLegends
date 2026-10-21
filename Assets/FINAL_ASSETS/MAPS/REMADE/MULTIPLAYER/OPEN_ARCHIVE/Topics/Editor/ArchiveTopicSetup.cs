using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Inference;
using LogicLegends.Graph;
using LogicLegends.Trees;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveTopicSetup
    {
        public const string Root="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE";
        public const string Folder=Root+"/Topics";
        public const string ScenePath=Folder+"/Testing/MultiplayerGameplay.unity";
        const string Source=Root+"/Testing/MonsterEncounter/OpenArchive_MonsterEncounter_Test.unity";
        static Material stone,wood,paper,teal,gold,lines,labelMaterial;
        static Mesh cube,padMesh,ringMesh,crystalMesh;
        static TMP_FontAsset font;
        static GameObject truthToken,numberToken,wordToken;

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Open Saved Topic Test")]
        public static void Open(){RequireSaved();EditorSceneManager.OpenScene(ScenePath);}
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Save Clean Topic Test Layout")]
        public static void SaveClean()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before saving the layout.");
            var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)throw new InvalidOperationException("Open the saved topic test scene first.");
            var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var toggle=transforms.First(t=>t.name=="Show_Hide_Test_Tools").GetComponent<UnityEngine.UI.Toggle>();
            toggle.SetIsOnWithoutNotify(false);transforms.First(t=>t.name=="Test_Tools_Content").gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Build Adapted Topic Test (Once)")]
        public static void Build()
        {
            RequireSaved();if(File.Exists(ScenePath))throw new InvalidOperationException("The topic scene exists. Open the saved scene; the builder never overwrites manual work.");
            foreach(string name in new[]{"Art","Art/Meshes","Art/Materials","Art/Prefabs","Testing","Testing/Reports"})Directory.CreateDirectory(Folder+"/"+name);
            AssetDatabase.Refresh();
            var catalog=Catalog();Art();
            var token=TokenPrefab(false);var crystal=TokenPrefab(true);var pad=PadPrefab();var lectern=LecternPrefab();
            truthToken=ActivityToken("Archive_TruthToken",0);numberToken=ActivityToken("Archive_NumberBlock",1);wordToken=ActivityToken("Archive_WordTile",2);
            var kits=new GameObject[10];for(int i=0;i<10;i++)kits[i]=StationPrefab(i,token,crystal,pad,lectern);
            AssetDatabase.SaveAssets();
            if(!AssetDatabase.CopyAsset(Source,ScenePath))throw new IOException("Unable to copy the existing monster encounter scene.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var service=new GameObject("OpenArchive_Team_Topics").AddComponent<ArchiveTopicSession>();service.catalog=catalog;
            service.hunting=Object.FindFirstObjectByType<LogicLegends.OpenArchive.Hunting.KhezuHuntingController>();
            service.stations=new ArchiveTopicStation[10];
            var slots=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("StructureSlot_")).OrderBy(t=>t.name).ToArray();
            if(slots.Length!=10)throw new InvalidOperationException("Expected exactly ten existing reserved topic anchors.");
            for(int i=0;i<10;i++)
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(kits[i],scene);instance.transform.SetParent(slots[i],false);
                var towardCenter=-slots[i].localPosition;towardCenter.y=0;
                instance.transform.localRotation=Quaternion.LookRotation(towardCenter,Vector3.up);
                var station=instance.GetComponent<ArchiveTopicStation>();station.session=service;service.stations[i]=station;
            }
            var ui=new GameObject("Archive_Topic_Interface",typeof(RectTransform)).AddComponent<ArchiveTopicUI>();ui.session=service;
            ui.panelArt=Sprite("Assets/Art/GraphHUD/GraphMapFrame.png");ui.statusArt=Sprite("Assets/Art/GraphHUD/GraphStatusFrame.png");
            ui.buttonArt=Sprite("Assets/Resources/StageUI/WoodButtonTeal.png");ui.textFont=StageUITheme.ReadingFont;ui.headingFont=StageUITheme.HeadingFont;
            var tools=service.gameObject.AddComponent<ArchiveTopicTestControls>();tools.session=service;
            tools.headingFont=StageUITheme.HeadingFont;tools.textFont=StageUITheme.ReadingFont;tools.buttonArt=ui.buttonArt;tools.frameArt=ui.statusArt;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=service.gameObject;
            File.WriteAllText(Folder+"/Testing/Reports/Setup.json",JsonUtility.ToJson(new SetupReport{scene=ScenePath,source=Source,topicCount=10,sharedProgress=true,mistakeAttribution=true,originalScenePreserved=true},true));
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
        [Serializable] sealed class SetupReport{public string scene,source;public int topicCount;public bool sharedProgress,mistakeAttribution,originalScenePreserved;}
        static ArchiveTopicCatalog Catalog()
        {
            var c=ScriptableObject.CreateInstance<ArchiveTopicCatalog>();
            var preview=EditorSceneManager.OpenPreviewScene("Assets/Scenes/PRELIM.unity");
            try
            {
                var quiz=preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<QuizManager>(true)).First();
                var original=(List<TopicChallenge>)typeof(QuizManager).GetField("challenges",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(quiz);
                foreach(var bank in original)c.propositions.Add(new ArchiveLogicBank{title=bank.topicName,questions=bank.questionsPool.Select(q=>new LogicQuestion{questionText=q.questionText,options=q.options.ToArray(),correctOptionIndex=q.correctOptionIndex}).ToList()});
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            c.inferenceRules=AssetDatabase.FindAssets("t:InferenceRule",new[]{"Assets/Gameplay/RulesOfInference"}).Select(g=>AssetDatabase.LoadAssetAtPath<InferenceRule>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(r=>r.abbreviation).ToArray();
            c.formalProofs=AssetDatabase.LoadAssetAtPath<ProofChallengeCatalog>("Assets/Resources/ProofChallenges.asset");
            c.proofMethods=AssetDatabase.LoadAssetAtPath<DirectIndirectProofCatalog>("Assets/Resources/DirectIndirectProofChallenges.asset");
            c.mappings=AssetDatabase.LoadAssetAtPath<MappingChallengeCatalog>("Assets/Resources/FunctionsRelationsChallenges.asset");
            c.graphs=AssetDatabase.LoadAssetAtPath<GraphChallengeCatalog>("Assets/Prefabs/Graph/GraphChallenges.asset");
            c.trees=AssetDatabase.LoadAssetAtPath<TreeChallengeCatalog>("Assets/Prefabs/Trees/TreeChallenges.asset");
            foreach(string name in new[]{"challengeOneBank","challengeTwoBank","challengeThreeBank","challengeFourBank","challengeFiveBank"})
            {
                var bank=new ArchiveSetBank();var source=(Array)typeof(SetssssChallengeOne).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
                foreach(object item in source)
                {
                    if(item is SetsConditionData condition)bank.questions.Add(new ArchiveSetCase{a=condition.setA.ToArray(),b=condition.setB.ToArray()});
                    else
                    {
                        var type=item.GetType();var selected=(string[])type.GetField("selected").GetValue(item);string setName=(string)type.GetField("setName").GetValue(item);
                        bank.questions.Add(new ArchiveSetCase{a=setName=="A"?selected:Array.Empty<string>(),b=setName=="B"?selected:Array.Empty<string>(),universe=(string[])type.GetField("universe").GetValue(item),setName=setName});
                    }
                }
                c.sets.Add(bank);
            }
            if(c.propositions.Count!=5||c.inferenceRules.Length!=10||c.formalProofs==null||c.proofMethods==null||c.mappings==null||c.graphs==null||c.trees==null)throw new InvalidOperationException("An original curriculum catalog is missing.");
            AssetDatabase.CreateAsset(c,Folder+"/Archive_Topic_Catalog.asset");return c;
        }
        static void Art()
        {
            stone=CloneMaterial(Root+"/Art/Materials/Archive_WeatheredStone_Vertex.mat","Archive_Topic_Stone");
            wood=CloneMaterial(Root+"/Art/Materials/Archive_TexturedBridgeWood.mat","Archive_Topic_Wood");
            paper=Material("Archive_Topic_ReadingFace",new Color(.86f,.80f,.65f),true);
            teal=Material("Archive_Topic_TealInlay",new Color(.13f,.42f,.40f),false);
            gold=Material("Archive_Topic_BrassInlay",new Color(.70f,.48f,.17f),false);
            lines=Material("Archive_Topic_Energy",new Color(.15f,.65f,.61f),true);
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp);
            padMesh=SaveMesh("Archive_BeveledOctagon",Prism(8));ringMesh=SaveMesh("Archive_InlayRing",Ring(64));crystalMesh=SaveMesh("Archive_RuleCrystal",Crystal());
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset")??StageUITheme.ReadingFont;
            labelMaterial=new Material(font.material){name="Archive_Topic_WorldLabels"};labelMaterial.SetColor("_OutlineColor",new Color(.10f,.07f,.035f));labelMaterial.SetFloat("_OutlineWidth",.16f);
            AssetDatabase.CreateAsset(labelMaterial,Folder+"/Art/Materials/Archive_Topic_WorldLabels.mat");
            PolishMaterials();
        }
        static Material CloneMaterial(string source,string name)
        {
            var original=AssetDatabase.LoadAssetAtPath<Material>(source);if(original==null)throw new FileNotFoundException(source);
            var m=new Material(original){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");return m;
        }
        static Material Material(string name,Color color,bool unlit)
        {
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("The installed URP shader is missing.");
            var m=new Material(shader){name=name,enableInstancing=true};m.SetColor("_BaseColor",color);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.15f);
            AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");return m;
        }
        static GameObject TokenPrefab(bool crystal)
        {
            var root=new GameObject(crystal?"Archive_RuleCrystal":"Archive_FormulaScroll");
            if(crystal)
            {
                Part(root.transform,"Faceted_Crystal",crystalMesh,teal,Vector3.zero,new Vector3(.75f,.9f,.75f));
                Part(root.transform,"Crystal_Socket",padMesh,gold,new Vector3(0,-.04f,0),new Vector3(.55f,.25f,.55f));
            }
            else
            {
                Part(root.transform,"Parchment_Face",cube,paper,new Vector3(0,.12f,0),new Vector3(.75f,.12f,.58f));
                for(int side=-1;side<=1;side+=2)Part(root.transform,"Wood_Roller",padMesh,wood,new Vector3(0,.14f,side*.3f),new Vector3(.15f,1.9f,.15f),new Vector3(0,0,90));
                Part(root.transform,"Readable_Tab",cube,teal,new Vector3(0,.185f,0),new Vector3(.14f,.012f,.4f));
            }
            return SavePrefab(root,root.name);
        }
        static GameObject PadPrefab()
        {
            var root=new GameObject("Archive_AnswerRunePad");
            Part(root.transform,"Beveled_Stone",padMesh,stone,Vector3.zero,new Vector3(2.2f,.15f,2.2f));
            Part(root.transform,"Inset_Ring",ringMesh,teal,new Vector3(0,.073f,0),new Vector3(1.8f,1,1.8f));
            Part(root.transform,"Inner_Ring",ringMesh,gold,new Vector3(0,.075f,0),new Vector3(1.4f,1,1.4f));
            return SavePrefab(root,root.name);
        }
        static GameObject LecternPrefab()
        {
            var root=new GameObject("Archive_OpenBook_Lectern");
            Part(root.transform,"Stone_Foot",padMesh,stone,Vector3.zero,new Vector3(1.25f,.7f,1.25f));
            Part(root.transform,"Carved_Pillar",padMesh,stone,new Vector3(0,.25f,0),new Vector3(.6f,2.2f,.6f));
            Part(root.transform,"Book_Cover",cube,wood,new Vector3(0,1.29f,0),new Vector3(1.45f,.12f,.95f));
            Part(root.transform,"Left_Page",cube,paper,new Vector3(-.34f,1.38f,0),new Vector3(.64f,.08f,.82f),new Vector3(0,0,-8));
            Part(root.transform,"Right_Page",cube,paper,new Vector3(.34f,1.38f,0),new Vector3(.64f,.08f,.82f),new Vector3(0,0,8));
            for(int side=-1;side<=1;side+=2)for(int line=0;line<4;line++)Part(root.transform,"Engraved_Text_Line",cube,teal,new Vector3(side*.34f,1.43f,-.24f+line*.15f),new Vector3(.4f,.01f,.024f));
            var collider=root.AddComponent<BoxCollider>();collider.center=new Vector3(0,.65f,0);collider.size=new Vector3(1.2f,1.4f,1);
            var obstacle=root.AddComponent<UnityEngine.AI.NavMeshObstacle>();obstacle.shape=UnityEngine.AI.NavMeshObstacleShape.Box;obstacle.center=collider.center;obstacle.size=collider.size;obstacle.carving=true;obstacle.carveOnlyStationary=true;
            return SavePrefab(root,root.name);
        }
        static GameObject ActivityToken(string name,int kind)
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Art/Prefabs/"+name+".prefab");if(existing!=null)return existing;
            var root=new GameObject(name);
            if(kind==0)
            {
                Part(root.transform,"Token_Rim",padMesh,teal,Vector3.zero,new Vector3(.95f,.4f,.95f));
                Part(root.transform,"Token_ReadingFace",padMesh,paper,new Vector3(0,.17f,0),new Vector3(.75f,.08f,.75f));
            }
            else if(kind==1)
            {
                Part(root.transform,"Number_Block",cube,wood,new Vector3(0,.23f,0),new Vector3(.65f,.46f,.65f));
                Part(root.transform,"Number_Face",cube,paper,new Vector3(0,.24f,-.333f),new Vector3(.55f,.34f,.02f));
                Part(root.transform,"Top_Inlay",cube,teal,new Vector3(0,.465f,0),new Vector3(.42f,.014f,.42f));
            }
            else
            {
                Part(root.transform,"Word_Plate",cube,wood,new Vector3(0,.08f,0),new Vector3(.9f,.16f,.5f));
                Part(root.transform,"Word_ReadingFace",cube,paper,new Vector3(0,.165f,0),new Vector3(.78f,.02f,.4f));
            }
            return SavePrefab(root,name);
        }
        static GameObject StationPrefab(int topic,GameObject token,GameObject crystal,GameObject pad,GameObject lectern)
        {
            var root=new GameObject("Archive_T"+(topic+1).ToString("00")+"_"+ArchiveTopicCatalog.Arenas[topic].Replace(' ','_').Replace("'",""));
            var station=root.AddComponent<ArchiveTopicStation>();station.topic=topic;station.tokenPrefab=token;station.crystalPrefab=crystal;station.padPrefab=pad;station.energyMaterial=lines;station.neutralMaterial=teal;station.font=font;station.labelMaterial=labelMaterial;
            if(topic==1)station.tokenPrefab=truthToken;else if(topic==2)station.tokenPrefab=wordToken;else if(topic==6)station.tokenPrefab=numberToken;
            var work=new GameObject("Shared_Activity_Objects");work.transform.SetParent(root.transform,false);station.workRoot=work.transform;
            var book=(GameObject)PrefabUtility.InstantiatePrefab(lectern);book.transform.SetParent(root.transform,false);book.transform.localPosition=ArchiveTopicStation.BookPosition;
            Part(root.transform,"Outer_Floor_Seal",ringMesh,stone,new Vector3(0,.022f,0),new Vector3(15.5f,1,15.5f));
            Part(root.transform,"Seal_Inlay",ringMesh,teal,new Vector3(0,.024f,0),new Vector3(14.7f,1,14.7f));
            // The central footprint remains flat: no walls, raised platforms, roofs or collision fences.
            Part(root.transform,"Question_Board_Frame",cube,wood,new Vector3(0,2.35f,10.2f),new Vector3(7.5f,3.7f,.22f));
            Part(root.transform,"Question_Board_Face",cube,paper,new Vector3(0,2.35f,10.06f),new Vector3(7.05f,3.25f,.06f));
            Part(root.transform,"Topic_Header_Backplate",cube,wood,new Vector3(0,4.85f,10.22f),new Vector3(9.5f,1.25f,.16f));
            for(int side=-1;side<=1;side+=2)Part(root.transform,"Board_Support",cube,stone,new Vector3(side*3.15f,1.3f,10.25f),new Vector3(.32f,2.6f,.4f));
            station.board=WorldText(root.transform,"Question_And_Instructions","BOOK STATUE\nInteract to begin",new Vector3(0,2.4f,10.01f),new Vector2(6.65f,2.9f),7);
            station.title=WorldText(root.transform,"Topic_Identity","T"+(topic+1)+"  "+ArchiveTopicCatalog.Names[topic]+"\n"+ArchiveTopicCatalog.Arenas[topic],new Vector3(0,4.85f,10.1f),new Vector2(9.0f,1.1f),6);
            station.title.fontSharedMaterial=labelMaterial;station.title.color=new Color(1,.94f,.78f);
            if(topic==6)
            {
                Part(root.transform,"Set_A_Region",ringMesh,gold,new Vector3(-2.2f,.038f,-1.3f),new Vector3(6.8f,1,6.8f));
                Part(root.transform,"Set_B_Region",ringMesh,teal,new Vector3(2.2f,.04f,-1.3f),new Vector3(6.8f,1,6.8f));
                for(int side=-1;side<=1;side+=2)
                {
                    Part(root.transform,"Universal_Set_Edge",cube,stone,new Vector3(side*7,.018f,-1.3f),new Vector3(.13f,.035f,13));
                    Part(root.transform,"Universal_Set_Edge",cube,stone,new Vector3(0,.018f,-1.3f+side*6.5f),new Vector3(14,.035f,.13f));
                }
            }
            else if(topic==5)
                foreach(var p in new[]{new Vector3(-4,.03f,3),new Vector3(4,.03f,3),new Vector3(0,.03f,-3)})Part(root.transform,"Induction_Altar_Seal",ringMesh,gold,p,new Vector3(3.4f,1,3.4f));
            else if(topic==7)
                for(int i=0;i<4;i++)foreach(int side in new[]{-1,1})
                {
                    Part(root.transform,"Mapping_Pylon_Foot",padMesh,stone,new Vector3(side*4.5f,.015f,4.8f-i*3),new Vector3(1.5f,.12f,1.5f));
                    Part(root.transform,"Mapping_Conduit",cube,gold,new Vector3(side*4.5f,.06f,.3f),new Vector3(.10f,.025f,10.5f));
                }
            else if(topic==1)
                for(int row=0;row<4;row++)for(int column=0;column<3;column++)Part(root.transform,"Truth_Mosaic_Tile",cube,(column+row)%2==0?stone:paper,new Vector3((column-1)*1.65f,.024f,(row-1.5f)*1.4f-1),new Vector3(1.5f,.045f,1.25f));
            else
                for(int i=0;i<topic+3;i++)
                {
                    float angle=i*Mathf.PI*2/(topic+3);Part(root.transform,"Mathematical_Inlay",padMesh,i%2==0?gold:teal,new Vector3(Mathf.Cos(angle)*6.5f,.027f,Mathf.Sin(angle)*6.5f),new Vector3(.32f,.10f,.32f));
                }
            OrientBoard(root);
            return SavePrefab(root,root.name);
        }
        static void OrientBoard(GameObject root)
        {
            // Keep the approach from the nest open; the reading backdrop belongs at the far edge.
            foreach(Transform child in root.transform)
                if(child.name.StartsWith("Question_Board_")||child.name=="Board_Support"||child.name=="Topic_Header_Backplate"||child.name=="Question_And_Instructions"||child.name=="Topic_Identity")
                {
                    var position=child.localPosition;position.z=-Mathf.Abs(position.z);child.localPosition=position;
                    if(child.GetComponent<TMP_Text>()!=null)child.localRotation=Quaternion.Euler(0,180,0);
                }
        }
        static TMP_Text WorldText(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize)
        {
            var text=new GameObject(name).AddComponent<TextMeshPro>();text.transform.SetParent(parent,false);text.transform.localPosition=position;text.font=font;text.text=value;
            text.fontSize=fontSize;text.enableAutoSizing=true;text.fontSizeMin=3;text.fontSizeMax=fontSize;text.alignment=TextAlignmentOptions.Center;text.color=new Color(.16f,.11f,.07f);text.rectTransform.sizeDelta=size;text.richText=false;text.raycastTarget=false;return text;
        }
        static GameObject Part(Transform parent,string name,Mesh mesh,Material mat,Vector3 position,Vector3 scale,Vector3 rotation=default)
        {
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;part.transform.localEulerAngles=rotation;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=mat;return part;
        }
        static GameObject SavePrefab(GameObject root,string name){string path=Folder+"/Art/Prefabs/"+name+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;}
        static Mesh SaveMesh(string name,Mesh mesh){mesh.name=name;mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Art/Meshes/"+name+".asset");return mesh;}
        static Mesh Prism(int n)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float[] radius={.44f,.5f,.5f,.43f};float[] y={0,.055f,.35f,.43f};
            for(int row=0;row<4;row++)for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vertices.Add(new Vector3(Mathf.Cos(a)*radius[row],y[row],Mathf.Sin(a)*radius[row]));uv.Add(new Vector2(i/(float)n,row/3f));}
            for(int row=0;row<3;row++)for(int i=0;i<n;i++){int a=row*n+i,b=row*n+(i+1)%n,c=a+n,d=b+n;triangles.AddRange(new[]{a,c,b,b,c,d});}
            vertices.Add(new Vector3(0,.43f,0));uv.Add(Vector2.one*.5f);int center=vertices.Count-1;
            for(int i=0;i<n;i++)triangles.AddRange(new[]{center,3*n+(i+1)%n,3*n+i});
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);return mesh;
        }
        static Mesh Ring(int n)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int i=0;i<n;i++)for(int r=0;r<2;r++){float a=i*Mathf.PI*2/n;float radius=r==0?.5f:.46f;vertices.Add(new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));uv.Add(new Vector2(i/(float)n,r));}
            for(int i=0;i<n;i++){int a=i*2,b=(i+1)%n*2;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);return mesh;
        }
        static Mesh Crystal()
        {
            var v=new List<Vector3>{new Vector3(0,1,0),new Vector3(0,-.1f,0)};for(int i=0;i<6;i++){float a=i*Mathf.PI/3;v.Add(new Vector3(Mathf.Cos(a)*.45f,.35f,Mathf.Sin(a)*.45f));}
            var indices=new List<int>();for(int i=0;i<6;i++){int a=i+2,b=(i+1)%6+2;indices.AddRange(new[]{0,b,a,1,a,b});}
            var m=new Mesh();m.SetVertices(v);m.SetTriangles(indices,0);m.uv=v.Select(p=>new Vector2(p.x+.5f,p.y)).ToArray();return m;
        }
        static Sprite Sprite(string path)=>AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderByDescending(s=>s.border.sqrMagnitude).First();
        static void PolishMaterials()
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Art/Materials/Archive_WeatheredStone_Vertex.mat");
            stone.shader=source.shader;stone.CopyPropertiesFromMaterial(source);stone.SetTexture("_BaseMap",Texture2D.whiteTexture);stone.SetFloat("_VertexColor",0);stone.SetColor("_BaseColor",new Color(.56f,.59f,.53f));stone.SetFloat("_MossAmount",.08f);
            wood.SetTexture("_BaseMap",Texture2D.whiteTexture);wood.SetColor("_Tint",new Color(.48f,.28f,.12f));
            paper.shader=Shader.Find("Universal Render Pipeline/Unlit");paper.SetColor("_BaseColor",new Color(.86f,.80f,.65f));
            EditorUtility.SetDirty(stone);EditorUtility.SetDirty(wood);EditorUtility.SetDirty(paper);
        }
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish Topic Kit Readability")]
        public static void Polish()
        {
            RequireSaved();string materials=Folder+"/Art/Materials/";
            stone=AssetDatabase.LoadAssetAtPath<Material>(materials+"Archive_Topic_Stone.mat");wood=AssetDatabase.LoadAssetAtPath<Material>(materials+"Archive_Topic_Wood.mat");paper=AssetDatabase.LoadAssetAtPath<Material>(materials+"Archive_Topic_ReadingFace.mat");PolishMaterials();
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            labelMaterial=AssetDatabase.LoadAssetAtPath<Material>(materials+"Archive_Topic_WorldLabels.mat");
            if(labelMaterial==null){labelMaterial=new Material(font.material){name="Archive_Topic_WorldLabels"};labelMaterial.SetColor("_OutlineColor",new Color(.10f,.07f,.035f));labelMaterial.SetFloat("_OutlineWidth",.16f);AssetDatabase.CreateAsset(labelMaterial,materials+"Archive_Topic_WorldLabels.mat");}
            var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp);
            padMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");teal=AssetDatabase.LoadAssetAtPath<Material>(materials+"Archive_Topic_TealInlay.mat");
            truthToken=ActivityToken("Archive_TruthToken",0);numberToken=ActivityToken("Archive_NumberBlock",1);wordToken=ActivityToken("Archive_WordTile",2);
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Folder+"/Art/Prefabs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var station=root.GetComponent<ArchiveTopicStation>();if(station==null)continue;
                    if(station.topic==1)station.tokenPrefab=truthToken;else if(station.topic==2)station.tokenPrefab=wordToken;else if(station.topic==6)station.tokenPrefab=numberToken;
                    station.labelMaterial=labelMaterial;station.board.fontSizeMax=7;station.board.fontSizeMin=3;station.board.fontSize=7;station.board.text="BOOK STATUE\nInteract to begin";
                    station.title.rectTransform.sizeDelta=new Vector2(9,1.1f);station.title.fontSizeMax=6;station.title.fontSize=6;station.title.fontSizeMin=3;station.title.fontSharedMaterial=labelMaterial;station.title.color=new Color(1,.94f,.78f);station.title.transform.localPosition=new Vector3(0,4.85f,10.1f);
                    if(root.transform.Find("Topic_Header_Backplate")==null)Part(root.transform,"Topic_Header_Backplate",cube,wood,new Vector3(0,4.85f,10.22f),new Vector3(9.5f,1.25f,.16f));
                    OrientBoard(root);
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(var material in new[]{stone,wood,paper,labelMaterial})if(material!=null)AssetDatabase.SaveAssetIfDirty(material);
        }
        static void RequireSaved()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene edits first.");
        }
    }
}
