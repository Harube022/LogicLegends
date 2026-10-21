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
    /// <summary>Authors the T5 Proof Forge kit without rebuilding the island or changing collision.</summary>
    public static class ArchiveMethodPolish
    {
        const string Folder=ArchiveTopicSetup.Folder,Prefabs=Folder+"/Art/Prefabs/",Kit=Prefabs+"Archive_T05_Proof_Forge.prefab";
        static Mesh cube,octagon,ring;static TMP_FontAsset font;static Material stone,gold,wood,paper,labels,ink,energy;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish T5 Proof Forge")]
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save existing scene changes first.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");
            stone=Mat("Archive_Topic_Stone");gold=Mat("Archive_Topic_BrassInlay");wood=Mat("Archive_Topic_Wood");paper=Mat("Archive_Book_Paper");labels=Mat("Archive_Topic_WorldLabels");font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            ink=ColorMat("Archive_T5_ForgeInk",new Color(.15f,.095f,.045f));energy=ColorMat("Archive_T5_ForgeAmber",new Color(1,.69f,.26f));
            var tablet=Tablet();var stand=Stand();var socket=Socket();var altar=Altar();
            var kit=PrefabUtility.LoadPrefabContents(Kit);
            try{
                var before=kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson).ToArray();
                foreach(var old in kit.transform.Cast<Transform>().Where(t=>t.name=="T5_Visual_Identity"||t.name=="Mathematical_Inlay").ToArray())Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T5_Visual_Identity").transform;root.SetParent(kit.transform,false);
                foreach(float x in new[]{-2.0f,2.0f})Part(root,"Forged_Proof_Inlay",cube,gold,new Vector3(x,.065f,-2),new Vector3(.035f,.012f,8.6f));
                var entry=Text(root,"Forge_Entry_Inscription","ASSUME   →   REASON   →   PROVE",new Vector3(0,.09f,5.75f),new Vector2(7,.6f),3.6f);entry.transform.localRotation=Quaternion.Euler(90,180,0);entry.color=new Color(.56f,.35f,.12f);
                var art=kit.GetComponent<ArchiveMethodPresentation>();if(art==null)art=kit.AddComponent<ArchiveMethodPresentation>();art.tabletPrefab=tablet;art.standPrefab=stand;art.socketPrefab=socket;art.methodAltarPrefab=altar;
                art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");art.takeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Arrow_Cast_01.wav");art.placeSound=art.takeSound;art.incorrectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Electric_Hit_01.wav");art.sparkMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_RuleCrystal.asset");art.sparkMaterial=Mat("Archive_T1_Sparks");
                art.progressMarks=new Renderer[5];for(int i=0;i<5;i++){float x=(2-i)*.65f;Part(root,"Forged_Progress_Brass_"+i,octagon,gold,new Vector3(x,.07f,6.4f),new Vector3(.48f,.07f,.48f));art.progressMarks[i]=Part(root,"Forged_Progress_Seal_"+(i+1),octagon,energy,new Vector3(x,.11f,6.4f),new Vector3(.33f,.025f,.33f)).GetComponent<Renderer>();}
                var st=kit.GetComponent<ArchiveTopicStation>();st.title.text="T5  DIRECT & INDIRECT PROOFS\nProof Forge";st.board.text="THE PROOF FORGE\nDirect · Contrapositive · Contradiction\nRead the Book to begin.";st.GetComponentInChildren<ArchiveAnimatedBook>().bookRig.localScale=Vector3.one*.75f;
                if(!before.SequenceEqual(kit.GetComponentsInChildren<Collider>(true).Select(EditorJsonUtility.ToJson)))throw new InvalidOperationException("Existing T5 collision changed.");
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }finally{PrefabUtility.UnloadPrefabContents(kit);}
            return "Saved T5 Proof Forge and four reusable props. Existing collision, scene and navigation preserved.";
        }
        static GameObject Tablet(){
            var root=new GameObject("Archive_T5_ProofTablet");Part(root.transform,"Brass_Tablet_Frame",cube,gold,new Vector3(0,.12f,0),new Vector3(1.14f,.18f,.82f));Part(root.transform,"Carved_Reading_Surface",cube,stone,new Vector3(0,.23f,0),new Vector3(1.02f,.06f,.70f));
            foreach(float x in new[]{-.43f,.43f})Part(root.transform,"Tablet_Binding",cube,gold,new Vector3(x,.275f,0),new Vector3(.045f,.018f,.56f));
            var t=Text(root.transform,"Tablet_ID","A",new Vector3(0,.275f,0),new Vector2(.75f,.55f),5.4f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.18f,.10f,.035f);return Save(root,"Archive_T5_ProofTablet.prefab");
        }
        static GameObject Stand(){
            var root=new GameObject("Archive_T5_TabletStand");Part(root.transform,"Stand_Foot",octagon,stone,new Vector3(0,.025f,0),new Vector3(1.65f,.18f,1.65f));Part(root.transform,"Stand_Shaft",octagon,stone,new Vector3(0,.10f,0),new Vector3(.55f,1.25f,.55f));Part(root.transform,"Tablet_Tray",octagon,gold,new Vector3(0,.64f,0),new Vector3(1.35f,.20f,1.35f));
            Part(root.transform,"Tablet_Plaque_Rim",cube,wood,new Vector3(0,.72f,.70f),new Vector3(2.55f,.8f,.075f));Part(root.transform,"Tablet_Plaque_Face",cube,ink,new Vector3(0,.72f,.75f),new Vector3(2.43f,.70f,.025f));
            var t=Text(root.transform,"Readable_Tablet_Name","Tablet A",new Vector3(0,.72f,.78f),new Vector2(2.28f,.62f),3.6f);t.transform.localRotation=Quaternion.Euler(0,180,0);return Save(root,"Archive_T5_TabletStand.prefab");
        }
        static GameObject Socket(){
            var root=new GameObject("Archive_T5_ProofSocket");Part(root.transform,"Answer_Stone",octagon,stone,Vector3.zero,new Vector3(1.7f,.16f,1.4f));Part(root.transform,"Socket_Energy",ring,energy,new Vector3(0,.09f,0),new Vector3(1.5f,1,1.2f));
            var t=Text(root.transform,"Socket_Name","STEP 1",new Vector3(-1.42f,.105f,0),new Vector2(1.25f,.5f),3.2f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.33f,.19f,.06f);return Save(root,"Archive_T5_ProofSocket.prefab");
        }
        static GameObject Altar(){
            var root=new GameObject("Archive_T5_MethodAltar");Part(root.transform,"Method_Base",octagon,stone,Vector3.zero,new Vector3(3.25f,.18f,2.2f));Part(root.transform,"Socket_Energy",ring,energy,new Vector3(0,.10f,0),new Vector3(3.0f,1,1.95f));
            Part(root.transform,"Method_Plaque_Rim",cube,wood,new Vector3(0,.68f,-.70f),new Vector3(3.9f,1.05f,.10f));Part(root.transform,"Method_Plaque_Face",cube,ink,new Vector3(0,.68f,-.63f),new Vector3(3.78f,.94f,.035f));
            var label=Text(root.transform,"Method_Name","CONTRAPOSITIVE",new Vector3(0,.68f,-.60f),new Vector2(3.6f,.82f),3.5f);label.transform.localRotation=Quaternion.Euler(0,180,0);
            var sigil=Text(root.transform,"Method_Sigil","¬Q → ¬P",new Vector3(0,.12f,.20f),new Vector2(2.9f,.7f),4.6f);sigil.transform.localRotation=Quaternion.Euler(90,180,0);sigil.color=new Color(.38f,.22f,.07f);return Save(root,"Archive_T5_MethodAltar.prefab");
        }
        static GameObject Save(GameObject root,string file){var asset=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+file);Object.DestroyImmediate(root);return asset;}
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color){var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize){var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;t.font=font;t.fontSharedMaterial=labels;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMax=fontSize;t.fontSizeMin=Mathf.Max(1.8f,fontSize-2);t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;}
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;}
    }
}
