using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Authors only the reserved T6 kit and its reusable props, retaining island geometry and navigation.</summary>
    public static class ArchiveInductionPolish
    {
        const string Folder=ArchiveTopicSetup.Folder,Prefabs=Folder+"/Art/Prefabs/",Kit=Prefabs+"Archive_T06_Induction_Court.prefab";
        static Mesh cube,octagon,ring;static TMP_FontAsset font;static Material stone,gold,wood,paper,labels,ink,energy;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish T6 Induction Court")]
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save existing scene changes first.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");
            stone=Mat("Archive_Topic_Stone");gold=Mat("Archive_Topic_BrassInlay");wood=Mat("Archive_Topic_Wood");paper=Mat("Archive_Book_Paper");labels=Mat("Archive_Topic_WorldLabels");font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            ink=ColorMat("Archive_T6_InductionInk",new Color(.045f,.13f,.19f));energy=ColorMat("Archive_T6_InductionAzure",new Color(.28f,.76f,.85f));
            var scroll=Scroll();var stand=Stand();var altar=Altar(false);var socket=Altar(true);var link=Link();
            var kit=PrefabUtility.LoadPrefabContents(Kit);
            try{
                var before=kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson).ToArray();
                foreach(var old in kit.transform.Cast<Transform>().Where(t=>t.name=="T6_Visual_Identity"||t.name=="Mathematical_Inlay"||t.name=="Induction_Altar_Seal").ToArray())Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T6_Visual_Identity").transform;root.SetParent(kit.transform,false);
                foreach(float x in new[]{-2.15f,2.15f})Part(root,"Induction_Chain_Border",cube,gold,new Vector3(x,.065f,-2.1f),new Vector3(.035f,.012f,8.3f));
                var entry=Text(root,"Induction_Entry_Inscription","P(1)     →     P(k)     →     P(k + 1)",new Vector3(0,.09f,5.75f),new Vector2(7.6f,.6f),3.6f);entry.transform.localRotation=Quaternion.Euler(90,180,0);entry.color=new Color(.12f,.35f,.40f);
                var art=kit.GetComponent<ArchiveInductionPresentation>();if(art==null)art=kit.AddComponent<ArchiveInductionPresentation>();art.scrollPrefab=scroll;art.standPrefab=stand;art.altarPrefab=altar;art.stepSocketPrefab=socket;art.linkPrefab=link;
                art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");art.takeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Arrow_Cast_01.wav");art.placeSound=art.takeSound;art.incorrectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Electric_Hit_01.wav");art.sparkMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_RuleCrystal.asset");art.sparkMaterial=Mat("Archive_T1_Sparks");
                art.progressMarks=new Renderer[5];for(int i=0;i<5;i++){float x=(2-i)*.65f;Part(root,"Induction_Progress_Brass_"+i,octagon,gold,new Vector3(x,.07f,6.4f),new Vector3(.48f,.07f,.48f));art.progressMarks[i]=Part(root,"Induction_Progress_Seal_"+(i+1),octagon,energy,new Vector3(x,.11f,6.4f),new Vector3(.33f,.025f,.33f)).GetComponent<Renderer>();}
                var st=kit.GetComponent<ArchiveTopicStation>();st.title.text="T6  MATHEMATICAL INDUCTION\nInduction Court";st.board.text="THE INDUCTION COURT\nP(1) · P(k) · P(k + 1)\nRead the Book to begin.";st.GetComponentInChildren<ArchiveAnimatedBook>().bookRig.localScale=Vector3.one*.75f;
                if(!before.SequenceEqual(kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson)))throw new InvalidOperationException("Existing T6 collision changed.");
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }finally{PrefabUtility.UnloadPrefabContents(kit);}
            return "Saved T6 Induction Court and five reusable props; existing scene, collision and navigation preserved.";
        }
        static GameObject Scroll(){
            var root=new GameObject("Archive_T6_FormulaScroll");Part(root.transform,"Formula_Parchment",cube,paper,new Vector3(0,.11f,0),new Vector3(1.05f,.06f,.70f));
            foreach(float z in new[]{-.38f,.38f}){var roll=Part(root.transform,"Formula_Scroll_Roller",octagon,wood,new Vector3(.58f,.12f,z),new Vector3(.17f,2.64f,.17f));roll.localRotation=Quaternion.Euler(0,0,90);}
            foreach(float x in new[]{-.4f,.4f})Part(root.transform,"Formula_Illumination",cube,gold,new Vector3(x,.15f,0),new Vector3(.025f,.012f,.55f));
            var t=Text(root.transform,"Formula_ID","A",new Vector3(0,.15f,0),new Vector2(.7f,.55f),5.4f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.05f,.25f,.3f);return Save(root,"Archive_T6_FormulaScroll.prefab");
        }
        static GameObject Stand(){
            var root=new GameObject("Archive_T6_FormulaStand");Part(root.transform,"Stand_Foot",octagon,stone,new Vector3(0,.025f,0),new Vector3(1.65f,.18f,1.65f));Part(root.transform,"Stand_Shaft",octagon,stone,new Vector3(0,.1f,0),new Vector3(.55f,1.25f,.55f));Part(root.transform,"Scroll_Tray",octagon,gold,new Vector3(0,.64f,0),new Vector3(1.35f,.2f,1.35f));
            Part(root.transform,"Formula_Plaque_Rim",cube,wood,new Vector3(0,.72f,.70f),new Vector3(2.55f,.8f,.075f));Part(root.transform,"Formula_Plaque_Face",cube,ink,new Vector3(0,.72f,.75f),new Vector3(2.43f,.70f,.025f));
            var t=Text(root.transform,"Formula_Name","Formula A",new Vector3(0,.72f,.78f),new Vector2(2.28f,.62f),3.6f);t.transform.localRotation=Quaternion.Euler(0,180,0);return Save(root,"Archive_T6_FormulaStand.prefab");
        }
        static GameObject Altar(bool step){
            var root=new GameObject(step?"Archive_T6_InductiveStepSocket":"Archive_T6_ProofAltar");float size=step?1.8f:3.15f;
            Part(root.transform,"Proof_Anchor_Stone",octagon,stone,Vector3.zero,new Vector3(size,.16f,size));Part(root.transform,"Socket_Energy",ring,energy,new Vector3(0,.09f,0),Vector3.one*(size-.22f));
            Vector3 labelPos=step?new Vector3(-1.75f,.105f,0):new Vector3(0,.105f,1.8f);var t=Text(root.transform,"Proof_Anchor_Name",step?"STEP 1":"HYPOTHESIS · P(k)",labelPos,new Vector2(step?1.9f:4.1f,.65f),3.3f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.05f,.25f,.3f);
            return Save(root,step?"Archive_T6_InductiveStepSocket.prefab":"Archive_T6_ProofAltar.prefab");
        }
        static GameObject Link(){
            var root=new GameObject("Archive_T6_InductionLink");Part(root.transform,"Inductive_Line",cube,energy,new Vector3(0,.005f,.1f),new Vector3(.065f,.015f,.55f));
            foreach(float side in new[]{-1f,1f}){var arrow=Part(root.transform,"Induction_Direction",cube,gold,new Vector3(side*.10f,.025f,-.12f),new Vector3(.045f,.015f,.32f));arrow.localRotation=Quaternion.Euler(0,side*40,0);}
            return Save(root,"Archive_T6_InductionLink.prefab");
        }
        static GameObject Save(GameObject root,string file){var asset=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+file);Object.DestroyImmediate(root);return asset;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color){var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize){var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;t.font=font;t.fontSharedMaterial=labels;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMax=fontSize;t.fontSizeMin=Mathf.Max(1.8f,fontSize-2);t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;}
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;}
    }
}
