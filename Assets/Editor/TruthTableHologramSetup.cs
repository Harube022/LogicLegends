#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Fits the multiplayer mathematical veil/energy materials to the three pointed table openings.</summary>
public static class TruthTableHologramSetup
{
    public const string Folder="Assets/Art/TruthTableHolograms";
    public const string Multiplayer="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Cinematics/VFX/CrimsonBarrier";
    static readonly Vector2[] Profile={new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),new Vector2(.5f,.29f),new Vector2(0,.5f),new Vector2(-.5f,.29f)};
    public static void Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="TruthTable")throw new InvalidOperationException("Open TruthTable in Edit Mode.");
        if(UnityEngine.Object.FindObjectsByType<TruthTableColumnHologram>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)throw new InvalidOperationException("Holograms already installed; inspect instead of duplicating.");
        EditorSceneManager.SaveScene(scene,"Library/TruthHologramBefore.unity",true);
        EnsureFolder(Folder);EnsureFolder(Folder+"/Materials");EnsureFolder(Folder+"/Meshes");
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Multiplayer+"/Fonts/Crimson_NotoSansMath_SDF.asset");
        foreach(char c in "∧∨¬→↔TF")if(!font.HasCharacter(c))throw new InvalidOperationException("Multiplayer atlas is missing "+c);
        var red=Clone("Crimson_Transparent_Veil","Column_Locked_Red");Palette(red,false);red.SetFloat("_Glow",.85f);red.SetFloat("_Opacity",.045f);red.SetFloat("_InsideOpacity",.03f);
        var green=new Material(red){name="Column_Completed_Green"};Palette(green,true);AssetDatabase.CreateAsset(green,Folder+"/Materials/Column_Completed_Green.mat");
        var line=Clone("Crimson_Energy_Lines","Column_Outline_Energy");line.SetFloat("_Glow",.9f);line.SetFloat("_Cull",0);line.SetVector("_BarrierCenter",new Vector4(0,0,0,100000));
        var glyph=Clone("Crimson_Math_Glyphs","Column_Math_Glyphs");glyph.SetFloat("_Glow",.85f);glyph.SetFloat("_Cull",0);glyph.SetVector("_BarrierCenter",new Vector4(0,0,0,100000));glyph.renderQueue=3011;
        var puzzle=UnityEngine.Object.FindObjectsByType<DynamicLogicPuzzle>(FindObjectsSortMode.None).First(p=>new SerializedObject(p).FindProperty("houseDoors").objectReferenceValue!=null);
        var so=new SerializedObject(puzzle);string before=EditorJsonUtility.ToJson(puzzle);
        var report=new List<object>();
        string[] runes={"∧∨¬","→↔T","F∧↔"};
        var temp=new GameObject("Temporary baked symbol authoring");var label=temp.AddComponent<TextMeshPro>();label.font=font;label.fontSharedMaterial=font.material;label.fontSize=10;label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;label.richText=false;
        try
        {
            for(int i=0;i<3;i++)
            {
                var barrier=(GameObject)so.FindProperty("col"+(i+1)+"Barrier").objectReferenceValue;
                var originalCollider=barrier.GetComponent<Collider>();string collision=EditorJsonUtility.ToJson(originalCollider);bool visible=barrier.activeSelf;
                var surface=Surface();surface.name=barrier.name+" Pointed Veil";AssetDatabase.CreateAsset(surface,Folder+"/Meshes/"+barrier.name+"_Veil.asset");barrier.GetComponent<MeshFilter>().sharedMesh=surface;
                var r=barrier.GetComponent<MeshRenderer>();r.sharedMaterial=red;Configure(r);
                var edgeMesh=Outline(barrier.transform.lossyScale);edgeMesh.name=barrier.name+" Soft Outline";AssetDatabase.CreateAsset(edgeMesh,Folder+"/Meshes/"+barrier.name+"_Outline.asset");
                var edge=Part(barrier.transform,"Soft Pointed Outline",edgeMesh,line);
                var glyphMesh=Glyphs(label,runes[i],barrier.transform.lossyScale);glyphMesh.name=barrier.name+" Discrete Math Symbols";AssetDatabase.CreateAsset(glyphMesh,Folder+"/Meshes/"+barrier.name+"_Glyphs.asset");
                var letters=Part(barrier.transform,"Drifting Math Symbols",glyphMesh,glyph);
                var effect=barrier.AddComponent<TruthTableColumnHologram>();effect.veil=r;effect.outline=edge;effect.symbols=letters;effect.phase=i*1.7f;
                if(EditorJsonUtility.ToJson(originalCollider)!=collision||barrier.activeSelf!=visible)throw new InvalidOperationException("Collision or visibility changed.");
                report.Add(new {barrier.name,colliderPreserved=true,activePreserved=true,triangles=(surface.triangles.Length+edgeMesh.triangles.Length+glyphMesh.triangles.Length)/3,glyphs=runes[i]});
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(temp);}
        // These two references are the only gameplay-component fields changed.
        if(EditorJsonUtility.ToJson(puzzle)!=before)throw new InvalidOperationException("Unexpected puzzle field edit.");
        so.FindProperty("lockedMaterial").objectReferenceValue=red;so.FindProperty("completedMaterial").objectReferenceValue=green;so.ApplyModifiedPropertiesWithoutUndo();
        foreach(var m in new[]{red,green,line,glyph}){m.enableInstancing=true;EditorUtility.SetDirty(m);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("Library/TruthHologramSetup.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {source=Multiplayer,columns=report,particles=0,lights=0,sourceMaterialsUnmodified=true},Newtonsoft.Json.Formatting.Indented));
    }
    static void Palette(Material m,bool green)
    {
        m.SetColor("_DarkRed",green?new Color(.015f,.20f,.08f):new Color(.25f,.015f,.04f));
        m.SetColor("_Crimson",green?new Color(.06f,.72f,.28f):new Color(.9f,.075f,.13f));
        m.SetColor("_Scarlet",green?new Color(.24f,1,.55f):new Color(1,.25f,.30f));
        m.SetColor("_RuneHighlights",green?new Color(.65f,1,.8f):new Color(1,.58f,.48f));
    }
    static Material Clone(string source,string name){var m=new Material(AssetDatabase.LoadAssetAtPath<Material>(Multiplayer+"/Materials/"+source+".mat")){name=name};AssetDatabase.CreateAsset(m,Folder+"/Materials/"+name+".mat");return m;}
    static MeshRenderer Part(Transform parent,string name,Mesh mesh,Material material){var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;Configure(r);return r;}
    static void Configure(MeshRenderer r){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static Mesh Surface()
    {
        var m=new Mesh();m.vertices=Profile.Select(p=>new Vector3(p.x,p.y,.51f)).ToArray();m.uv=Profile.Select(p=>p+Vector2.one*.5f).ToArray();m.normals=Enumerable.Repeat(Vector3.forward,5).ToArray();m.triangles=new[]{0,1,2,0,2,3,0,3,4};m.RecalculateBounds();return m;
    }
    static Mesh Outline(Vector3 scale)
    {
        var v=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
        for(int i=0;i<5;i++)foreach(var style in new[]{new Vector2(.13f,.075f),new Vector2(.035f,.48f)})
        {
            Vector2 a=Vector2.Scale(Profile[i],new Vector2(scale.x,scale.y)),b=Vector2.Scale(Profile[(i+1)%5],new Vector2(scale.x,scale.y));
            var d=(b-a).normalized;var side=new Vector2(-d.y,d.x)*style.x*.5f;int n=v.Count;
            foreach(var p in new[]{a-side,b-side,b+side,a+side}){v.Add(new Vector3(p.x/scale.x,p.y/scale.y,.516f));colors.Add(new Color(1,1,1,style.y));}
            indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        var m=new Mesh();m.SetVertices(v);m.SetColors(colors);m.SetNormals(Enumerable.Repeat(Vector3.forward,v.Count).ToList());m.SetUVs(0,Enumerable.Repeat(Vector2.one*.5f,v.Count).ToList());m.SetUVs(1,Enumerable.Repeat(new Vector2(.3f,0),v.Count).ToList());m.SetTriangles(indices,0);m.RecalculateBounds();return m;
    }
    static Mesh Glyphs(TextMeshPro label,string text,Vector3 scale)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var seeds=new List<Vector2>();var colors=new List<Color>();var triangles=new List<int>();
        var spots=new[]{new Vector2(-.34f,-.29f),new Vector2(.34f,.015f),new Vector2(-.12f,.37f)};
        for(int i=0;i<text.Length;i++)
        {
            label.text=text[i].ToString();label.ForceMeshUpdate();var mesh=label.mesh;var bounds=mesh.bounds;float factor=.48f/Mathf.Max(.001f,bounds.size.y);int n=v.Count;
            // TMP's default front faces -Z; these columns are read from +Z.
            foreach(var p in mesh.vertices){var offset=(p-bounds.center)*factor;v.Add(new Vector3(spots[i].x-offset.x/scale.x,spots[i].y+offset.y/scale.y,.52f));seeds.Add(new Vector2(.2f+i*.27f,0));colors.Add(new Color(1,1,1,.26f));}
            uv.AddRange(mesh.uv);triangles.AddRange(mesh.triangles.Select(k=>k+n));
        }
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetUVs(1,seeds);m.SetColors(colors);m.SetNormals(Enumerable.Repeat(Vector3.forward,v.Count).ToList());m.SetTriangles(triangles,0);m.RecalculateBounds();var b=m.bounds;b.Expand(.03f);m.bounds=b;return m;
    }
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
}
#endif
