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
    /// <summary>Authored T2 visual assets only. Preserves the scene, existing Book collision and navigation.</summary>
    public static class ArchiveTruthPolish
    {
        const string Folder=ArchiveTopicSetup.Folder;
        const string Prefabs=Folder+"/Art/Prefabs/";
        const string Kit=Prefabs+"Archive_T02_Truth_Mosaic.prefab";
        static Mesh cube,octagon,ring;static TMP_FontAsset font;
        static Material stone,gold,wood,labels,ink,jade,amber;
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Polish T2 Truth Mosaic")]
        public static string Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save existing scene changes first.");
            var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(primitive);
            octagon=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_BeveledOctagon.asset");ring=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_InlayRing.asset");
            stone=Mat("Archive_Topic_Stone");gold=Mat("Archive_Topic_BrassInlay");wood=Mat("Archive_Topic_Wood");labels=Mat("Archive_Topic_WorldLabels");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/RulesOfInferenceUI/InferenceBody.asset");
            ink=ColorMat("Archive_T2_DeepTeal",new Color(.055f,.18f,.18f));jade=ColorMat("Archive_T2_TrueJade",new Color(.22f,.76f,.59f));amber=ColorMat("Archive_T2_FalseAmber",new Color(.96f,.66f,.27f));
            var tile=CreateTile();var socket=CreateSocket();var stand=CreateStand();
            var kit=PrefabUtility.LoadPrefabContents(Kit);
            try{
                var beforeColliders=kit.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)).ToArray();
                foreach(var old in kit.transform.Cast<Transform>().Where(t=>t.name=="Truth_Mosaic_Tile"||t.name=="T2_Visual_Identity").ToArray())Object.DestroyImmediate(old.gameObject);
                var root=new GameObject("T2_Visual_Identity").transform;root.SetParent(kit.transform,false);
                var art=kit.GetComponent<ArchiveTruthPresentation>();if(art==null)art=kit.AddComponent<ArchiveTruthPresentation>();
                art.truthTilePrefab=tile;art.rowSocketPrefab=socket;art.standPrefab=stand;
                art.correctSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Graph/Correct.wav");
                art.takeSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Ice_Arrow_Cast_01.wav");
                art.placeSound=art.takeSound;art.incorrectSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets/Sounds Effects/Small Sound Kit/Magic/Mgc_Electric_Hit_01.wav");
                art.sparkMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Art/Meshes/Archive_RuleCrystal.asset");art.sparkMaterial=Mat("Archive_T1_Sparks");
                for(int row=0;row<4;row++){
                    float z=ArchiveTruthPresentation.RowPosition(row).z;
                    Part(root,"Truth_Row_Stone_"+(row+1),cube,stone,new Vector3(.4f,.04f,z),new Vector3(9.25f,.035f,1.9f));
                    Part(root,"Row_Leading_Inlay_"+row,cube,gold,new Vector3(.4f,.067f,z+.91f),new Vector3(9.1f,.012f,.035f));
                    foreach(float x in new[]{1.85f,-1.15f})Part(root,"Column_Divider_"+row,cube,gold,new Vector3(x,.07f,z),new Vector3(.035f,.012f,1.72f));
                    for(int col=0;col<2;col++){
                        float x=col==0?3.25f:.35f;
                        var input=Text(root,"Fixed_"+(col==0?"P":"Q")+"_Row_"+(row+1),ArchiveTruthPresentation.RowNames[row][col].ToString(),new Vector3(x,.075f,z),new Vector2(1.7f,1.25f),9);
                        input.transform.localRotation=Quaternion.Euler(90,180,0);input.color=ArchiveTruthPresentation.RowNames[row][col]=='T'?new Color(.20f,.62f,.45f):new Color(.69f,.40f,.15f);
                    }
                    var rowId=Text(root,"Row_Number_"+(row+1),(row+1).ToString(),new Vector3(5.05f,.08f,z),new Vector2(.6f,.9f),4);rowId.transform.localRotation=Quaternion.Euler(90,180,0);
                }
                foreach(var header in new[]{new{label="P",x=3.25f},new{label="Q",x=.35f},new{label="OUTPUT",x=-3.05f}}){
                    var t=Text(root,"Mosaic_Column_"+header.label,header.label,new Vector3(header.x,.08f,-5.35f),new Vector2(2.6f,.8f),5.5f);t.transform.localRotation=Quaternion.Euler(90,180,0);t.color=new Color(.66f,.46f,.19f);
                }
                art.progressMarks=new Renderer[9];
                for(int i=0;i<9;i++){
                    float x=(i-4)*.52f+(i<3?-.15f:i>5?.15f:0);
                    art.progressMarks[i]=Part(root,"Restored_Table_"+(i+1),octagon,jade,new Vector3(x,.08f,6.25f),new Vector3(.32f,.025f,.32f)).GetComponent<Renderer>();
                }
                var st=kit.GetComponent<ArchiveTopicStation>();st.board.text="TRUTH MOSAIC\nP   Q  →  OUTPUT\nRead the Book to begin.";st.title.text="T2  TRUTH TABLE\nTruth Mosaic";
                var book=kit.GetComponentInChildren<ArchiveAnimatedBook>();book.bookRig.localScale=Vector3.one*.75f;
                if(!beforeColliders.SequenceEqual(kit.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c))))throw new InvalidOperationException("T2 collision changed.");
                PrefabUtility.SaveAsPrefabAsset(kit,Kit);
            }finally{PrefabUtility.UnloadPrefabContents(kit);}
            var result=new{checkedUtc=DateTime.UtcNow.ToString("o"),prefab=Kit,rows=4,rounds=9,timerSeconds=360,collisionsPreserved=true,navigationRebuilt=false,soloAssetsChanged=false};
            Directory.CreateDirectory(Folder+"/Testing/Reports");File.WriteAllText(Folder+"/Testing/Reports/T2AssetSetup.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            return "Saved T2 kit, reusable T/F tile, row socket and tile stand. Existing scene and navigation preserved.";
        }
        static GameObject CreateTile(){
            var root=new GameObject("Archive_T2_TruthTile");
            Part(root.transform,"Brass_Tile_Rim",octagon,gold,Vector3.zero,new Vector3(.82f,.13f,.82f));
            Part(root.transform,"Truth_Face",octagon,jade,new Vector3(0,.08f,0),new Vector3(.69f,.025f,.69f));
            var glyph=Text(root.transform,"Truth_Letter","T",new Vector3(0,.105f,0),new Vector2(.62f,.62f),6.2f);glyph.transform.localRotation=Quaternion.Euler(90,180,0);glyph.color=new Color(.025f,.10f,.09f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"Archive_T2_TruthTile.prefab");Object.DestroyImmediate(root);return prefab;
        }
        static GameObject CreateSocket(){
            var root=new GameObject("Archive_T2_RowSocket");
            Part(root.transform,"Socket_Stone",octagon,stone,Vector3.zero,new Vector3(1.62f,.11f,1.62f));
            Part(root.transform,"Output_Inlay",ring,jade,new Vector3(0,.075f,0),new Vector3(1.45f,1,1.45f));
            var label=Text(root.transform,"Row_And_Output","ROW 1 · TT\nOUTPUT",new Vector3(0,.09f,.93f),new Vector2(2.0f,.58f),3.2f);label.transform.localRotation=Quaternion.Euler(90,180,0);label.color=new Color(.11f,.32f,.3f);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"Archive_T2_RowSocket.prefab");Object.DestroyImmediate(root);return prefab;
        }
        static GameObject CreateStand(){
            var root=new GameObject("Archive_T2_TileStand");
            Part(root.transform,"Stand_Foot",octagon,stone,new Vector3(0,.08f,0),new Vector3(1.8f,.16f,1.8f));
            Part(root.transform,"Stand_Column",octagon,stone,new Vector3(0,.40f,0),new Vector3(.6f,.58f,.6f));
            Part(root.transform,"Brass_Tray",octagon,gold,new Vector3(0,.74f,0),new Vector3(1.35f,.1f,1.35f));
            Part(root.transform,"Tile_Stand_Label_Rim",cube,wood,new Vector3(0,.75f,.67f),new Vector3(2.18f,.75f,.07f));
            Part(root.transform,"Tile_Stand_Label_Face",cube,ink,new Vector3(0,.75f,.715f),new Vector3(2.08f,.65f,.035f));
            Text(root.transform,"Take_Truth_Tile_Label","T  TRUE\nTAKE A TILE",new Vector3(0,.75f,.75f),new Vector2(1.98f,.58f),3.5f).transform.localRotation=Quaternion.Euler(0,180,0);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"Archive_T2_TileStand.prefab");Object.DestroyImmediate(root);return prefab;
        }
        static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Art/Materials/"+name+".mat");
        static Material ColorMat(string name,Color color){var m=Mat(name);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name,enableInstancing=true};AssetDatabase.CreateAsset(m,Folder+"/Art/Materials/"+name+".mat");}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;}
        static TMP_Text Text(Transform parent,string name,string value,Vector3 position,Vector2 size,float fontSize){
            var t=new GameObject(name).AddComponent<TextMeshPro>();t.transform.SetParent(parent,false);t.transform.localPosition=position;t.rectTransform.sizeDelta=size;
            t.font=font;t.fontSharedMaterial=labels;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=true;t.fontSizeMax=fontSize;t.fontSizeMin=fontSize-1;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;t.richText=false;t.alignment=TextAlignmentOptions.Center;t.color=new Color(1,.94f,.79f);t.raycastTarget=false;return t;
        }
        static Transform Part(Transform parent,string name,Mesh mesh,Material material,Vector3 position,Vector3 scale){var t=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)).transform;t.SetParent(parent,false);t.localPosition=position;t.localScale=scale;t.GetComponent<MeshFilter>().sharedMesh=mesh;var r=t.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;return t;}
    }
}
