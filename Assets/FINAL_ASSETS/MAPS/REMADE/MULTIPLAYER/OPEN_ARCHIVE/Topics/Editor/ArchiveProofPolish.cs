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
    /// <summary>Authors only T4's reusable visual kit; preserves collision, the scene and existing navigation.</summary>
    public static class ArchiveProofPolish
    {
        const string Folder=ArchiveTopicSetup.Folder,Prefabs=Folder+"/Art/Prefabs/",Kit=Prefabs+"Archive_T04_Scribes_Court.prefab";
        static Mesh cube,octagon,ring;static TMP_FontAsset font;static Material stone,gold,wood,paper,labels,ink,energy;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish T4 Scribes Court")]
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save existing scene changes first.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");
            stone=Mat("Archive_Topic_Stone");gold=Mat("Archive_Topic_BrassInlay");wood=Mat("Archive_Topic_Wood");paper=Mat("Archive_Book_Paper");labels=Mat("Archive_Topic_WorldLabels");font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            ink=ColorMat("Archive_T4_ReadingInk",new Color(.045f,.17f,.16f));energy=ColorMat("Archive_T4_ScribesJade",new Color(.33f,.78f,.68f));
            var scroll=Manuscript();var seal=RuleSeal();var stand=Stand();var socket=Socket(false);var altar=Socket(true);
            var kit=PrefabUtility.LoadPrefabContents(Kit);
            try{
                var before=kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson).ToArray();
                foreach(var old in kit.transform.Cast<Transform>().Where(t=>t.name=="T4_Visual_Identity"||t.name=="Mathematical_Inlay").ToArray())Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T4_Visual_Identity").transform;root.SetParent(kit.transform,false);
                foreach(float x in new[]{-2.65f,2.65f})Part(root,"Scribes_Proof_Inlay",cube,gold,new Vector3(x,.065f,0),new Vector3(.035f,.012f,10));
                var entry=Text(root,"Scribes_Entry_Inscription","READ   →   REASON   →   JUSTIFY",new Vector3(0,.09f,5.75f),new Vector2(7.0f,.6f),3.6f);entry.transform.localRotation=Quaternion.Euler(90,180,0);entry.color=new Color(.59f,.43f,.19f);
                var art=kit.GetComponent<ArchiveProofPresentation>();if(art==null)art=kit.AddComponent<ArchiveProofPresentation>();art.scrollPrefab=scroll;art.ruleSealPrefab=seal;art.standPrefab=stand;art.socketPrefab=socket;art.classificationAltarPrefab=altar;
                art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");art.takeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Arrow_Cast_01.wav");art.placeSound=art.takeSound;art.incorrectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Electric_Hit_01.wav");art.sparkMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_RuleCrystal.asset");art.sparkMaterial=Mat("Archive_T1_Sparks");
                art.progressMarks=new Renderer[5];for(int i=0;i<5;i++){float x=(2-i)*.65f;Part(root,"Accepted_Proof_Brass_"+i,octagon,gold,new Vector3(x,.07f,6.4f),new Vector3(.48f,.07f,.48f));art.progressMarks[i]=Part(root,"Accepted_Proof_Seal_"+(i+1),octagon,energy,new Vector3(x,.11f,6.4f),new Vector3(.33f,.025f,.33f)).GetComponent<Renderer>();}
                var st=kit.GetComponent<ArchiveTopicStation>();st.title.text="T4  FORMAL & INFORMAL PROOFS\nScribes' Court";st.board.text="THE SCRIBES' COURT\nRead and classify proof scrolls.\nThen order statements and rules.\nRead the Book to begin.";st.GetComponentInChildren<ArchiveAnimatedBook>().bookRig.localScale=Vector3.one*.75f;
                if(!before.SequenceEqual(kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson)))throw new InvalidOperationException("Existing T4 collision changed.");
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }finally{PrefabUtility.UnloadPrefabContents(kit);}
            Directory.CreateDirectory(Folder+"/Testing/Reports");File.WriteAllText(Folder+"/Testing/Reports/T4AssetSetup.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{checkedUtc=DateTime.UtcNow.ToString("o"),prefab=Kit,challenges=5,timerSeconds=360,collisionsPreserved=true,navigationRebuilt=false,soloAssetsChanged=false},Newtonsoft.Json.Formatting.Indented));return "Saved T4 Scribes' Court and five reusable props. Existing scene and navigation preserved.";
        }
        static GameObject Manuscript(){
            var root=new GameObject("Archive_T4_ProofManuscript");Part(root.transform,"Parchment_Face",cube,paper,new Vector3(0,.11f,0),new Vector3(.95f,.06f,.68f));
            foreach(float z in new[]{-.36f,.36f}){var roller=Part(root.transform,"Rolled_Parchment_Edge",octagon,paper,new Vector3(.52f,.12f,z),new Vector3(.18f,2.36f,.18f));roller.localRotation=Quaternion.Euler(0,0,90);}
            var t=Text(root.transform,"Manuscript_ID","A",new Vector3(0,.15f,0),new Vector2(.75f,.55f),5.4f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.05f,.22f,.20f);return Save(root,"Archive_T4_ProofManuscript.prefab");
        }
        static GameObject RuleSeal(){
            var root=new GameObject("Archive_T4_JustificationSeal");Part(root.transform,"Brass_Seal",octagon,gold,Vector3.zero,new Vector3(1.1f,.16f,1.1f));Part(root.transform,"Rule_Reading_Face",octagon,paper,new Vector3(0,.08f,0),new Vector3(.90f,.04f,.90f));
            var t=Text(root.transform,"Rule_ID","A",new Vector3(0,.11f,0),new Vector2(.80f,.52f),4.5f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.05f,.22f,.20f);return Save(root,"Archive_T4_JustificationSeal.prefab");
        }
        static GameObject Stand(){
            var root=new GameObject("Archive_T4_ReadingStand");Part(root.transform,"Stand_Foot",octagon,stone,new Vector3(0,.025f,0),new Vector3(1.65f,.18f,1.65f));Part(root.transform,"Stand_Shaft",octagon,stone,new Vector3(0,.10f,0),new Vector3(.55f,1.25f,.55f));Part(root.transform,"Manuscript_Tray",octagon,gold,new Vector3(0,.64f,0),new Vector3(1.35f,.20f,1.35f));
            Part(root.transform,"Manuscript_Plaque_Rim",cube,wood,new Vector3(0,.72f,.70f),new Vector3(2.55f,.8f,.075f));Part(root.transform,"Manuscript_Plaque_Face",cube,ink,new Vector3(0,.72f,.75f),new Vector3(2.43f,.70f,.025f));
            var t=Text(root.transform,"Readable_Manuscript_Name","Scroll A",new Vector3(0,.72f,.78f),new Vector2(2.28f,.62f),3.6f);t.transform.localRotation=Quaternion.Euler(0,180,0);t.textWrappingMode=TextWrappingModes.Normal;return Save(root,"Archive_T4_ReadingStand.prefab");
        }
        static GameObject Socket(bool classification){
            var root=new GameObject(classification?"Archive_T4_ClassificationAltar":"Archive_T4_ProofSocket");float size=classification?3.8f:1.8f;
            Part(root.transform,"Answer_Stone",octagon,stone,Vector3.zero,new Vector3(size,.16f,size));Part(root.transform,"Socket_Energy",ring,energy,new Vector3(0,.09f,0),Vector3.one*(size-.20f));
            var t=Text(root.transform,"Socket_Name",classification?"FORMAL":"STEP 1",new Vector3(0,.105f,classification?2.18f:1.0f),new Vector2(classification?4.2f:2.15f,.65f),classification?4.8f:3.3f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.05f,.25f,.23f);return Save(root,classification?"Archive_T4_ClassificationAltar.prefab":"Archive_T4_ProofSocket.prefab");
        }
        static GameObject Save(GameObject root,string file){var asset=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+file);Object.DestroyImmediate(root);return asset;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color){var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize){var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;t.font=font;t.fontSharedMaterial=labels;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMax=fontSize;t.fontSizeMin=Mathf.Max(1.8f,fontSize-2);t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;}
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;}
    }
}
