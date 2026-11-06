using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Opt-in native reading checks against genuine graded work and explicit, excluded terminal fixtures.</summary>
    public static class ArchiveLearningRecapPlayChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Saved MultiplayerGameplay offline Editor Play: real incorrect/correct T2 submissions, excluded victory/defeat fixtures, native pointer controls, 1920x1080 and 2340x1080 reading, and detached presentation across a temporary additive Main Menu scene. Full retained-party scene return, remote rendered UI and physical Android are separate checks.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        const string Folder="Temp/ArchiveLearningRecap",SizeKey="ArchiveLearningRecap.QA.GameView";
        const BindingFlags Hidden=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static readonly Dictionary<string,KeyValuePair<bool,int>> preferences=new Dictionary<string,KeyValuePair<bool,int>>();
        static readonly Dictionary<Toggle,bool> toggles=new Dictionary<Toggle,bool>();
        static ArchiveTopicSession session;
        static ArchiveLearningRecapUI view;
        static Player player;
        static Report report;
        static Vector3 position;
        static Quaternion rotation;
        static HealthManager.ActorHealth[] health;
        static bool active,oldThreats,oldDefeat,oldInput,ownerActive;
        static float scale;
        static double next;
        static object previousReview,previousAccount;
        static Scene originalScene,temporaryScene;
        static bool madeTemporaryScene,ownerDisabled;
        static ArchiveMistake detachedSource;
        static string detachedFeedback,attemptPrompt;
        public static string Status{get;private set;}="Idle";

        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Post-match Learning Review (Offline Play)")]
        public static void Begin()
        {
            session=ArchiveTopicSession.Current;player=Player.LocalInstance;
            if(active||!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||player==null||
                SceneManager.GetActiveScene().path!="Assets/Scenes/MultiplayerGameplay.unity")
                throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play.");
            position=player.transform.position;rotation=player.transform.rotation;health=session.hunting.health.ExportActorHealth();
            oldThreats=session.worldProgression.suppressThreatsForVerification;oldDefeat=session.suppressDefeatForVerification;
            oldInput=GameInput.Instance.GameplayInputBlocked;scale=Time.timeScale;originalScene=SceneManager.GetActiveScene();
            previousReview=typeof(ArchiveLearningRecapUI).GetField("snapshot",Hidden).GetValue(null);
            previousAccount=typeof(ArchiveLearningRecapUI).GetField("account",Hidden).GetValue(null);
            preferences.Clear();foreach(string key in new[]{ArchiveLearningUI.OnboardingPreferenceKey,"OpenArchive.Learning.v1.AutoTips"}
                .Concat(Enumerable.Range(0,10).Select(i=>"OpenArchive.Learning.v1.Seen."+i)))
                preferences[key]=new KeyValuePair<bool,int>(PlayerPrefs.HasKey(key),PlayerPrefs.GetInt(key,0));
            toggles.Clear();foreach(var toggle in UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(toggle.name=="Show_Hide_Test_Tools"){toggles[toggle]=toggle.isOn;toggle.isOn=false;}
            SessionState.SetInt(SizeKey,SelectedSize());
            Directory.CreateDirectory(Folder);report=new Report();active=true;next=0;stack.Clear();stack.Push(Flow());
            Status="Preparing genuine academic receipts";EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Application.logMessageReceived+=Log;
        }
        static void Tick()
        {
            if(!active)return;EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if(EditorApplication.timeSinceStartup<next)return;
                if(!Application.isPlaying)throw new InvalidOperationException("Play Mode stopped.");
                if(stack.Count==0){Finish(true);return;}
                var current=stack.Peek();if(!current.MoveNext()){stack.Pop();return;}
                if(current.Current is IEnumerator nested)stack.Push(nested);
                else next=EditorApplication.timeSinceStartup+(current.Current is float delay?delay:0);
            }
            catch(Exception exception){report.errors.Add(Status+": "+exception);Finish(false);}
        }
        static void PlayState(PlayModeStateChange state){if(active&&state==PlayModeStateChange.ExitingPlayMode){report.errors.Add("Play Mode interrupted.");Finish(false);}}
        static void Log(string message,string trace,LogType type)
        {if(active&&report.errors.Count<8&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))report.errors.Add(message+"\n"+trace);}
        static void Check(bool pass,string message){report.checks.Add((pass?"PASS ":"FAIL ")+message);if(!pass)throw new InvalidOperationException(message);}
        static IEnumerator Wait(Func<bool> ready,string message,float seconds=10)
        {double end=EditorApplication.timeSinceStartup+seconds;while(!ready()){if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(message);yield return .025f;}}
        static void Commit()=>typeof(ArchiveTopicSession).GetMethod("Commit",Hidden).Invoke(session,null);

        static void NativeTarget(Button button,bool click)
        {
            Check(button!=null&&button.isActiveAndEnabled&&button.interactable,"Existing action is visible and enabled: "+(button==null?"missing":button.name));
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),
                button.name+" receives the topmost native raycast (top="+(hits.Count==0?"none":hits[0].gameObject.name)+")");
            if(click)ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        }
        static void SafeReading()
        {
            Canvas.ForceUpdateCanvases();var corners=new Vector3[4];view.ReviewRoot.GetWorldCorners(corners);
            Check(corners.All(p=>p.x>=Screen.safeArea.xMin-1&&p.x<=Screen.safeArea.xMax+1&&p.y>=Screen.safeArea.yMin-1&&p.y<=Screen.safeArea.yMax+1),
                "The reading card stays in the "+Screen.width+"x"+Screen.height+" safe area");
            Check(view.ReadingScroll.vertical&&!view.ReadingScroll.horizontal&&view.ReadingScroll.verticalScrollbar!=null&&
                view.ReadingScroll.verticalScrollbarVisibility==ScrollRect.ScrollbarVisibility.Permanent,"Reading uses a permanent visible native vertical scrollbar");
            foreach(var label in view.ReviewRoot.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();Check(label.font!=null,"Visible reading font exists for "+label.name);
                if(label.name=="Learning_Review_Text")Check("∴⋯≥∈∅≤".All(c=>label.font.HasCharacter(c,true,false)),"The actual recap reading font supports DM1 notation through its existing fallbacks");
                if(label.name!="Learning_Review_Text")Check(!label.isTextOverflowing,"Fixed reading label fits: "+label.name);
            }
        }
        static int SelectedSize()
        {
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            return (int)type.GetProperty("selectedSizeIndex",Hidden).GetValue(EditorWindow.GetWindow(type));
        }
        static void SelectSize(int index)
        {
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex",Hidden).SetValue(window,index);window.Repaint();
        }
        static void Resolution(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;var type=assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=type.BaseType.GetProperty("instance",Hidden).GetValue(null);var group=type.GetProperty("currentGroup",Hidden).GetValue(sizes);
            int count=(int)group.GetType().GetMethod("GetTotalCount",Hidden).Invoke(group,null);var get=group.GetType().GetMethod("GetGameViewSize",Hidden);
            for(int i=0;i<count;i++)
            {
                var value=get.Invoke(group,new object[]{i});var sizeType=value.GetType();
                if((int)sizeType.GetProperty("width",Hidden).GetValue(value)==width&&(int)sizeType.GetProperty("height",Hidden).GetValue(value)==height)
                {SelectSize(i);return;}
            }
            var ctor=assembly.GetType("UnityEditor.GameViewSize").GetConstructors().First(c=>c.GetParameters().Length==4);
            var size=ctor.Invoke(new object[]{Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1),width,height,"Learning review QA "+width+"x"+height});
            group.GetType().GetMethod("AddCustomSize",Hidden).Invoke(group,new[]{size});SelectSize(count);
        }
        static void Retention()
        {
            var originals=session.State.mistakes;string actors=JsonUtility.ToJson(new ActorCopy{values=session.State.actors.ToArray()});
            int mistakes=originals.Count,hearts=session.hunting.health.ActorHearts(-1),revision=session.State.revision;
            var deadlines=session.State.stations.Select(s=>s.timerDeadline).ToArray();double matchDeadline=session.State.matchTime.deadline;
            try
            {
                session.State.mistakes=new List<ArchiveMistake>();
                foreach(int actor in new[]{-1,-2})for(int topic=0;topic<5;topic++)
                    session.State.mistakes.Add(new ArchiveMistake{actor=actor,topic=topic,round=0,subphase=0,resetSerial=0,reason="answer",reviewVersion=1,reviewFeedback=new string('R',900)});
                session.State.mistakes.Add(new ArchiveMistake{actor=-1,topic=4,round=0,subphase=0,resetSerial=0,reason="answer",reviewVersion=1,reviewFeedback="Latest same-problem attempt"});
                session.State.mistakes.Add(new ArchiveMistake{actor=-1,topic=99,reason="answer",reviewVersion=1,reviewFeedback="Invalid context"});
                var metadata=session.State.mistakes.Select(m=>m.actor+":"+m.topic+":"+m.round+":"+m.subphase+":"+m.resetSerial+":"+m.reason).ToArray();
                typeof(ArchiveTopicSession).GetMethod("NormalizeLearningReviews",Hidden).Invoke(session,null);
                var retained=session.State.mistakes.Where(m=>!string.IsNullOrEmpty(m.reviewFeedback)).ToArray();
                Check(retained.Length<=ArchiveTopicSession.LearningReviewLimitPerMatch&&retained.GroupBy(m=>m.actor).All(g=>g.Count()<=3),"The real receipt normalizer bounds personal reading context without another gameplay history");
                Check(retained.All(m=>m.reviewFeedback.Length<=700)&&retained.All(m=>m.topic>=0&&m.topic<10),"Retained feedback is bounded to 700 characters and valid topic identities");
                Check(retained.GroupBy(m=>m.actor+":"+m.topic+":"+m.round+":"+m.subphase+":"+m.resetSerial).All(g=>g.Count()==1),"A repeated problem retains only one recent context");
                Check(metadata.SequenceEqual(session.State.mistakes.Select(m=>m.actor+":"+m.topic+":"+m.round+":"+m.subphase+":"+m.resetSerial+":"+m.reason))&&
                    actors==JsonUtility.ToJson(new ActorCopy{values=session.State.actors.ToArray()})&&session.State.revision==revision&&
                    session.hunting.health.ActorHearts(-1)==hearts&&deadlines.SequenceEqual(session.State.stations.Select(s=>s.timerDeadline))&&session.State.matchTime.deadline==matchDeadline,
                    "Trimming presentation context preserves mistake metadata, actor counters, hearts and deadlines");
            }
            finally{session.State.mistakes=originals;}
            Check(session.State.mistakes.Count==mistakes,"The isolated retention fixture restores the original mistake list immediately");
        }
        [Serializable] sealed class ActorCopy{public ArchiveActor[] values;}
        static IEnumerator GradedAttempt(bool correctAfterward)
        {
            var station=session.stations[1];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            session.Request(1,ArchiveAction.Start);yield return Wait(()=>session.State.stations[1].phase==1,"Original T2 Book command activates its clock");
            var state=session.State.stations[1];var question=session.Question(1);attemptPrompt=question.prompt;
            session.RequestLearningHint(1);int count=session.State.mistakes.Count;
            state.placements=ArchiveTopicChecks.Solution(question).placements.ToArray();state.placements[0]=1-state.placements[0];
            session.Request(1,ArchiveAction.Submit);yield return Wait(()=>session.State.mistakes.Count==count+1,"The real T2 validator records an incorrect submission");
            Check(state.lastAnswerResult==-1&&session.State.mistakes.Last().actor==session.LocalActor,"The genuine mistake is attributed to the submitting local player");
            session.hunting.Request(Hunting.KhezuCommand.Sleep);
            if(!correctAfterward)yield break;
            yield return Wait(()=>ArchiveTopicSession.Clock-state.lastAnswerAt>=1.6,"The original submission cooldown expires");
            state.placements=ArchiveTopicChecks.Solution(question).placements.ToArray();session.Request(1,ArchiveAction.Submit);
            yield return Wait(()=>state.round==1,"The same real validator accepts the corrected table");
            var actor=session.State.actors.First(a=>a.actor==session.LocalActor);
            Check(actor.answers==2&&actor.correctAnswers==1&&state.lastAnswerResult==1,"Existing answer receipts distinguish one wrong and one correct submission");
        }
        static IEnumerator Flow()
        {
            session.suppressDefeatForVerification=session.worldProgression.suppressThreatsForVerification=true;
            PlayerPrefs.SetInt("OpenArchive.Learning.v1.AutoTips",0);
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
            session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);ArchiveLearningRecapUI.ClearLastReview();
            Retention();yield return GradedAttempt(true);
            Check(!ArchiveLearningRecapUI.HasReview,"An unfinished topic cannot publish a terminal learning recap");
            session.RequestAdminTest(ArchiveAdminTestAction.CompleteAllTopics);
            session.State.exit.opened=session.State.exit.won=true;session.State.exit.openedAt=session.State.exit.wonAt=ArchiveTopicSession.Clock;
            session.State.exit.escaped.Add(new ArchiveEscape{actor=-1,anchor=player.transform.position,time=ArchiveTopicSession.Clock});Commit();
            yield return Wait(()=>ArchiveExitUI.Current!=null&&ArchiveExitUI.Current.ResultVisible&&ArchiveLearningRecapUI.HasReview,"The real victory result offers the captured reading model");
            yield return .25f;view=ArchiveLearningRecapUI.Current;
            Check(ArchiveLearningRecapUI.Snapshot.actor==-1&&ArchiveLearningRecapUI.Snapshot.reviews.Length==1&&ArchiveLearningRecapUI.Snapshot.reviews[0].prompt.Contains(attemptPrompt),"The terminal snapshot retains the actual earlier problem and only personal mistake context");
            Check(ArchiveLearningRecapUI.Snapshot.practice,"The explicit Admin fixture is clearly marked practice");
            string checkpoint=JsonUtility.ToJson(session.State),snapshot=JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot);
            bool blocked=GameInput.Instance.GameplayInputBlocked,locked=player.IsMovementLocked;
            foreach(int width in new[]{1920,2340})
            {
                Status="Native learning review "+width+"x1080";Resolution(width,1080);yield return Wait(()=>Screen.width==width&&Screen.height==1080,"Native Game View resolution settles");yield return .15f;
                NativeTarget(ArchiveExitUI.Current.ReviewButton,true);yield return Wait(()=>ArchiveLearningRecapUI.IsVisible,"Review opens through the existing result button");
                SafeReading();NativeTarget(view.ReturnButton,false);
                Check(view.VisibleText.Contains("TOPICS PRACTICED")&&view.VisibleText.Contains("TEAMWORK"),"Summary contains topic and teamwork records");
                var scrollEvent=new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-6)};
                ExecuteEvents.Execute(view.ReadingScroll.gameObject,scrollEvent,ExecuteEvents.scrollHandler);yield return .1f;
                Check(view.ReadingScroll.verticalNormalizedPosition<.99f,"The native reading pane responds to a scroll event");
                NativeTarget(view.MistakesButton,true);yield return .1f;
                Check(view.VisibleText.Contains(attemptPrompt)&&view.VisibleText.Contains("REASONING TO PRACTICE"),"Mistake tab shows the submitted problem and reasoning guidance");
                ScreenCapture.CaptureScreenshot(Folder+"/Mistakes_"+width+".png");yield return .1f;
                NativeTarget(view.StudyButton,true);yield return .1f;
                Check(view.VisibleText.Contains("LOGIC GARDEN")&&view.VisibleText.Contains("MODULE"),"Study Next provides module-based review suggestions");
                NativeTarget(view.SummaryButton,true);yield return .1f;
                ScreenCapture.CaptureScreenshot(Folder+"/Summary_"+width+".png");yield return .1f;
                NativeTarget(view.CloseButton,true);yield return .1f;
                Check(!ArchiveLearningRecapUI.IsVisible&&ArchiveExitUI.Current.ResultVisible,"Closing the optional review returns immediately to results");NativeTarget(ArchiveExitUI.Current.ReturnButton,false);
                Check(JsonUtility.ToJson(session.State)==checkpoint&&JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot)==snapshot&&Time.timeScale==scale&&
                    GameInput.Instance.GameplayInputBlocked==blocked&&player.IsMovementLocked==locked,"Reading, tabs, scrolling and closing cannot mutate gameplay, rewards, input ownership or clocks");
            }
            Status="Defeat and detached scene presentation";ArchiveLearningRecapUI.ClearLastReview();
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);yield return GradedAttempt(false);
            session.State.survival.defeated=true;session.State.survival.defeatedAt=ArchiveTopicSession.Clock;session.State.survival.defeatReason=ArchiveTopicSession.MatchTimeoutReason;Commit();
            Check(ArchiveLearningRecapUI.HasReview&&ArchiveLearningRecapUI.Snapshot.outcome=="defeat","The terminal owner event captures the recap before any deferred scene-return frame");
            double defeatedAt=session.State.survival.defeatedAt;
            Check(Mathf.Abs(session.DefeatRemaining-6)<.2f,"Defeat retains the original six-second return countdown");
            yield return Wait(()=>ArchiveExitUI.Current.ResultVisible&&ArchiveLearningRecapUI.Snapshot?.outcome=="defeat","The defeat result offers a separate terminal review");
            NativeTarget(ArchiveExitUI.Current.ReviewButton,true);yield return .1f;NativeTarget(view.MistakesButton,true);yield return .1f;
            string defeatSnapshot=JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot),reading=view.VisibleText;
            Check(session.State.survival.defeatedAt==defeatedAt&&Math.Abs(session.DefeatRemaining-Math.Max(0,6-(ArchiveTopicSession.Clock-defeatedAt)))<.05,
                "Opening defeat review never extends, restarts or pauses the original return timer");
            detachedSource=session.State.mistakes.Last(m=>m.actor==-1&&m.reason=="answer");detachedFeedback=detachedSource.reviewFeedback;
            detachedSource.reviewFeedback="EDITED AFTER SNAPSHOT";
            ownerActive=session.gameObject.activeSelf;session.gameObject.SetActive(false);ownerDisabled=true;
            temporaryScene=SceneManager.GetSceneByName("Main Menu");madeTemporaryScene=!temporaryScene.IsValid()||!temporaryScene.isLoaded;
            if(madeTemporaryScene)temporaryScene=SceneManager.CreateScene("Main Menu");SceneManager.SetActiveScene(temporaryScene);yield return .2f;
            Check(ArchiveLearningRecapUI.IsVisible&&view.gameObject.scene.name=="DontDestroyOnLoad"&&view.VisibleText==reading&&
                JsonUtility.ToJson(ArchiveLearningRecapUI.Snapshot)==defeatSnapshot,"Detached reading survives an inactive source owner and a temporary Main Menu scene without following source edits");
            Check(!view.ReturnButton.gameObject.activeSelf&&view.CloseButton.GetComponentInChildren<TMP_Text>().text.Contains("PARTY"),"The retained-party reading view uses its own Close action without a duplicate return timer");
            NativeTarget(view.CloseButton,true);yield return .1f;Check(!ArchiveLearningRecapUI.IsVisible,"The detached review can be dismissed after the scene changes");
            detachedSource.reviewFeedback=detachedFeedback;detachedSource=null;SceneManager.SetActiveScene(originalScene);session.gameObject.SetActive(ownerActive);ownerDisabled=false;
            if(madeTemporaryScene){var unload=SceneManager.UnloadSceneAsync(temporaryScene);yield return Wait(()=>unload==null||unload.isDone,"Temporary scene unloads");madeTemporaryScene=false;}
        }
        static void MoveBack()
        {
            if(player==null)return;var cc=player.GetComponent<CharacterController>();bool enabled=cc!=null&&cc.enabled;
            if(enabled)cc.enabled=false;Vector3 delta=position-player.transform.position;player.transform.SetPositionAndRotation(position,rotation);player.ResetMotionAfterTeleport();
            if(enabled)cc.enabled=true;Physics.SyncTransforms();TeleportCameraSync.ResetAfterMove(player.transform,delta);
        }
        static void Finish(bool success)
        {
            active=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;stack.Clear();
            try
            {
                if(detachedSource!=null)detachedSource.reviewFeedback=detachedFeedback;detachedSource=null;
                if(originalScene.IsValid()&&originalScene.isLoaded)SceneManager.SetActiveScene(originalScene);
                if(ownerDisabled&&session!=null)session.gameObject.SetActive(ownerActive);ownerDisabled=false;
                if(madeTemporaryScene&&temporaryScene.IsValid()&&temporaryScene.isLoaded)SceneManager.UnloadSceneAsync(temporaryScene);madeTemporaryScene=false;
                if(Application.isPlaying&&session!=null)
                {
                    ArchiveLearningRecapUI.Current?.CloseReview();session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                    session.hunting.health.ApplyActorHealthSnapshot(health);MoveBack();session.suppressDefeatForVerification=oldDefeat;session.worldProgression.suppressThreatsForVerification=oldThreats;
                    GameInput.Instance.SetGameplayInputBlocked(oldInput);Time.timeScale=scale;
                }
                SelectSize(SessionState.GetInt(SizeKey,7));
                typeof(ArchiveLearningRecapUI).GetField("snapshot",Hidden).SetValue(null,previousReview);
                typeof(ArchiveLearningRecapUI).GetField("account",Hidden).SetValue(null,previousAccount);
            }
            catch(Exception exception){report.errors.Add("Cleanup: "+exception);}
            foreach(var entry in toggles)if(entry.Key!=null)entry.Key.isOn=entry.Value;
            foreach(var entry in preferences){if(entry.Value.Key)PlayerPrefs.SetInt(entry.Key,entry.Value.Value);else PlayerPrefs.DeleteKey(entry.Key);}PlayerPrefs.Save();
            report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            File.WriteAllText(Folder+"/Play.json",JsonUtility.ToJson(report,true));Debug.Log("Learning recap native checks: "+Status+" ("+report.checks.Count+").");
        }
    }
}
