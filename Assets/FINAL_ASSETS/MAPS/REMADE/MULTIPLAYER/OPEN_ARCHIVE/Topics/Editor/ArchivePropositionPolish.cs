using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Idempotent authoring confined to the T1 prefab and multiplayer catalog copy.</summary>
    public static class ArchivePropositionPolish
    {
        const string Folder=ArchiveTopicSetup.Folder;
        const string Kit=Folder+"/Art/Prefabs/Archive_T01_Rune_Choice_Circle.prefab";
        const string Rune=Folder+"/Art/Prefabs/Archive_T1_ChoiceRune.prefab";
        static Mesh cube,octagon,ring;
        static Material stone,wood,gold,ink,energy,labels;
        static TMP_FontAsset font;
        [MenuItem("Tools/Logic Legends/Open Archive/T1/Apply T1 Polish")]
        public static string Apply()
        {
            if(Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring T1.");
            var scene=SceneManager.GetActiveScene();if(scene.path!=ArchiveTopicSetup.ScenePath||scene.isDirty)throw new InvalidOperationException("Open the saved MultiplayerGameplay scene first.");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");
            stone=Mat("Archive_Topic_Stone");wood=Mat("Archive_Topic_Wood");gold=Mat("Archive_Topic_BrassInlay");labels=Mat("Archive_Topic_WorldLabels");
            ink=ColorMat("Archive_T1_LabelFace",new Color(.065f,.17f,.16f));energy=ColorMat("Archive_T1_RuneEnergy",new Color(.16f,.57f,.54f));
            var rune=CreateRune();var kit=PrefabUtility.LoadPrefabContents(Kit);
            try
            {
                var old=kit.transform.Find("T1_Visual_Identity");if(old!=null)Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T1_Visual_Identity").transform;root.SetParent(kit.transform,false);
                var art=kit.GetComponent<ArchivePropositionPresentation>();if(art==null)art=kit.AddComponent<ArchivePropositionPresentation>();
                art.runePrefab=rune;art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");
                Part(root,"Logic_Seal_Outer",ring,gold,new Vector3(0,.06f,0),new Vector3(4.6f,1,4.6f));
                Part(root,"Logic_Seal_Inner",ring,energy,new Vector3(0,.065f,0),new Vector3(3.9f,1,3.9f));
                var operators=World(root,"All_Connectives","∧   ∨   ⊕   →   ↔",new Vector3(0,.08f,0),new Vector2(4.8f,1.0f),6.8f);
                operators.transform.localRotation=Quaternion.Euler(90,0,0);operators.color=new Color(.76f,.58f,.25f);
                // Small perimeter ticks, no collision or raised platform across the chase route.
                for(int i=0;i<16;i++)
                {
                    float a=i*Mathf.PI/8;var p=new Vector3(Mathf.Sin(a)*7.0f,.055f,Mathf.Cos(a)*7.0f);
                    var tick=Part(root,"Seal_Tick_"+i,cube,gold,p,new Vector3(.075f,.012f,.25f));tick.localRotation=Quaternion.Euler(0,i*22.5f,0);
                }
                art.progressMarks=new Renderer[5];
                for(int i=0;i<5;i++)art.progressMarks[i]=Part(root,"Team_Progress_"+(i+1),octagon,energy,new Vector3((i-2)*.75f,.06f,6.2f),new Vector3(.42f,.025f,.42f)).GetComponent<Renderer>();
                var st=kit.GetComponent<ArchiveTopicStation>();st.board.text="READ THE BOOK\nDiscuss with your team.\nChannel one of four answer runes.";
                st.title.text="T1  PROPOSITIONAL LOGIC\nRune Choice Circle";
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }
            finally{PrefabUtility.UnloadPrefabContents(kit);}
            RepairCatalog();
            var station=Object.FindObjectsByType<ArchiveTopicStation>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(s=>s.topic==0);
            AssetDatabase.SaveAssets();Selection.activeGameObject=station.gameObject;
            return "T1 prefab, reusable rune and multiplayer question copy saved for MultiplayerGameplay.";
        }
        static GameObject CreateRune()
        {
            var root=new GameObject("Archive_T1_ChoiceRune");var rune=root.AddComponent<ArchivePropositionRune>();
            Part(root.transform,"Weathered_Rune_Base",octagon,stone,Vector3.zero,new Vector3(2.2f,.15f,2.2f));
            Part(root.transform,"Brass_Rim",ring,gold,new Vector3(0,.09f,0),new Vector3(2.0f,1,2.0f));
            rune.inlay=Part(root.transform,"Interactive_Inlay",ring,energy,new Vector3(0,.10f,0),new Vector3(1.75f,1,1.75f)).GetComponent<Renderer>();
            var arc=new GameObject("Channel_Progress_Arc").AddComponent<LineRenderer>();arc.transform.SetParent(root.transform,false);arc.useWorldSpace=false;arc.sharedMaterial=energy;arc.startWidth=arc.endWidth=.07f;arc.positionCount=0;arc.enabled=false;rune.channelArc=arc;
            rune.labelRoot=new GameObject("Camera_Facing_Label").transform;rune.labelRoot.SetParent(root.transform,false);rune.labelRoot.localPosition=new Vector3(0,1.35f,0);
            rune.labelRoot.localScale=Vector3.one*.58f;
            Part(rune.labelRoot,"Wood_Label_Rim",cube,wood,Vector3.zero,new Vector3(3.9f,1.55f,.06f));
            Part(rune.labelRoot,"Quiet_Teal_Face",cube,ink,new Vector3(0,0,-.04f),new Vector3(3.76f,1.4f,.04f));
            rune.letter=World(rune.labelRoot,"Rune_Letter","A",new Vector3(-1.4f,.30f,-.075f),new Vector2(.65f,.55f),5.4f);
            rune.symbol=World(rune.labelRoot,"Connective_Symbol","∧",new Vector3(-1.4f,-.25f,-.075f),new Vector2(.7f,.55f),5.4f);
            rune.answer=World(rune.labelRoot,"Connective_Name","Conjunction",new Vector3(.43f,.20f,-.075f),new Vector2(2.65f,.64f),4.5f);
            rune.condition=World(rune.labelRoot,"Interaction_State","STAND IN RING",new Vector3(.43f,-.38f,-.075f),new Vector2(2.65f,.40f),3.0f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Rune);Object.DestroyImmediate(root);return prefab;
        }
        static TMP_Text World(Transform parent,string name,string text,Vector3 position,Vector2 size,float fontSize)
        {
            var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;
            t.font=font;t.fontSharedMaterial=labels;t.text=text;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMin=fontSize-1;t.fontSizeMax=fontSize;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;
        }
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
        {
            var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;
            t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color)
        {
            var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}
            m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;
        }
        static void RepairCatalog()
        {
            var c=AssetDatabase.LoadAssetAtPath<ArchiveTopicCatalog>(Folder+"/Archive_Topic_Catalog.asset");
            foreach(var bank in c.propositions)foreach(var q in bank.questions)
            {
                q.options=q.options.Select(s=>s.Trim()).ToArray();
                // Each bank identifies exactly one connective. Correct only the exported multiplayer copy.
                q.correctOptionIndex=Array.IndexOf(q.options,bank.title);
            }
            c.propositions[0].questions[6].questionText="P ___ Q is true only for TT. It is false for TF, FT and FF. Which connective belongs in the blank?";
            c.propositions[1].questions[1].questionText="I combine statements using inclusive OR: either may be true, and both may be true.";
            c.propositions[1].questions[4].questionText="My symbol is ∨. At least one statement must be true; both true is also allowed.";
            c.propositions[1].questions[5].questionText="P ___ Q is true for TT, TF and FT, but false for FF. Which connective belongs in the blank?";
            c.propositions[2].questions[1].questionText="I am true exactly when one statement is true and the other is false. I am false when their truth values match.";
            c.propositions[3].questions[4].questionText="My only false case is P true and Q false. In all three other cases I am true. Which connective am I?";
            EditorUtility.SetDirty(c);AssetDatabase.SaveAssetIfDirty(c);
        }
    }
}
