#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class TruthTableSunsetChecks
{
    const string Report="Library/TruthTableSunsetChecks.txt";
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS "+message+"\n");}
    public static IEnumerator Run()
    {
        File.WriteAllText(Report,"Offline TruthTable sunset checks. No player account/reward writes.\n");
        var root=GameObject.Find("Truth_Table");var lighting=root.GetComponent<TruthTableDuskLighting>();var camera=Camera.main;
        Check(root.GetComponentsInChildren<TruthTableNightVisibility>(true).Length==0,"night cone, peripheral blur and darkness component removed");
        Check(camera.targetTexture==null&&!UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Any(c=>c.name=="TruthTable UI Presentation Camera"),"normal direct camera rendering with no night compositor");
        Check(!root.GetComponentsInChildren<Light>(true).Any(l=>l.name=="TruthTable Facing Visibility Light"),"player-facing night light removed");
        Check(RenderSettings.sun.intensity>.7f&&RenderSettings.sun.color.r>RenderSettings.sun.color.b&&RenderSettings.sun.transform.eulerAngles.x<20,"warm low-angle sunset sun");
        Check(RenderSettings.ambientSkyColor.maxColorComponent>=.3f&&RenderSettings.ambientGroundColor.maxColorComponent>=.12f,"readable ambient illumination");
        Check(RenderSettings.skybox.name.Contains("TruthTableSunsetSky")&&!ShaderUtil.ShaderHasError(RenderSettings.skybox.shader),"dedicated compatible sunset sky");
        Check(lighting.FixtureCount==25&&lighting.FlickeringCount==6,"all 25 lamp lights and six original flickering fixtures preserved");
        var initial=lighting.LampLights.Select(l=>l.intensity).ToArray();var changed=new HashSet<int>();float until=Time.realtimeSinceStartup+13;
        while(Time.realtimeSinceStartup<until){yield return null;for(int i=0;i<initial.Length;i++)if(Mathf.Abs(initial[i]-lighting.LampLights[i].intensity)>.1f)changed.Add(i);}
        Check(changed.Count==6&&lighting.LampLights.All(l=>l.intensity>=19.3f&&l.intensity<=22.01f),"gentle flicker retained at sunset brightness; other lamps remain steady");
        var brain=camera.GetComponent<Unity.Cinemachine.CinemachineBrain>();bool enabled=brain!=null&&brain.enabled;
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        if(brain!=null)brain.enabled=false;
        camera.transform.SetPositionAndRotation(new Vector3(642,153,801),Quaternion.LookRotation(new Vector3(632,149,776)-new Vector3(642,153,801)));
        foreach(int size in new[]{6,12,3})
        {
            BookMapMobileChecks.SetSize(size);yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot("Library/TruthSunset_"+Screen.width+"x"+Screen.height+".png");
            Check(camera.targetTexture==null,"full clear camera view at "+Screen.width+"x"+Screen.height);
        }
        var player=Player.LocalInstance;var facing=player.transform.rotation;
        foreach(float yaw in new[]{0f,90f,180f,270f}){player.transform.rotation=Quaternion.Euler(0,yaw,0);yield return null;Check(camera.targetTexture==null,"turning does not enable a visibility mask: "+yaw);}
        player.transform.rotation=facing;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);if(brain!=null)brain.enabled=enabled;
        BookMapMobileChecks.SetSize(6);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/TruthSunset_Gameplay.png");
        File.AppendAllText(Report,"ALL SUNSET CHECKS PASSED\n");
    }
}
#endif
