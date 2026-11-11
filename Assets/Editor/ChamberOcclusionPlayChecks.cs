#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChamberOcclusionPlayChecks
{
    const string Flag="ChamberOcclusion.Active", Report="output/mobile-performance/OcclusionPlayResults.txt";
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        SessionState.SetString(Flag+".Start",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Flag+".Background",Application.runInBackground);
        SessionState.SetInt(Flag+".Size",BookMapMobileChecks.GetSize());
        SessionState.SetInt(Flag+".Safe",(int)Crystal.SafeArea.Sim);
        SessionState.SetBool(Flag,true);
        File.WriteAllText(Report,"EDITOR ONLY. Retained real chamber, baked occlusion data, 800x480, fixed poses. Frame CPU includes Editor/wait; not Android performance. GPU-named counter is raw/unvalidated, not an accepted GPU duration.\n");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Tests/AndroidOptimization/ChamberExperiment.unity");
        EditorApplication.isPlaying=true;
    }
    [InitializeOnLoadMethod] static void Hook(){EditorApplication.playModeStateChanged-=Restore;EditorApplication.playModeStateChanged+=Restore;}
    static void Restore(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Flag,false))return;
        SessionState.SetBool(Flag,false);Application.runInBackground=SessionState.GetBool(Flag+".Background",false);
        BookMapMobileChecks.SetSize(SessionState.GetInt(Flag+".Size",2));
        Crystal.SafeArea.Sim=(Crystal.SafeArea.SimDevice)SessionState.GetInt(Flag+".Safe",0);
        string path=SessionState.GetString(Flag+".Start","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot()
    {
        if(!SessionState.GetBool(Flag,false))return;
        var go=new GameObject("Occlusion Play checks");var runner=go.AddComponent<ChamberOcclusionQaRunner>();runner.StartCoroutine(Run());
    }
    static IEnumerator Run()
    {
        Application.runInBackground=true;BookMapMobileChecks.SetSize(2);
        File.AppendAllText(Report,"Hardware="+SystemInfo.processorType+" / "+SystemInfo.graphicsDeviceName+" / "+SystemInfo.graphicsDeviceType+"\n");
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.enabled=false;
        foreach(var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))animator.enabled=false;
        foreach(var particles in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))particles.Pause(true);
        var cameraGo=new GameObject("Measured camera");var view=cameraGo.AddComponent<Camera>();cameraGo.tag="MainCamera";
        view.nearClipPlane=.1f;view.farClipPlane=150;
        var asset=AssetDatabase.LoadMainAssetAtPath("Assets/Tests/AndroidOptimization/ChamberExperiment/OcclusionCullingData.asset");
        File.AppendAllText(Report,"Occlusion serialized bytes="+new FileInfo(AssetDatabase.GetAssetPath(asset)).Length+" native object bytes="+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(asset)+"\n");
        var poses=new[]{new Vector3(0,4,-17),new Vector3(-14,4,-5),new Vector3(0,4,12)};
        foreach(var pose in poses)
        {
            view.transform.position=pose;view.transform.LookAt(new Vector3(0,4,0));
            foreach(bool enabled in new[]{false,true})
            {
                view.useOcclusionCulling=enabled;
                for(int i=0;i<60;i++)yield return null;
                using(var draws=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
                using(var triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1))
                using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
                using(var cull=ProfilerRecorder.StartNew(ProfilerCategory.Render,"CullScriptable",1))
                using(var gpu=ProfilerRecorder.StartNew(ProfilerCategory.Render,"GPU Frame Time",1))
                {
                    double d=0,t=0,c=0,u=0,g=0;
                    for(int i=0;i<60;i++){yield return null;d+=draws.LastValue;t+=triangles.LastValue;c+=cpu.LastValue/1000000.0;u+=cull.LastValue/1000000.0;g+=gpu.LastValue/1000000.0;}
                    string gpuText=gpu.Valid&&g>0?(g/60).ToString("F2"):"unavailable";
                    File.AppendAllText(Report,"pose="+pose+" occlusion="+enabled+" draw="+(d/60).ToString("F1")+" triangles="+(t/60).ToString("F0")+" main-thread-ms="+(c/60).ToString("F2")+" scriptable-cull-ms="+(u/60).ToString("F3")+" gpu-named-counter-raw-ms="+gpuText+" Unity-total-allocated-MiB="+(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()/1048576.0).ToString("F1")+"\n");
                }
                if(pose==poses[0]) { ScreenCapture.CaptureScreenshot("Library/ChamberOcclusion_"+enabled+".png"); yield return null; yield return null; }
            }
        }
        File.AppendAllText(Report,"COMPLETE. Check images for visibility. Device CPU/GPU/culling-memory costs and moving-door correctness remain unverified.\n");
        EditorApplication.isPlaying=false;
    }
}
public sealed class ChamberOcclusionQaRunner:MonoBehaviour{}
#endif
