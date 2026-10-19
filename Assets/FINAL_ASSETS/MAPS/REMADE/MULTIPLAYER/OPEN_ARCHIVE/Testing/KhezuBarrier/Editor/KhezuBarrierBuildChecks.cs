using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LogicLegends.Tests.Khezu.Editor
{
    public static class KhezuBarrierBuildChecks
    {
        const string Folder="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuBarrier/Reports";
        const string Scene="Assets/FINAL_ASSETS/MAPS/REMADE/MULTIPLAYER/OPEN_ARCHIVE/Testing/KhezuCinematic/OpenArchive_KhezuCinematic_Test.unity";
        [Serializable] public class BuildResult {
            public string checkedUtc,result,output,exception,limits="Build/scene-strip checks only. No physical Android touchscreen, performance or live multiplayer run is implied.";
            public bool development,androidSupported,developmentUiRetained,releaseUiStripped,debugKeystoreRestored;
            public double seconds;public string[] errors=new string[0],warnings=new string[0];
        }
        public static string VerifySceneStripping(){
            var scene=EditorSceneManager.NewPreviewScene();bool kept=false,stripped=false;
            try{
                var go=new GameObject("Barrier_Development_Panel_Strip_Fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<KhezuBarrierDevelopmentUI>();
                KhezuBarrierReleaseUiStripper.Strip(scene,true);kept=go!=null;
                KhezuBarrierReleaseUiStripper.Strip(scene,false);stripped=go==null;
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
            if(!kept || !stripped)throw new InvalidOperationException("Development panel strip policy failed.");return "Development scene retains its panel; release scene removes it.";
        }
        public static void QueueAndroidDevelopmentBuild(){
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Editor must be idle.");
            VerifySceneStripping();EditorApplication.update-=RunQueuedBuild;EditorApplication.update+=RunQueuedBuild;
        }
        static void RunQueuedBuild(){EditorApplication.update-=RunQueuedBuild;BuildAndroidDevelopment();}
        static void BuildAndroidDevelopment(){
            var result=new BuildResult{checkedUtc=DateTime.UtcNow.ToString("o"),result="Building",development=true,developmentUiRetained=true,releaseUiStripped=true,androidSupported=BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android),output=Path.GetFullPath("Builds/KhezuPhase3B/LogicLegends-Khezu-Development.apk")};
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Path.GetDirectoryName(result.output));Save(result);
            bool customKey=PlayerSettings.Android.useCustomKeystore;
            try{
                // A development APK uses the debug key; preserve the project's release signing configuration.
                PlayerSettings.Android.useCustomKeystore=false;
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=BuildTarget.Android,locationPathName=result.output,options=BuildOptions.Development|BuildOptions.AllowDebugging});
                result.result=build.summary.result.ToString();result.seconds=build.summary.totalTime.TotalSeconds;
                result.errors=build.steps.SelectMany(step=>step.messages).Where(m=>m.type==LogType.Error || m.type==LogType.Exception).Select(m=>m.content.Split('\n')[0]).Distinct().ToArray();
                result.warnings=build.steps.SelectMany(step=>step.messages).Where(m=>m.type==LogType.Warning).Select(m=>m.content.Split('\n')[0]).Distinct().ToArray();
            }catch(Exception ex){result.result="Exception";result.exception=ex.Message.Split('\n')[0];}
            finally{PlayerSettings.Android.useCustomKeystore=customKey;AssetDatabase.SaveAssets();result.debugKeystoreRestored=PlayerSettings.Android.useCustomKeystore==customKey;Save(result);}
            Debug.Log("Khezu Phase 3B Android development build: "+result.result+"; "+result.output);
        }
        static void Save(BuildResult result){File.WriteAllText(Folder+"/AndroidDevelopmentBuild.json",JsonUtility.ToJson(result,true));}
    }
}
