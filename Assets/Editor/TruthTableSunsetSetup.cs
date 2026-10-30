#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class TruthTableSunsetSetup
{
    const string Folder="Assets/Art/TruthTableSunset";
    public static object Apply()
    {
        var scene=SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.name!="TruthTable")throw new InvalidOperationException("Open TruthTable in Edit Mode.");
        if(!System.IO.File.Exists("Library/TruthSunsetBefore.unity"))EditorSceneManager.SaveScene(scene,"Library/TruthSunsetBefore.unity",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art","TruthTableSunset");
        var root=GameObject.Find("Truth_Table");
        foreach(var old in root.GetComponentsInChildren<TruthTableNightVisibility>(true))UnityEngine.Object.DestroyImmediate(old);
        var lighting=root.GetComponent<TruthTableDuskLighting>();var so=new SerializedObject(lighting);
        Set(so,"sunlightIntensity",.85f);Set(so,"skyExposure",1.05f);Set(so,"lampIntensity",22);Set(so,"lampRange",19);Set(so,"fixtureGlow",1.2f);
        Color sun=new Color(1,.74f,.5f),sky=new Color(.36f,.29f,.36f),equator=new Color(.3f,.24f,.22f),ground=new Color(.15f,.125f,.15f),tint=new Color(.6f,.52f,.48f,1);
        so.FindProperty("sunlightColor").colorValue=sun;so.FindProperty("ambientSky").colorValue=sky;so.FindProperty("ambientEquator").colorValue=equator;so.FindProperty("ambientGround").colorValue=ground;so.FindProperty("skyTint").colorValue=tint;so.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
        if(RenderSettings.sun!=null){RenderSettings.sun.intensity=.85f;RenderSettings.sun.color=sun;RenderSettings.sun.transform.rotation=Quaternion.Euler(14,330,0);}
        var skybox=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/TruthTableSunsetSky.mat");
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/SkySeries Freebie/MegaSun.mat");
        if(skybox==null){skybox=new Material(source.shader){name="TruthTableSunsetSky"};AssetDatabase.CreateAsset(skybox,Folder+"/TruthTableSunsetSky.mat");}
        skybox.shader=source.shader;skybox.shaderKeywords=new string[0];skybox.SetTexture("_Tex",source.GetTexture("_Tex"));skybox.SetFloat("_Rotation",source.GetFloat("_Rotation"));skybox.SetFloat("_Exposure",1.05f);skybox.SetColor("_Tint",tint);RenderSettings.skybox=skybox;EditorUtility.SetDirty(skybox);
        int lamps=0;
        foreach(var light in root.GetComponentsInChildren<Light>(true).Where(l=>l.name=="TruthTableFixtureLight")){light.intensity=22;light.range=19;lamps++;}
        var glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/TruthTableNight/Celestial Gold - TruthTable Lantern.mat");
        if(glow!=null){glow.SetColor("_EmissionColor",new Color(1,.71f,.4f)*1.2f);EditorUtility.SetDirty(glow);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return new {lamps,skybox=skybox.name,sunIntensity=RenderSettings.sun.intensity,nightEffects=root.GetComponentsInChildren<TruthTableNightVisibility>(true).Length};
    }
    static void Set(SerializedObject so,string name,float value){so.FindProperty(name).floatValue=value;}
}
#endif
