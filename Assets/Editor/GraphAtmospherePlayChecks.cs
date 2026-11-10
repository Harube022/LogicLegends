#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using LogicLegends.Graph;
using UnityEditor;
using UnityEngine;

/// <summary>Opt-in visual checks inside the existing isolated Graph Help test session.</summary>
public static class GraphAtmospherePlayChecks
{
    const string Folder="Library/GraphAtmosphereQA/";
    static void Check(bool pass,string label)
    {File.AppendAllText(Folder+"PlayChecks.txt",(pass?"PASS ":"FAIL ")+label+"\n");if(!pass)throw new InvalidOperationException(label);}
    static IEnumerator Capture(string name)
    {yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Folder+name+".png");yield return null;yield return null;}
    public static IEnumerator Run()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"PlayChecks.txt","Graph atmosphere — isolated Editor Play Mode. No live wallet transport.\n");
        var stage=UnityEngine.Object.FindFirstObjectByType<GraphStageManager>();
        var effect=UnityEngine.Object.FindFirstObjectByType<GraphChamberAtmosphere>();
        Check(effect.torchLights.Length==8&&effect.crystalLights.Length==9,"all eight torches and nine blue clusters have pool lights");
        Check(effect.torchLights.All(l=>l.enabled&&l.shadows==LightShadows.None)&&effect.crystalLights.All(l=>l.enabled&&l.shadows==LightShadows.None),"local lights enabled without mobile shadow-map cost");
        Check(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.name=="Torch warm light").All(l=>!l.enabled),"legacy duplicate warm pools remain disabled");
        Check(effect.crystalSurfaces.All(s=>s.renderer.sharedMaterials[s.materialIndex].IsKeywordEnabled("_EMISSION")&&!s.renderer.sharedMaterials[s.materialIndex].shader.name.Contains("Error")),"blue crystal submesh uses a valid emissive material");
        float torchMin=float.MaxValue,torchMax=0,blueMin=float.MaxValue,blueMax=0;
        var fixtureMaterials=effect.crystalSurfaces.Select(s=>s.renderer.sharedMaterials[s.materialIndex].GetInstanceID()).ToArray();
        float until=Time.realtimeSinceStartup+6;
        while(Time.realtimeSinceStartup<until)
        {
            torchMin=Mathf.Min(torchMin,effect.torchLights[0].intensity);torchMax=Mathf.Max(torchMax,effect.torchLights[0].intensity);
            blueMin=Mathf.Min(blueMin,effect.crystalLights[0].intensity);blueMax=Mathf.Max(blueMax,effect.crystalLights[0].intensity);yield return null;
        }
        Check(torchMax-torchMin>.05f&&torchMin>effect.torchIntensity*.8f,"gentle torch variation without dark outages");
        Check(blueMax-blueMin>.4f&&blueMin>effect.crystalIntensity*.8f,"slow blue pulse remains above a readable baseline");
        Check(fixtureMaterials.SequenceEqual(effect.crystalSurfaces.Select(s=>s.renderer.sharedMaterials[s.materialIndex].GetInstanceID())),"fixture animation retains its shared material instances");
        int question=stage.QuestionIndex,challenge=stage.ChallengeIndex,correct=stage.Correct;
        float previousTime=Time.timeScale;int previousLighting=SoloGameSettings.Current.lightingQuality;
        var settings=UnityEngine.Object.FindFirstObjectByType<SoloSettingsRuntime>();
        var apply=typeof(SoloSettingsRuntime).GetMethod("Apply",BindingFlags.Instance|BindingFlags.NonPublic);
        Time.timeScale=0;
        foreach(int size in new[]{2,6,12})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("Gameplay_"+Screen.width+"x"+Screen.height);
        }
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.3f);
        var roof=GraphOverheadVisibility.HideCeiling();
        var rig=PrelimTutorialCameraRig.Create("Graph atmosphere isolated preview");
        until=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<until)
        {GraphQuestionView.Frame(rig,stage.Challenge,stage.arena,.5f,.58f,.73f,.65f);yield return null;}
        yield return Capture("BirdsEye_1920x1080");
        GraphOverheadVisibility.Restore(roof);
        SoloGameSettings.Current.lightingQuality=1;apply.Invoke(settings,null);
        var target=effect.crystalLights[4].transform.position+Vector3.up*2;
        until=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<until)
        {rig.MoveTowards(target+new Vector3(15,8,20),Quaternion.LookRotation(new Vector3(-15,-8,-20)),55);yield return null;}
        Check(effect.bloomVolume.enabled,"restrained bloom enabled at medium lighting");
        yield return Capture("CrystalPool_Medium");
        target=effect.torchLights[0].transform.position-Vector3.up*2;
        until=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<until)
        {rig.MoveTowards(target+new Vector3(16,3,20),Quaternion.LookRotation(new Vector3(-16,-3,-20)),55);yield return null;}
        yield return Capture("TorchPool_Medium");
        SoloGameSettings.Current.lightingQuality=0;apply.Invoke(settings,null);yield return null;
        Check(!effect.bloomVolume.enabled,"lowest lighting skips bloom while lights and emission remain active");
        SoloGameSettings.Current.lightingQuality=previousLighting;apply.Invoke(settings,null);
        rig.ReturnToGameplay(null);yield return new WaitForSecondsRealtime(1.5f);Time.timeScale=previousTime;
        Check(stage.QuestionIndex==question&&stage.ChallengeIndex==challenge&&stage.Correct==correct,"lighting and preview cameras do not change question or progression");
        Check(Player.LocalInstance.enabled&&!GameInput.Instance.GameplayInputBlocked,"normal controls remain available after views");
        File.AppendAllText(Folder+"PlayChecks.txt","PASS Graph visual checks complete. Physical Android performance requires device profiling.\n");
    }
}
#endif
