using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Asset-only setup for the two board props; never saves or edits a gameplay scene.
public static class InductionBoardsSetup
{
    const string Root="Assets/Art/InductionBoards";
    const string Source="SourceAssets/InductionBoards";
    static readonly string[] Surfaces={"Stone","Parchment","Metal","Crystal"};
    static readonly string[] ModelNames={"InformationBoard","FormulaScrollsStand"};
    static readonly string[] ScrollLabels={"Algebra Rules","Summation Formulas","Induction Templates","Useful Identities"};
    static List<string> report;

    [MenuItem("Tools/Logic Legends/Build Induction Information and Formula Boards")]
    public static void Build()
    {
        if(Shader.Find("Universal Render Pipeline/Lit")==null)throw new Exception("This pack requires the project's URP Lit shader.");
        Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Source);
        AssetDatabase.Refresh();report=new List<string>();
        var mats=new Dictionary<string,Material>();
        foreach(string surface in Surfaces)
        {
            ConfigureTexture(surface+"_BaseColor",true,false);
            ConfigureTexture(surface+"_Roughness",false,false);
            ConfigureTexture(surface+"_MetallicSmoothness",false,false);
            ConfigureTexture(surface+"_Normal",false,true);
            var path=Root+"/Materials/Chamber_"+surface+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.name="Chamber_"+surface;mat.shader=Shader.Find("Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor",Color.white);
            mat.SetTexture("_BaseMap",Texture(surface+"_BaseColor"));
            mat.SetTexture("_BumpMap",Texture(surface+"_Normal"));mat.SetFloat("_BumpScale",.35f);
            mat.SetTexture("_MetallicGlossMap",Texture(surface+"_MetallicSmoothness"));
            mat.SetFloat("_Smoothness",1);mat.SetFloat("_SmoothnessTextureChannel",0);
            mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            bool glow=surface=="Crystal";
            mat.SetTexture("_EmissionMap",glow?Texture(surface+"_BaseColor"):null);
            mat.SetColor("_EmissionColor",glow?Color.white*.8f:Color.black);
            mat.globalIlluminationFlags=glow?MaterialGlobalIlluminationFlags.RealtimeEmissive:MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if(glow)mat.EnableKeyword("_EMISSION");else mat.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);mats.Add(mat.name,mat);
        }
        AssetDatabase.SaveAssets();
        foreach(string name in ModelNames)
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/"+name+".fbx");
            importer.globalScale=1;importer.useFileScale=true;importer.importAnimation=false;
            importer.importCameras=false;importer.importLights=false;importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var pair in mats)importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);
            importer.SaveAndReimport();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+name+".fbx");
            var wrapper=new GameObject(name+"_Editable");
            try
            {
                var geometry=Object.Instantiate(model);geometry.transform.SetParent(wrapper.transform,false);
                if(name=="InformationBoard")
                {
                    AddText(geometry,"ChallengeTitleAnchor","CHALLENGE 1",new Vector2(1.43f,.27f),2.5f,TextAlignmentOptions.Center,true);
                    AddText(geometry,"ChallengeFormulaAnchor","1 + 2 + ... + n\n= n(n + 1) / 2",new Vector2(1.43f,.48f),1.8f,TextAlignmentOptions.Center,false);
                    AddText(geometry,"ChallengeInstructionsAnchor","Use mathematical induction\nto prove the statement.\n\nBase Case: prove P(1).\nAssume P(k), then prove P(k + 1).",new Vector2(1.43f,.79f),1.6f,TextAlignmentOptions.TopLeft,false);
                }
                else
                {
                    var nodes=geometry.GetComponentsInChildren<Transform>();
                    for(int i=0;i<4;i++)
                    {
                        string number=(i+1).ToString("00");var slot=nodes.Single(t=>t.name=="ScrollSlot_"+number);
                        foreach(string child in new[]{"Scroll_"+number+"_ReusableParchment","ScrollLabelAnchor_"+number,"ScrollLabelAnchor_"+number+"_Facing"})
                            nodes.Single(t=>t.name==child).SetParent(slot,true);
                        AddText(geometry,"ScrollLabelAnchor_"+number,ScrollLabels[i],new Vector2(.98f,.23f),1.7f,TextAlignmentOptions.Center,false);
                    }
                }
                Validate(wrapper,name,mats);
                PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"/Prefabs/"+name+".prefab");
                if(name=="FormulaScrollsStand")
                {
                    var first=geometry.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Scroll_01_ReusableParchment");
                    var scrolls=geometry.GetComponentsInChildren<MeshFilter>().Where(f=>f.name.Contains("ReusableParchment")).ToArray();
                    if(scrolls.Length!=4||scrolls.Any(f=>f.sharedMesh!=first.sharedMesh))throw new Exception("Scroll mesh instances must be shared.");
                    // Additional convenience prefab references the same mesh; no extra FBX export.
                    var reusable=new GameObject("ReusableFormulaScroll");
                    try
                    {
                        var piece=Object.Instantiate(first.gameObject,reusable.transform);piece.transform.localPosition=Vector3.zero;
                        var label=Object.Instantiate(geometry.GetComponentsInChildren<TMP_Text>().Single(t=>t.text==ScrollLabels[0]).gameObject,reusable.transform);
                        var scrollAnchor=geometry.GetComponentsInChildren<Transform>().Single(t=>t.name=="ScrollLabelAnchor_01");
                        label.transform.localPosition=scrollAnchor.localPosition;
                        label.transform.rotation=Quaternion.LookRotation(-new Vector3(0,Mathf.Sin(14*Mathf.Deg2Rad),Mathf.Cos(14*Mathf.Deg2Rad)),Vector3.up);
                        label.GetComponent<TMP_Text>().text="Editable Formula Label";
                        PrefabUtility.SaveAsPrefabAsset(reusable,Root+"/Prefabs/ReusableFormulaScroll.prefab");
                    }
                    finally{Object.DestroyImmediate(reusable);}
                    report.Add("PASS: four separate scroll objects use one shared imported mesh.");
                }
            }
            finally{Object.DestroyImmediate(wrapper);}
        }
        AssetDatabase.SaveAssets();File.WriteAllLines(Source+"/unity-validation.txt",report);
        Debug.Log("INDUCTION_BOARDS_UNITY_VALIDATION_PASS");
    }
    static Texture2D Texture(string name)
    {
        var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+".png");
        if(tex==null)throw new Exception("Missing baked texture: "+name);return tex;
    }
    static void ConfigureTexture(string name,bool color,bool normal)
    {
        var importer=AssetImporter.GetAtPath(Root+"/Textures/"+name+".png") as TextureImporter;
        if(importer==null)throw new Exception("Texture not imported: "+name);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        importer.sRGBTexture=color;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;
        importer.maxTextureSize=1024;importer.mipmapEnabled=true;importer.wrapMode=TextureWrapMode.Repeat;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Trilinear;importer.SaveAndReimport();
        report.Add("PASS: texture "+name+", "+(normal?"tangent normal":color?"sRGB color":"linear data"));
    }
    static TMP_FontAsset Font(bool title)
    {
        var font=title?TMP_Settings.defaultFontAsset:AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if(font==null)font=TMP_Settings.defaultFontAsset;
        if(font==null)throw new Exception("Import TextMeshPro Essential Resources.");return font;
    }
    static void AddText(GameObject geometry,string anchorName,string text,Vector2 size,float fontSize,TextAlignmentOptions align,bool title)
    {
        var transforms=geometry.GetComponentsInChildren<Transform>();
        var anchor=transforms.Single(t=>t.name==anchorName);var facing=transforms.Single(t=>t.name==anchorName+"_Facing");
        string labelName=anchorName.StartsWith("ScrollLabelAnchor_")?anchorName.Replace("ScrollLabelAnchor_","ScrollLabel_"):anchorName.Replace("Anchor","Label");
        var label=new GameObject(labelName,typeof(RectTransform));label.transform.SetParent(anchor,false);
        label.transform.rotation=Quaternion.LookRotation(-(facing.position-anchor.position).normalized,Vector3.up);
        var tmp=label.AddComponent<TextMeshPro>();tmp.font=Font(title);tmp.text=text;tmp.color=new Color(.13f,.08f,.035f);
        tmp.fontStyle=title?FontStyles.Bold:FontStyles.Normal;tmp.alignment=align;tmp.enableAutoSizing=true;
        tmp.fontSize=fontSize;tmp.fontSizeMax=fontSize;tmp.fontSizeMin=.65f;tmp.rectTransform.sizeDelta=size;
        tmp.textWrappingMode=TextWrappingModes.Normal;tmp.overflowMode=TextOverflowModes.Overflow;tmp.raycastTarget=false;tmp.ForceMeshUpdate();
        if(tmp.isTextOverflowing||tmp.textInfo.characterCount==0)throw new Exception("Text does not fit: "+text);
        report.Add("PASS: editable TMP "+anchorName+", "+text.Replace("\n"," / "));
    }
    static void Validate(GameObject asset,string name,Dictionary<string,Material> mats)
    {
        var renderers=asset.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        if(Mathf.Abs(bounds.min.y)>.005||Mathf.Abs(bounds.center.x)>.005)throw new Exception("Invalid bottom center pivot: "+name+" "+bounds);
        var bottom=asset.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<TMP_Text>()==null)
            .SelectMany(f=>f.sharedMesh.vertices.Select(p=>f.transform.TransformPoint(p)))
            .Where(p=>Mathf.Abs(p.y)<.001f).ToArray();
        if(bottom.Length==0||Mathf.Abs((bottom.Min(p=>p.z)+bottom.Max(p=>p.z))*.5f)>.001f)
            throw new Exception("Pivot must be at the center of the bottom footprint: "+name);
        foreach(var r in renderers)
        {
            if(r.GetComponent<MeshFilter>().sharedMesh.uv.Length==0)throw new Exception("Missing UVs: "+r.name);
            if(r.transform.lossyScale!=Vector3.one)throw new Exception("Unapplied scale: "+r.name);
            foreach(var mat in r.sharedMaterials)
            {
                if(mat==null||!mats.Values.Contains(mat))throw new Exception("Unmapped Blender material: "+r.name);
                if(mat.shader==null||mat.shader.name!="Universal Render Pipeline/Lit"||!mat.shader.isSupported)throw new Exception("Unsupported/pink shader: "+r.name);
                foreach(string property in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"})
                    if(mat.GetTexture(property)==null)throw new Exception("Missing "+property+" in "+mat.name);
                report.Add("PASS: "+name+" / "+r.name+" / "+mat.name+" -> color, normal, metallic-smoothness textures");
            }
        }
        report.Add("PASS: "+name+" bottom center pivot (including depth); bounds "+bounds);
    }
    public static void BuildAndPackage()
    {
        Build();Directory.CreateDirectory("output/InductionBoards");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var info=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/InformationBoard.prefab"));info.transform.position=new Vector3(1.03f,0,0);
        var rack=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/FormulaScrollsStand.prefab"));rack.transform.position=new Vector3(-1.15f,0,0);
        foreach(var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))text.ForceMeshUpdate();
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.4f,.44f,.52f);
        var light=new GameObject("StudioKey").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,145,0);
        var camera=new GameObject("ValidationCamera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.95f;camera.aspect=1.6f;
        camera.transform.position=new Vector3(0,3.4f,10);camera.transform.LookAt(new Vector3(0,1.64f,0));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.046f,.075f);camera.nearClipPlane=.1f;camera.farClipPlane=30;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);rt.Create();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        RenderTexture.active=rt;var png=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        png.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);png.Apply();File.WriteAllBytes(Source+"/Unity_Assets_Preview.png",png.EncodeToPNG());
        var pixels=png.GetPixels32();int pink=pixels.Count(p=>p.r>220&&p.b>220&&p.g<40);
        if(pink>pixels.Length*.005f)throw new Exception("Error-shader magenta detected in Unity render.");
        report.Add("PASS: Unity URP render; magenta error pixels "+pink+" / "+pixels.Length);
        File.WriteAllLines(Source+"/unity-validation.txt",report);
        RenderTexture.active=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(png);
        var assets=AssetDatabase.FindAssets("",new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)&&!p.EndsWith(".cs")).ToArray();
        var package=AssetDatabase.GetDependencies(assets,true).Where(p=>p.StartsWith("Assets/")&&!p.EndsWith(".cs")&&!AssetDatabase.IsValidFolder(p)).Concat(new[]{Root+"/Editor/InductionBoardsSetup.cs"}).Distinct().ToArray();
        AssetDatabase.ExportPackage(package,"output/InductionBoards/InductionBoards.unitypackage",ExportPackageOptions.Default);
        Debug.Log("INDUCTION_BOARDS_PACKAGE_AND_RENDER_PASS");
    }
}
