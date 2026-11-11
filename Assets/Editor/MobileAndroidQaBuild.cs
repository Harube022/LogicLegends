#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Separate, debug-signed package. Does not overwrite the production application.
public static class MobileAndroidQaBuild
{
    const string Folder="output/mobile-performance/AndroidInputQA";
    public static void Prepare()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Finish Play Mode first.");
        Directory.CreateDirectory("Assets/Tests/AndroidOptimization");
        File.Copy("output/mobile-performance/qa/MobileAndroidSmoke.cs","Assets/Tests/AndroidOptimization/MobileAndroidSmoke.cs",true);
        AssetDatabase.Refresh();
    }
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Finish current operation first.");
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Select Android explicitly before the QA build.");
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/Build.txt","QUEUED. Separate Android development QA package; not a production release.\n");
        EditorApplication.update-=BuildOnce;EditorApplication.update+=BuildOnce;
    }
    static void BuildOnce(){EditorApplication.update-=BuildOnce;Build();}
    static void Build()
    {
        string id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        bool customKey=PlayerSettings.Android.useCustomKeystore;
        var architecture=PlayerSettings.Android.targetArchitectures;
        try
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.logiclegends.mobileqa");
            // Unity 6000.3 rejects the deprecated Magic Leap x86-64 target.
            // Retain the supported phone architecture for any future explicit QA build.
            PlayerSettings.Android.useCustomKeystore=false;PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            File.AppendAllText(Folder+"/Build.txt","BUILDING ARM64 package; extra LL_MOBILE_QA define only for this Development build.\n");
            var scenes=new[]{"TruthTable","RulesOfInference","Setssss","Formal vs. Informal Proofs","Direct&Indirect","Trees"};
            for(int i=0;i<scenes.Length;i++)scenes[i]="Assets/Scenes/"+scenes[i]+".unity";
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=Folder+"/LogicLegends_InputQA.apk",target=BuildTarget.Android,options=BuildOptions.Development,extraScriptingDefines=new[]{"LL_MOBILE_QA"}});
            File.AppendAllText(Folder+"/Build.txt",report.summary.result+" | "+report.summary.totalSize+" bytes | "+report.summary.totalTime+"\n");
        }
        catch(Exception e){File.AppendAllText(Folder+"/Build.txt","FAIL "+e+"\n");Debug.LogException(e);}
        finally
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);PlayerSettings.Android.useCustomKeystore=customKey;PlayerSettings.Android.targetArchitectures=architecture;
            File.AppendAllText(Folder+"/Build.txt","Restored original application ID, architecture and signing selection.\n");
        }
    }
}
#endif
