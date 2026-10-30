using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    public static class ArchiveNightAtmosphereChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;public float fogPixelDifference;
            public string scope="Native saved MultiplayerGameplay: existing night-state fog/audio presentation, original map rendered with fog enabled/disabled at identical night lighting, warning duck and exact daylight/disable restoration. No Android performance or subjective sound perception verified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static double next;static ArchiveTopicSession s;static ArchiveWorldProgression w;
        static bool oldThreats,dayFog;static FogMode dayMode;static Color dayColor;static float dayDensity,dayStart,dayEnd;static GameObject probe;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new Exception("Use offline saved Play Mode.");
            s=ArchiveTopicSession.Current;w=s.worldProgression;oldThreats=w.suppressThreatsForVerification;w.suppressThreatsForVerification=true;
            dayFog=RenderSettings.fog;dayMode=RenderSettings.fogMode;dayColor=RenderSettings.fogColor;dayDensity=RenderSettings.fogDensity;dayStart=RenderSettings.fogStartDistance;dayEnd=RenderSettings.fogEndDistance;
            report=new Report();Directory.CreateDirectory("Temp/ArchiveNightAtmosphere");flow=Flow();next=0;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Log(string t,string stack,LogType kind){if(report.errors.Count<8&&(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert))report.errors.Add(t+"\n"+stack);}
        static void Save()=>File.WriteAllText("Temp/ArchiveNightAtmosphere/Play.json",JsonUtility.ToJson(report,true));
        static void Check(bool ok,string label){report.checks.Add((ok?"PASS ":"FAIL ")+label);Save();if(!ok)throw new Exception(label);}
        static void Tick(){if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();try{if(EditorApplication.timeSinceStartup<next)return;if(!Application.isPlaying)throw new Exception("Play stopped");if(!flow.MoveNext()){Finish(true);return;}next=EditorApplication.timeSinceStartup+(flow.Current is float delay?delay:0);}catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}}
        static void Finish(bool ok)
        {
            EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;
            if(probe!=null)UnityEngine.Object.Destroy(probe);
            if(Application.isPlaying&&s!=null){w.enabled=true;s.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);w.suppressThreatsForVerification=oldThreats;}
            report.passed=ok&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();
        }
        static void SetPhase(int phase,bool warning=false)
        {var state=s.State.worldTime;state.adminPhaseOverride=state.phase=state.previousPhase=phase;state.changedAt=ArchiveTopicSession.Clock-100;state.arrivalStartedAt=warning?ArchiveTopicSession.Clock:-1;state.secondaryReleased=false;}
        static bool OriginalFog()=>RenderSettings.fog==dayFog&&RenderSettings.fogMode==dayMode&&RenderSettings.fogColor==dayColor&&Mathf.Abs(RenderSettings.fogDensity-dayDensity)<.000001f&&Mathf.Abs(RenderSettings.fogStartDistance-dayStart)<.001f&&Mathf.Abs(RenderSettings.fogEndDistance-dayEnd)<.001f;
        static Texture2D Render(Camera camera,string name)
        {
            var rt=RenderTexture.GetTemporary(640,360,24);var previous=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(640,360,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,640,360),0,0);image.Apply();File.WriteAllBytes("Temp/ArchiveNightAtmosphere/"+name+".png",ImageConversion.EncodeToPNG(image));
            camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);return image;
        }
        static IEnumerator Flow()
        {
            Status="Night fog and bounded ambience";SetPhase(3);yield return .8f;
            Check(w.NightFogActive&&w.NightFogBlend>.99f&&RenderSettings.fogMode==FogMode.ExponentialSquared&&Mathf.Abs(RenderSettings.fogDensity-w.nightFogDensity)<.0001f,
                "Night enables the real URP exponential-squared distance fog at its Inspector density");
            Check(w.nightWind!=null&&w.nightCracks!=null&&w.nightWind.name=="Archive_Night_Ominous_Wind_Loop"&&w.nightCracks.name=="Archive_Night_Distant_Creaks_Loop"&&w.NightAmbiencePlaying&&w.NightAmbienceVoiceCount==2,
                "Night plays the new ominous wind and sparse distant-detail loops using only the existing two voices");
            probe=new GameObject("Night_Fog_Pixel_QA");var camera=probe.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.transform.SetPositionAndRotation(Camera.main.transform.position,Camera.main.transform.rotation);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var fogged=Render(camera,"Original_Map_Night_Fog");RenderSettings.fog=false;var clear=Render(camera,"Original_Map_Night_NoFog");RenderSettings.fog=true;
            var a=fogged.GetPixels32();var b=clear.GetPixels32();double difference=0;
            for(int i=0;i<a.Length/2;i++)difference+=Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
            report.fogPixelDifference=(float)(difference/(a.Length/2*3*255));UnityEngine.Object.Destroy(fogged);UnityEngine.Object.Destroy(clear);
            Check(report.fogPixelDifference>.004f,"The original map's rendered pixels visibly change with fog at identical night lighting");
            Check(Mathf.Exp(-Mathf.Pow(w.nightFogDensity*15,2))>.85f&&Mathf.Exp(-Mathf.Pow(w.nightFogDensity*75,2))<.4f,
                "Night haze obscures distant scenery while keeping nearby teammates and the flashlight range readable");
            s.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1);s.RequestEquipItem(0);yield return .3f;s.RequestUseEquippedItem();yield return .7f;
            ScreenCapture.CaptureScreenshot("Temp/ArchiveNightAtmosphere/Night_Fog_Flashlight.png");yield return .2f;
            Status="Warnings retain audible priority";SetPhase(3,true);yield return .7f;
            Check(w.NightAmbienceDuck<=w.warningAmbienceMultiplier+.08f,"Real night warnings duck the ambience rather than competing with it");
            Status="Original daytime restoration";SetPhase(0);yield return .5f;
            Check(OriginalFog()&&!w.NightAmbiencePlaying&&w.NightAmbienceVoiceCount==0,"Daylight restores every saved fog setting and stops the night loops");
            SetPhase(3);yield return .4f;w.enabled=false;yield return .1f;
            Check(OriginalFog()&&w.NightAmbienceVoiceCount==0,"Disabling the existing world controller restores original fog and stops its night ambience");
        }
    }
}
