using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveBookAuthoring
    {
        const string Folder=ArchiveTopicSetup.Folder;
        const string Path=Folder+"/Art/Prefabs/Archive_OpenBook_Lectern.prefab";
        static Mesh cube,octagon;
        static Material stone,brass,leather,paper;
        static TMP_FontAsset font;
        [MenuItem("Tools/Logic Legends/Open Archive/T1/Rebuild Animated Book Statue")]
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode before updating the shared Book asset.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");
            stone=Mat("Archive_Topic_Stone");brass=Mat("Archive_Topic_BrassInlay");
            leather=ColorMat("Archive_Book_Leather",new Color(.065f,.23f,.22f));paper=ColorMat("Archive_Book_Paper",new Color(.91f,.82f,.60f));
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            var root=PrefabUtility.LoadPrefabContents(Path);
            try
            {
                while(root.transform.childCount>0)Object.DestroyImmediate(root.transform.GetChild(0).gameObject);
                var book=root.GetComponent<ArchiveAnimatedBook>();if(book==null)book=root.AddComponent<ArchiveAnimatedBook>();
                Part(root.transform,"Carved_Octagonal_Foot",octagon,stone,Vector3.zero,new Vector3(1.25f,.42f,1.25f));
                Part(root.transform,"Pillar",cube,stone,new Vector3(0,.48f,0),new Vector3(.57f,1.05f,.57f));
                Part(root.transform,"Pillar_Gold_Band",cube,brass,new Vector3(0,.84f,0),new Vector3(.60f,.045f,.60f));
                Part(root.transform,"Lectern_Cap",cube,stone,new Vector3(0,1.04f,0),new Vector3(1.43f,.16f,1.05f));
                book.bookRig=Node(root.transform,"Animated_Archive_Book",new Vector3(0,1.18f,0));
                Part(book.bookRig,"Bottom_Leather_Cover",cube,leather,Vector3.zero,new Vector3(1.23f,.08f,.96f));
                Part(book.bookRig,"Bound_Page_Block",cube,paper,new Vector3(0,.13f,0),new Vector3(1.10f,.17f,.82f));
                Part(book.bookRig,"Leather_Spine",cube,leather,new Vector3(-.60f,.115f,0),new Vector3(.09f,.25f,.96f));
                for(int i=0;i<5;i++)
                {
                    Part(book.bookRig,"Fine_Page_Edge_"+i,cube,brass,new Vector3(.557f,.075f+i*.029f,0),new Vector3(.004f,.0025f,.79f));
                    Part(book.bookRig,"Spine_Binding_"+i,cube,brass,new Vector3(-.651f,.12f,(i-2)*.18f),new Vector3(.012f,.24f,.026f));
                }
                book.coverHinge=Node(book.bookRig,"Opening_Cover_Hinge",new Vector3(-.58f,.245f,0));
                Part(book.coverHinge,"Top_Leather_Cover",cube,leather,new Vector3(.58f,.035f,0),new Vector3(1.23f,.08f,.96f));
                Part(book.coverHinge,"Inner_Parchment",cube,paper,new Vector3(.58f,-.012f,0),new Vector3(1.10f,.015f,.82f));
                for(int side=-1;side<=1;side+=2)
                {
                    Part(book.coverHinge,"Cover_Gilt_Long_"+side,cube,brass,new Vector3(.58f,.08f,side*.40f),new Vector3(1.06f,.009f,.018f));
                    Part(book.coverHinge,"Cover_Gilt_Short_"+side,cube,brass,new Vector3(.58f+side*.52f,.08f,0),new Vector3(.018f,.009f,.80f));
                    for(int end=-1;end<=1;end+=2)Part(book.coverHinge,"Brass_Corner_"+side+"_"+end,cube,brass,new Vector3(.58f+side*.52f,.086f,end*.40f),new Vector3(.12f,.016f,.12f));
                }
                Text(book.coverHinge,"Closed_Cover_Title","THE\nARCHIVE",new Vector3(.58f,.087f,.01f),new Vector2(.85f,.46f),3.2f,brass.color);
                Text(book.bookRig,"Right_Page_Logic","P   Q\n∧  ∨  ⊕\n→  ↔",new Vector3(.03f,.222f,0),new Vector2(.90f,.70f),2.9f,new Color(.13f,.29f,.27f));
                var inner=Text(book.coverHinge,"Left_Page_Logic","LOGIC\nP → Q\nQ ↔ P",new Vector3(.58f,-.023f,0),new Vector2(.9f,.7f),2.7f,new Color(.13f,.29f,.27f));inner.localRotation=Quaternion.Euler(-90,0,0);
                var page=Node(book.bookRig,"Turning_Parchment_Sheet",Vector3.zero);book.turningPage=page.gameObject.AddComponent<MeshFilter>();
                var renderer=page.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=paper;renderer.shadowCastingMode=ShadowCastingMode.Off;
                book.openingSeconds=.65f;book.pageSeconds=2.4f;book.hoverHeight=.38f;
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();return "Shared Book Statue saved: closed leather cover, gilded binding, floating opening and curled page turns; original collision retained.";
        }
        static Transform Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize,Color color)
        {
            var text=new GameObject(name).AddComponent<TextMeshPro>();text.transform.SetParent(parent,false);text.transform.localPosition=position;text.transform.localRotation=Quaternion.Euler(90,0,0);
            text.rectTransform.sizeDelta=size;text.font=font;text.fontSize=fontSize;text.text=value;text.alignment=TextAlignmentOptions.Center;text.color=color;text.raycastTarget=false;
            text.enableAutoSizing=true;text.fontSizeMin=.45f;text.fontSizeMax=fontSize;text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Truncate;return text.transform;
        }
        static Transform Node(Transform parent,string name,Vector3 position){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=position;return t;}
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale)
        {
            var t=Node(parent,name,position);t.localScale=scale;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;return t;
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color)
        {
            var material=Mat(name);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(material,Folder+"/Art/Materials/"+name+".mat");}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.2f);EditorUtility.SetDirty(material);return material;
        }
    }
}
