using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    [InitializeOnLoad]
    public static class ArchiveMusicLabelChecks
    {
        const string Key="LogicLegends.AdaptiveMusicAndLabels.Checks",Output="output/ArchiveAdaptiveMusic/";
        [Serializable]class Report{public bool passed;public string error,scope="Native isolated Play Mode, streaming audio and world-label rendering; no room or account writes.";public List<string> checks=new List<string>();public int maximumMusicVoices;}
        static Report report;static ArchiveAudioLandscape audio;static ArchiveWorldLabelLayout layout;static TextMeshPro near,far,late;static TextMeshProUGUI hud;
        static MethodInfo tickMusic;static int stage;static double began,next;static int phase;static bool hunted,focus,recovery;
        static float oldTimeScale;static bool finishing;
        static ArchiveMusicLabelChecks(){EditorApplication.playModeStateChanged+=State;}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use saved Edit Mode.");
            var scene=SceneManager.GetActiveScene();if(scene.isDirty)throw new InvalidOperationException("Preserve unsaved scene changes.");
            SessionState.SetString(Key+".restore",scene.path);
            var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.SaveScene(test,"Assets/Cinematics/OpenArchiveTeaser/AdaptiveAudioLabelChecks.unity");
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                report=new Report();finishing=false;oldTimeScale=Time.timeScale;Time.timeScale=1;stage=0;phase=0;hunted=focus=recovery=false;began=EditorApplication.timeSinceStartup;next=began+1.2;
                var camera=new GameObject("Label Test Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.1f;camera.farClipPlane=200;camera.gameObject.AddComponent<AudioListener>();
                var sun=new GameObject("Test Light").AddComponent<Light>();sun.type=LightType.Directional;
                RenderSettings.fog=false;
                layout=new GameObject("Existing Label Presenter").AddComponent<ArchiveWorldLabelLayout>();
                near=Text("Near World Caption",20);far=Text("Far World Caption",100);
                var canvas=new GameObject("Screen HUD",typeof(RectTransform),typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                hud=new GameObject("HUD remains",typeof(RectTransform)).AddComponent<TextMeshProUGUI>();hud.transform.SetParent(canvas.transform,false);hud.text="HUD";
                var go=new GameObject("Existing Audio Presenter");go.SetActive(false);audio=go.AddComponent<ArchiveAudioLandscape>();
                ArchiveAdaptiveMusicSetup.AssignIfAvailable(audio);audio.musicCrossfadeSeconds=.35f;audio.huntCrossfadeSeconds=.25f;
                go.SetActive(true);tickMusic=typeof(ArchiveAudioLandscape).GetMethod("UpdateBackgroundMusic",BindingFlags.Instance|BindingFlags.NonPublic);
                EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
            }
            else if(state==PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update-=Tick;Application.logMessageReceived-=Log;SessionState.SetBool(Key,false);
                var restore=SessionState.GetString(Key+".restore","");if(!string.IsNullOrEmpty(restore))EditorSceneManager.OpenScene(restore,OpenSceneMode.Single);
            }
        }
        static TextMeshPro Text(string name,float z)
        {var text=new GameObject(name).AddComponent<TextMeshPro>();text.transform.position=new Vector3(0,0,z);text.rectTransform.sizeDelta=new Vector2(6,2);text.fontSize=2;text.alignment=TextAlignmentOptions.Center;text.text=name;return text;}
        static void Check(bool pass,string name){report.checks.Add((pass?"PASS ":"FAIL ")+name);if(!pass)throw new Exception(name);}
        static void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)report.error=message+"\n"+stack;}
        static void Tick()
        {
            if(finishing)return;
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if(EditorApplication.timeSinceStartup-began>45)throw new Exception("Audio/label checks timed out.");
                tickMusic.Invoke(audio,new object[]{phase,hunted,focus,recovery,Time.unscaledDeltaTime});
                report.maximumMusicVoices=Mathf.Max(report.maximumMusicVoices,audio.PlayingMusicStems);
                if(audio.PlayingMusicStems>2)throw new Exception("More than two background streams.");
                if(EditorApplication.timeSinceStartup<next)return;
                next=EditorApplication.timeSinceStartup+.8;
                switch(stage++)
                {
                    case 0:
                        Check(audio.PlayingMusicStems==1&&audio.CurrentMusicMood==ArchiveMusicMood.Daylight,"Daylight automatically fades into the original exploration score");
                        Check(near.GetComponent<ArchiveWorldLabel>()!=null&&far.GetComponent<ArchiveWorldLabel>()!=null,"Fixed world text is discovered by the existing label presenter");
                        Check(near.alpha>.99f&&far.alpha<.01f&&far.GetComponent<Renderer>().forceRenderingOff,"Near captions are readable; distant captions stop rendering");
                        Check(hud.alpha==1&&hud.GetComponent<ArchiveWorldLabel>()==null,"Screen HUD is excluded from distance fading");phase=1;break;
                    case 1:Check(audio.CurrentMusicMood==ArchiveMusicMood.Afternoon&&!audio.MusicTransitioning,"Afternoon switches to the warmer score");phase=2;break;
                    case 2:Check(audio.CurrentMusicMood==ArchiveMusicMood.Evening&&!audio.MusicTransitioning,"Evening switches to the uneasy score");phase=3;break;
                    case 3:Check(audio.CurrentMusicMood==ArchiveMusicMood.Night&&!audio.MusicTransitioning,"Night switches to the suspense score");hunted=true;break;
                    case 4:Check(audio.CurrentMusicMood==ArchiveMusicMood.Hunt&&!audio.MusicTransitioning,"Local pursuit switches to the urgent hunt score");hunted=false;break;
                    case 5:
                        Check(audio.CurrentMusicMood==ArchiveMusicMood.Night&&audio.PlayingMusicStems==1,"Ending a pursuit returns to the current world mood without stacking tracks");
                        Check(audio.GetComponentsInChildren<AudioSource>().Where(s=>s.loop).All(s=>s.outputAudioMixerGroup!=null&&s.outputAudioMixerGroup.name=="Music"),"Both score voices use the existing Music volume mixer");focus=true;break;
                    case 6:Check(Mathf.Abs(audio.MusicDuck-audio.encounterDuck)<.01f,"Capture/cinematic focus ducks the score for SFX and instructions");focus=false;recovery=true;break;
                    case 7:Check(audio.CurrentMusicLevel<.001f,"Connection recovery fades the score to silence");recovery=false;near.transform.position=new Vector3(0,0,70);break;
                    case 8:Check(Mathf.Abs(near.alpha-.5f)<.03f&&near.enabled&&near.gameObject.activeInHierarchy,"Captions fade smoothly between 55 and 85 units while owners stay active");near.transform.position=new Vector3(0,0,50);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.04f;break;
                    case 9:
                        Check(near.alpha>.99f,"Text remains readable at 50 units even in dense night fog");
                        RenderSettings.fog=false;far.text="Updated while hidden";far.transform.position=new Vector3(0,0,20);break;
                    case 10:Check(far.alpha>.99f&&far.text=="Updated while hidden"&&!far.GetComponent<Renderer>().forceRenderingOff&&far.transform.position.z==20,"Approaching restores current text and preserves the owner's moving transform");late=Text("Late Spawned Caption",100);next+=.8;break;
                    case 11:Check(late.GetComponent<ArchiveWorldLabel>()!=null&&late.alpha<.01f,"Labels created later by challenges are discovered and distance culled");layout.enabled=false;break;
                    case 12:
                        Check(late.alpha>.99f&&!late.GetComponent<Renderer>().forceRenderingOff,"Disabling the presenter restores owned visual properties");
                        Check(Enumerable.Range(0,11).Select(ArchiveTopicSession.WorldPhaseForTopics).SequenceEqual(new[]{0,0,0,1,1,2,2,3,3,3,3}),"Music follows the unchanged 3/5/7 completed-topic world phases");
                        Check(audio.MusicDuck>.99f&&audio.PlayingMusicStems==1,"Score recovers to a single background voice");Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            finishing=true;EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
            if(!string.IsNullOrEmpty(error))report.error=error;report.passed=string.IsNullOrEmpty(report.error);Time.timeScale=oldTimeScale;
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"playmode-checks.json",JsonUtility.ToJson(report,true));EditorApplication.ExitPlaymode();
        }
    }
}
