using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Exercises the saved scenery with real book/answer commands, not invented progress.</summary>
    public static class ArchiveEnvironmentChecks
    {
        [Serializable] sealed class Report
        {
            public bool passed;public int topicsObserved,animatedCanopies,visibleMotes,ambientVoices;
            public string scope="Native Editor: saved decorations, leaf/bark separation and mesh cleanup, existing 59-round command journey and portal, restoration/reset/late-join presentation. Android performance and physical touch are not verified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;static IEnumerator flow;static double next,deadline;
        static ArchiveTopicSession session;static ArchiveTopicLandmark[] landmarks;static ArchiveLivingEnvironment living;
        static readonly bool[] completed=new bool[10],restored=new bool[10],partial=new bool[10],captured=new bool[10];
        static bool observing,toggledCompleted;static int toggledTopic=-1,toggledCount;
        public static string Status{get;private set;}="Idle";
        public static void Begin()
        {
            if(!Application.isPlaying||Photon.Pun.PhotonNetwork.InRoom||flow!=null)throw new InvalidOperationException("Use fresh offline MultiplayerGameplay Play.");
            session=ArchiveTopicSession.Current;report=new Report();
            landmarks=UnityEngine.Object.FindObjectsByType<ArchiveTopicLandmark>(FindObjectsSortMode.None).OrderBy(v=>v.station.topic).ToArray();
            living=UnityEngine.Object.FindFirstObjectByType<ArchiveLivingEnvironment>();
            Array.Clear(completed,0,10);Array.Clear(restored,0,10);Array.Clear(partial,0,10);
            Array.Clear(captured,0,10);
            observing=toggledCompleted=false;toggledTopic=-1;toggledCount=0;
            flow=Flow();next=0;deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
        }
        static void Save(){Directory.CreateDirectory("Temp/ArchiveEnvironment");File.WriteAllText("Temp/ArchiveEnvironment/Play.json",JsonUtility.ToJson(report,true));}
        static void Check(bool value,string text){report.checks.Add((value?"PASS ":"FAIL ")+text);Save();if(!value)throw new InvalidOperationException(text);}
        static void Log(string text,string stack,LogType type){if(report.errors.Count<12&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(text+"\n"+stack);}
        static void Tick()
        {
            if(flow==null)return;EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if(!Application.isPlaying||EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Play stopped or environment verification timed out.");
                if(observing&&session.Ready)
                {
                    foreach(var landmark in landmarks)
                    {
                        int topic=landmark.station.topic;float body=ArchiveCrystalProgress.Body(session.State,topic);
                        partial[topic]|=body>0&&body<1&&landmark.TargetProgress>0&&landmark.TargetProgress<1;
                        completed[topic]|=session.State.stations[topic].phase==2&&body==1&&landmark.TargetProgress==1;
                        restored[topic]|=completed[topic]&&landmark.CurrentProgress>.99f;
                        if((topic==6||topic==9)&&restored[topic]&&!captured[topic])
                        {captured[topic]=true;CaptureLandmark(topic,"T"+(topic+1)+"_Restored.png");}
                        if(!toggledCompleted&&completed[topic]&&landmark.CompletionBurstCount>0)
                        {
                            // Re-enable an already completed display as a late join would.
                            // This touches presentation only and never the running topic.
                            toggledCompleted=true;toggledTopic=topic;toggledCount=landmark.CompletionBurstCount;
                            landmark.enabled=false;landmark.enabled=true;
                        }
                    }
                }
                if(EditorApplication.timeSinceStartup<next)return;
                if(!flow.MoveNext()){Finish(true);return;}
                next=EditorApplication.timeSinceStartup+(flow.Current is float seconds?seconds:0);
            }
            catch(Exception e){report.errors.Add(Status+": "+e);Finish(false);}
        }
        static void Finish(bool success)
        {EditorApplication.update-=Tick;Application.logMessageReceived-=Log;flow=null;observing=false;if(living!=null)living.enabled=true;report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";Save();}
        public static void CaptureLandmark(int topic,string file)
        {
            var owner=ArchiveTopicSession.Current;
            var station=owner.stations[topic];var ornament=station.GetComponentInChildren<ArchiveTopicLandmark>();
            var renderers=ornament.GetComponentsInChildren<Renderer>(true);var enabledBefore=renderers.Select(r=>r.enabled).ToArray();
            bool textEnabled=ornament.identity!=null&&ornament.identity.enabled;
            var cameraObject=new GameObject("Environment_Review_Camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=48;camera.nearClipPlane=.1f;camera.farClipPlane=700;
            camera.transform.position=station.transform.TransformPoint(new Vector3(17,6.8f,7));
            camera.transform.LookAt(station.transform.TransformPoint(new Vector3(10,2.4f,-5)));
            var destination=RenderTexture.GetTemporary(1280,720,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                foreach(var renderer in renderers)renderer.enabled=true;
                if(ornament.identity!=null){ornament.identity.enabled=true;ornament.identity.ForceMeshUpdate();}
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=destination});
                RenderTexture.active=destination;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
                Directory.CreateDirectory("Temp/ArchiveEnvironment");File.WriteAllBytes("Temp/ArchiveEnvironment/"+file,texture.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<renderers.Length;i++)renderers[i].enabled=enabledBefore[i];
                if(ornament.identity!=null)ornament.identity.enabled=textEnabled;
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(destination);UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(cameraObject);
            }
        }
        static IEnumerator Flow()
        {
            Status="Saved assets and leaf-only animation";
            Check(landmarks.Length==10&&landmarks.Select(v=>v.station.topic).SequenceEqual(Enumerable.Range(0,10)),"All ten existing stations have their own mathematical landmark");
            var roots=ArchiveEnvironmentPolishSetup.DecorationRoots(session);
            Check(roots.Length==11&&roots.All(r=>r.GetComponentsInChildren<Collider>(true).Length==0&&r.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true).Length==0),"Decoration cannot create new collision fences or monster navigation blockers");
            Check(roots.All(r=>r.GetComponentsInChildren<Canvas>(true).Length==0&&r.GetComponentsInChildren<Light>(true).Length==0),"Environment adds no screen UI or realtime light sources");
            Check(roots.SelectMany(r=>r.GetComponentsInChildren<Renderer>(true)).Where(r=>r.GetComponent<TMPro.TMP_Text>()==null).SelectMany(r=>r.sharedMaterials).All(m=>m!=null&&m.shader.isSupported&&m.renderQueue<=2500),"All new scenery meshes use supported opaque URP materials; text retains its existing font shader");
            Check(living!=null&&living.foliageMeshes.Length>0&&living.groveCenters.Length>0&&living.motes.Length==24,"Existing textured foliage and a bounded reusable mote pool are wired in the saved scene");
            living.enabled=false;yield return .1f;
            var originals=living.foliageMeshes.Where(f=>f!=null).ToDictionary(f=>f,f=>f.sharedMesh);
            living.enabled=true;yield return 1.25f;
            report.animatedCanopies=living.AnimatedCanopyCount;report.visibleMotes=living.VisibleMoteCount;report.ambientVoices=living.ActiveAmbientVoiceCount;
            Check(living.AnimatedCanopyCount>0&&living.AnimatedCanopyCount<=8&&living.VisibleMoteCount<=24&&living.ActiveAmbientVoiceCount<=2,"Live foliage, motes and localized voices remain within their Inspector budgets");
            int changedLeaves=0,barkChanges=0,animated=0;
            foreach(var pair in originals)
            {
                if(pair.Key.sharedMesh==pair.Value)continue;animated++;
                int sourceIndex=Array.IndexOf(living.originalFoliageSources,pair.Value);
                if(sourceIndex<0)throw new InvalidOperationException("Animated canopy has no saved readable wind source.");
                var source=living.readableFoliageSources[sourceIndex];
                var tags=new List<Vector4>();source.GetUVs(2,tags);
                var before=source.vertices;var after=pair.Key.sharedMesh.vertices;
                for(int i=0;i<before.Length;i++)if((before[i]-after[i]).sqrMagnitude>.000000001f){if(tags[i].w>.5f)changedLeaves++;else barkChanges++;}
            }
            Check(animated>0&&changedLeaves>0&&barkChanges==0,"Actual canopy vertices move while every bark vertex remains unchanged");
            living.enabled=false;yield return .1f;
            Check(originals.All(p=>p.Key.sharedMesh==p.Value),"Disabling ambience restores every original foliage mesh reference");
            living.enabled=true;yield return .85f;
            Check(living.AnimatedCanopyCount<=8,"Re-enabling ambience respects the same bounded mesh budget");
            Status="Real topic journey with environment observer";
            observing=true;ArchiveTopicPlayChecks.Begin();
            while(ArchiveTopicPlayChecks.Status!="Passed"&&ArchiveTopicPlayChecks.Status!="Failed")yield return .2f;
            Check(ArchiveTopicPlayChecks.Status=="Passed","All existing 59 challenge rounds, interaction validation and portal journey still pass");
            observing=false;yield return .5f;
            report.topicsObserved=completed.Count(v=>v);
            Check(completed.All(v=>v)&&restored.All(v=>v),"Every topic's accepted answers restore its own environment through the existing shared state");
            Check(partial.All(v=>v),"Each topic shows incremental restoration before its final answer");
            Check(toggledCompleted&&landmarks[toggledTopic].CompletionBurstCount==toggledCount,"Re-enabling a completed landmark never replays its completion burst");
            Check(landmarks.All(v=>v.TargetProgress==0&&v.CurrentProgress<.01f),"The existing new-match reset restores broken scenery without retaining the previous match's progress");
            var nest=UnityEngine.Object.FindFirstObjectByType<ArchiveNestAtmosphere>();
            Check(nest!=null&&nest.waymarkRunes.Length==10&&nest.warningRunes.Length+nest.emberRenderers.Length+nest.chainRenderers.Length<=12,"All ten topic waymarks are saved and nest ornaments stay within twelve renderers");
            Check(ArchiveNestAtmosphere.WarningFor(Hunting.KhezuHuntState.NestSleeping,4,false)==0&&ArchiveNestAtmosphere.WarningFor(Hunting.KhezuHuntState.Hunting,4,true)==0,"Nest warning honors earned sleep and tranquilizer suppression");
            session.hunting.Request(Hunting.KhezuCommand.Sleep);yield return .35f;
            Check(nest.WarningAmount==0,"Resetting the existing monster to sleep also clears its environmental danger warning");
            Save();
        }
    }
}
