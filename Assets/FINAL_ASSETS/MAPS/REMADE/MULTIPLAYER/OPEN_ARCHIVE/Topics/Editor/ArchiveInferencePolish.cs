using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Reusable T3 art only. No environment rebuild, navigation edits, or new collision.</summary>
    public static class ArchiveInferencePolish
    {
        const string Folder=ArchiveTopicSetup.Folder,Prefabs=Folder+"/Art/Prefabs/",Kit=Prefabs+"Archive_T03_Inference_Constellation.prefab";
        static Mesh cube,octagon,ring,crystal;static TMP_FontAsset font;
        static Material stone,gold,wood,labels,ink,blue,parchment,crystalSurface;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish T3 Inference Constellation")]
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save existing scene changes first.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");crystal=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_RuleCrystal.asset");
            stone=Mat("Archive_Topic_Stone");gold=Mat("Archive_Topic_BrassInlay");wood=Mat("Archive_Topic_Wood");labels=Mat("Archive_Topic_WorldLabels");
            ink=ColorMat("Archive_T3_NightSlate",new Color(.055f,.16f,.20f));blue=ColorMat("Archive_T3_Starlight",new Color(.36f,.77f,.94f));parchment=ColorMat("Archive_T3_WordParchment",new Color(.96f,.86f,.64f));
            crystalSurface=Mat("Archive_T3_CrystalAzure");if(crystalSurface==null){crystalSurface=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Archive_T3_CrystalAzure",enableInstancing=true};AssetDatabase.CreateAsset(crystalSurface,Folder+"/Art/Materials/Archive_T3_CrystalAzure.mat");}
            crystalSurface.SetColor("_BaseColor",new Color(.15f,.58f,.76f));crystalSurface.SetFloat("_Metallic",.22f);crystalSurface.SetFloat("_Smoothness",.6f);crystalSurface.EnableKeyword("_EMISSION");crystalSurface.SetColor("_EmissionColor",new Color(.015f,.07f,.10f));EditorUtility.SetDirty(crystalSurface);AssetDatabase.SaveAssetIfDirty(crystalSurface);
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            var word=WordTablet();var rule=RuleCrystal();var stand=Stand();var socket=Socket(false);var altar=Socket(true);
            var kit=PrefabUtility.LoadPrefabContents(Kit);
            try{
                var before=kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson).ToArray();
                foreach(var old in kit.transform.Cast<Transform>().Where(t=>t.name=="T3_Visual_Identity"||t.name=="Mathematical_Inlay").ToArray())Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T3_Visual_Identity").transform;root.SetParent(kit.transform,false);
                var art=kit.GetComponent<ArchiveInferencePresentation>();if(art==null)art=kit.AddComponent<ArchiveInferencePresentation>();
                art.wordTabletPrefab=word;art.ruleCrystalPrefab=rule;art.standPrefab=stand;art.socketPrefab=socket;art.altarPrefab=altar;
                art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");
                art.takeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Arrow_Cast_01.wav");art.placeSound=art.takeSound;
                art.incorrectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Electric_Hit_01.wav");art.sparkMesh=crystal;art.sparkMaterial=Mat("Archive_T1_Sparks");
                art.progressStars=new Renderer[5];
                for(int i=0;i<5;i++){
                    float a=(90+i*72)*Mathf.Deg2Rad;Vector3 p=new Vector3(Mathf.Cos(a)*3.7f,.065f,Mathf.Sin(a)*3.7f);
                    float b=(90+(i+2)*72)*Mathf.Deg2Rad;Vector3 end=new Vector3(Mathf.Cos(b)*3.7f,.065f,Mathf.Sin(b)*3.7f);var delta=end-p;
                    // Crossed inlays have separate depth planes; the node rings sit above every line.
                    var line=Part(root,"Constellation_Line_"+i,cube,gold,(p+end)*.5f+Vector3.up*(i*.004f),new Vector3(.026f,.012f,delta.magnitude));line.rotation=Quaternion.LookRotation(delta,Vector3.up);
                    Part(root,"Constellation_Node_"+i,ring,blue,p+Vector3.up*.035f,Vector3.one*.36f);
                    float x=(2-i)*.65f;
                    var star=Part(root,"Solved_Challenge_Star_"+(i+1),crystal,blue,new Vector3(x,.13f,6.25f),new Vector3(.22f,.13f,.22f));art.progressStars[i]=star.GetComponent<Renderer>();
                }
                var instruction=Text(root,"Constellation_Entry_Inlay","PREMISES   →   RULE   →   CONCLUSION",new Vector3(0,.085f,5.5f),new Vector2(7.2f,.55f),3.5f);instruction.transform.localRotation=Quaternion.Euler(90,180,0);instruction.color=new Color(.62f,.45f,.21f);
                var st=kit.GetComponent<ArchiveTopicStation>();st.title.text="T3  RULES OF INFERENCE\nInference Constellation";st.board.text="RESTORE THE CONSTELLATION\nComplete the premises, then choose the rule.\nRead the Book to begin.";
                st.GetComponentInChildren<ArchiveAnimatedBook>().bookRig.localScale=Vector3.one*.75f;
                if(!before.SequenceEqual(kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson)))throw new InvalidOperationException("Existing T3 collision changed.");
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }finally{PrefabUtility.UnloadPrefabContents(kit);}
            Directory.CreateDirectory(Folder+"/Testing/Reports");File.WriteAllText(Folder+"/Testing/Reports/T3AssetSetup.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{checkedUtc=DateTime.UtcNow.ToString("o"),prefab=Kit,challenges=5,stagesPerChallenge=2,timerSeconds=360,collisionsPreserved=true,navigationRebuilt=false,soloAssetsChanged=false},Newtonsoft.Json.Formatting.Indented));
            return "Saved T3 constellation kit and five reusable props. Existing Book collision, scene and navigation preserved.";
        }
        static GameObject WordTablet(){
            var root=new GameObject("Archive_T3_WordTablet");
            Part(root.transform,"Tablet_Brass_Edge",cube,gold,Vector3.zero,new Vector3(1.25f,.16f,.7f));
            Part(root.transform,"Tablet_Parchment_Face",cube,parchment,new Vector3(0,.09f,0),new Vector3(1.12f,.035f,.58f));
            var text=Text(root.transform,"Connector_Word","AND",new Vector3(0,.115f,0),new Vector2(1.02f,.48f),4.7f);text.transform.localRotation=Quaternion.Euler(90,180,0);text.color=new Color(.06f,.20f,.23f);
            return Save(root,"Archive_T3_WordTablet.prefab");
        }
        static GameObject RuleCrystal(){
            var root=new GameObject("Archive_T3_RuleCrystal");
            Part(root.transform,"Crystal_Core",crystal,crystalSurface,new Vector3(0,.10f,0),new Vector3(.60f,.85f,.60f));
            Part(root.transform,"Crystal_Brass_Binding",ring,gold,new Vector3(0,.28f,0),Vector3.one*.62f);
            var t=Text(root.transform,"Rule_Code","MP",new Vector3(0,.07f,.40f),new Vector2(.90f,.42f),3.5f);t.transform.localRotation=Quaternion.Euler(0,180,0);
            return Save(root,"Archive_T3_RuleCrystal.prefab");
        }
        static GameObject Stand(){
            var root=new GameObject("Archive_T3_ConstellationStand");
            // The shared octagon mesh extends from y=0 to .44, rather than being centered like a cube.
            Part(root.transform,"Stand_Stone_Foot",octagon,stone,new Vector3(0,.025f,0),new Vector3(1.7f,.18f,1.7f));
            Part(root.transform,"Stand_Shaft",octagon,stone,new Vector3(0,.10f,0),new Vector3(.55f,1.4f,.55f));
            Part(root.transform,"Star_Tray",octagon,gold,new Vector3(0,.70f,0),new Vector3(1.35f,.20f,1.35f));
            Part(root.transform,"Rule_Name_Backplate",cube,wood,new Vector3(0,.8f,.73f),new Vector3(2.62f,.9f,.075f));
            Part(root.transform,"Rule_Name_Face",cube,ink,new Vector3(0,.8f,.78f),new Vector3(2.50f,.8f,.025f));
            var t=Text(root.transform,"Readable_Rule_Name","HYPOTHETICAL\nSYLLOGISM",new Vector3(0,.8f,.81f),new Vector2(2.36f,.70f),3.6f);t.transform.localRotation=Quaternion.Euler(0,180,0);t.textWrappingMode=TextWrappingModes.Normal;
            return Save(root,"Archive_T3_ConstellationStand.prefab");
        }
        static GameObject Socket(bool altar){
            var root=new GameObject(altar?"Archive_T3_RuleAltar":"Archive_T3_PremiseSocket");float size=altar?2.6f:1.8f;
            Part(root.transform,"Socket_Stone",octagon,stone,Vector3.zero,new Vector3(size,.12f,size));
            Part(root.transform,"Socket_Energy",ring,blue,new Vector3(0,.08f,0),Vector3.one*(size-.18f));
            if(altar)Part(root.transform,"Altar_Inner_Circle",ring,gold,new Vector3(0,.085f,0),Vector3.one*1.55f);
            var t=Text(root.transform,"Number_Or_Altar",altar?"RULE ALTAR":"BLANK 1",new Vector3(0,.095f,altar?1.55f:1.05f),new Vector2(altar?3.2f:2.0f,.6f),3.5f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.07f,.28f,.30f);
            return Save(root,altar?"Archive_T3_RuleAltar.prefab":"Archive_T3_PremiseSocket.prefab");
        }
        static GameObject Save(GameObject root,string file){var asset=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+file);Object.DestroyImmediate(root);return asset;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color){var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize){
            var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;t.font=font;t.fontSharedMaterial=labels;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMax=fontSize;t.fontSizeMin=fontSize-1;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;
        }
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;}
    }
}
