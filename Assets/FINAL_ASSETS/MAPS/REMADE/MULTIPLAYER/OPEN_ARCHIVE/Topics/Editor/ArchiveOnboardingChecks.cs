using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LogicLegends.OpenArchive.Topics.Editor
{
    /// <summary>Explicit native UI interactions against the saved scene; no fake tutorial rewards or answers.</summary>
    public static class ArchiveOnboardingChecks
    {
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Fresh saved MultiplayerGameplay offline Editor Play. Actual Book/Hand, inventory equip/use and GIVE controls run against existing validators using explicit Admin supplies/practice teammate. Tutorial preferences are restored. Physical Android and remote rendered UI remain unverified.";
            public List<string> checks=new List<string>(),errors=new List<string>();
        }
        static Report report;
        static readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        static readonly Dictionary<string,KeyValuePair<bool,int>> preferences=new Dictionary<string,KeyValuePair<bool,int>>();
        static readonly Dictionary<Toggle,bool> toolToggles=new Dictionary<Toggle,bool>();
        static ArchiveTopicSession session;
        static ArchiveLearningUI ui;
        static ArchiveRewardView items;
        static Player player;
        static Vector3 originalPosition;
        static Quaternion originalRotation;
        static bool active,oldThreats,oldDefeat,oldInput;
        static float oldTimeScale;
        static double next;
        public static string Status{get;private set;}="Idle";
        const string AutoKey="OpenArchive.Learning.v1.AutoTips";
        [MenuItem("Tools/Logic Legends/Open Archive/Topics/Test Contextual Onboarding (Offline Play)")]
        public static void Begin()
        {
            session=ArchiveTopicSession.Current;ui=ArchiveLearningUI.Current;items=ArchiveRewardView.Current;player=Player.LocalInstance;
            if(active||!Application.isPlaying||PhotonNetwork.InRoom||session==null||!session.Ready||ui==null||items==null||player==null||
                session.gameObject.scene.path!="Assets/Scenes/MultiplayerGameplay.unity")throw new InvalidOperationException("Use fresh saved MultiplayerGameplay offline Play.");
            preferences.Clear();var keys=new[]{ArchiveLearningUI.OnboardingPreferenceKey,AutoKey}.Concat(Enumerable.Range(0,10).Select(i=>"OpenArchive.Learning.v1.Seen."+i));
            foreach(string key in keys)preferences[key]=new KeyValuePair<bool,int>(PlayerPrefs.HasKey(key),PlayerPrefs.GetInt(key,0));
            originalPosition=player.transform.position;originalRotation=player.transform.rotation;
            toolToggles.Clear();foreach(var toggle in UnityEngine.Object.FindObjectsByType<Toggle>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(toggle.name=="Show_Hide_Test_Tools"){toolToggles[toggle]=toggle.isOn;toggle.isOn=false;}
            oldThreats=session.worldProgression.suppressThreatsForVerification;oldDefeat=session.suppressDefeatForVerification;
            oldInput=GameInput.Instance.GameplayInputBlocked;oldTimeScale=Time.timeScale;
            Directory.CreateDirectory("Temp/ArchiveLearningScaffold");
            report=new Report();active=true;stack.Clear();stack.Push(Flow());next=0;Status="Preparing first-use UI";
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Application.logMessageReceived+=Log;
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
        static void Log(string message,string trace,LogType kind)
        {if(report!=null&&report.errors.Count<8&&(kind==LogType.Error||kind==LogType.Exception||kind==LogType.Assert))report.errors.Add(message+"\n"+trace);}
        static void PlayState(PlayModeStateChange state){if(active&&state==PlayModeStateChange.ExitingPlayMode){report.errors.Add("Play Mode interrupted.");Finish(false);}}
        static void Check(bool condition,string label){report.checks.Add((condition?"PASS ":"FAIL ")+label);if(!condition)throw new InvalidOperationException(label);}
        static IEnumerator Wait(Func<bool> predicate,string label,float seconds=8)
        {double end=EditorApplication.timeSinceStartup+seconds;while(!predicate()){if(EditorApplication.timeSinceStartup>end)throw new TimeoutException(label);yield return .025f;}}
        static void Move(Player target,Vector3 position)
        {
            var cc=target.GetComponent<CharacterController>();bool enabled=cc!=null&&cc.enabled;
            if(enabled)cc.enabled=false;Vector3 delta=position-target.transform.position;target.transform.position=position;target.ResetMotionAfterTeleport();if(enabled)cc.enabled=true;
            Physics.SyncTransforms();if(target==Player.LocalInstance)TeleportCameraSync.ResetAfterMove(target.transform,delta);
        }
        static void Click(Button button)
        {
            Check(button!=null&&button.isActiveAndEnabled&&button.interactable,"The requested existing gameplay button is visible and enabled");
            var rect=(RectTransform)button.transform;
            var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Check(hits.Count>0&&(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform)),"The tutorial does not cover or intercept "+button.name+"; top hit: "+(hits.Count==0?"none":hits[0].gameObject.name+" under "+hits[0].gameObject.transform.parent?.name));
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        static bool InSafeArea(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);Rect safe=Screen.safeArea;
            return corners.All(p=>p.x>=safe.xMin-1&&p.x<=safe.xMax+1&&p.y>=safe.yMin-1&&p.y<=safe.yMax+1);
        }
        static IEnumerator Flow()
        {
            session.suppressDefeatForVerification=session.worldProgression.suppressThreatsForVerification=true;
            session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
            session.RequestAdminTest(ArchiveAdminTestAction.StartMatchClock);session.hunting.Request(Hunting.KhezuCommand.Sleep);
            PlayerPrefs.SetInt(AutoKey,1);ui.ResetOnboardingForQA();ArchiveTopicUI.Current.Close();ui.DismissGuide();
            var station=session.stations[0];ArchiveTopicPlayChecks.MoveTo(station,ArchiveTopicStation.BookPosition+Vector3.back*1.7f);
            yield return Wait(()=>ui.OnboardingVisible&&ui.OnboardingStep==ArchiveOnboardingStep.Book,"Book prompt appears in the real arena");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningScaffold/Book.png");yield return .1f;
            int revision=session.State.revision,mistakes=session.State.mistakes.Count;double matchDeadline=session.State.matchTime.deadline;
            int hearts=session.hunting.health.ActorHearts(session.LocalActor);string work=JsonUtility.ToJson(session.State.stations[0]);
            Check(ui.OnboardingWorldTarget!=null&&ui.OnboardingHighlightTarget!=null,"Book tip points at the world Book and actual Hand control");
            var outline=ui.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Control_Tip_Target_Outline");
            Check(Vector2.Distance(outline.position,ui.OnboardingHighlightTarget.TransformPoint(ui.OnboardingHighlightTarget.rect.center))<2,"The passive outline is centered on the real Hand button");
            Check(InSafeArea(ui.OnboardingCard),"The compact tutorial card fits the current native safe area");
            Check(!player.IsMovementLocked&&!GameInput.Instance.GameplayInputBlocked,"Showing a tutorial neither locks movement nor blocks gameplay input");
            yield return .15f;
            Check(session.State.revision==revision&&session.State.mistakes.Count==mistakes&&session.State.matchTime.deadline==matchDeadline&&JsonUtility.ToJson(session.State.stations[0])==work,
                "Viewing the Book cue never starts a topic, changes answers, creates mistakes or resets clocks");
            yield return Wait(()=>ArchiveTopicUI.Current.FocusedPoint!=null&&ArchiveTopicUI.Current.FocusedPoint.role==ArchivePointRole.Book,"Existing Book focus becomes ready");
            var hand=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.name=="InteractButton");Click(hand);
            yield return Wait(()=>ArchiveTopicUI.Current.ReadingStation==station&&ui.IsOnboardingComplete(ArchiveOnboardingStep.Book),"Real Hand action opens the Book and acknowledges only the local Book lesson");
            Check(!ui.OnboardingVisible&&session.State.stations[0].timerRunning&&session.State.stations[0].phase==1,"The real Book activation starts the original topic clock and the open reader suppresses the tip");
            double topicDeadline=session.State.stations[0].timerDeadline;
            ArchiveTopicUI.Current.Close();ui.OpenGuide(0);yield return .1f;
            Check(ui.GuideVisible&&!ui.OnboardingVisible,"The learning guide and contextual tip do not cover each other");ui.DismissGuide();

            Status="Native equip and safe flashlight use";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.GrantAllItems,-1),"Existing Admin Lab supplies create an explicitly excluded practice fixture");
            int medicineBefore=session.CountItem(-1,ArchiveSurvivalItem.Medkit);
            yield return Wait(()=>ui.OnboardingVisible&&ui.OnboardingStep==ArchiveOnboardingStep.Equip,"Owned inventory exposes the equip tip");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningScaffold/Equip.png");yield return .1f;
            int slot=Array.FindIndex(Enumerable.Range(0,6).ToArray(),i=>ui.OnboardingHighlightTarget==(RectTransform)items.HotbarButton(i).transform);
            Check(slot>=0,"Equip guidance highlights an actual owned item slot");
            // The flashlight is a harmless real first use; no medicine or dart is spent.
            Click(items.HotbarButton(0));yield return Wait(()=>ui.IsOnboardingComplete(ArchiveOnboardingStep.Equip)&&ui.OnboardingVisible&&ui.OnboardingStep==ArchiveOnboardingStep.Use,"Accepted local equip advances to the actual item action");
            Check(ui.OnboardingHighlightTarget==items.MobileItemActionRect,"Use guidance highlights the existing circular item action");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningScaffold/Use.png");yield return .1f;
            Check(Vector2.Distance(outline.position,items.MobileItemActionRect.TransformPoint(items.MobileItemActionRect.rect.center))<2,"The outline tracks the circular item button across different canvases");
            Check(InSafeArea(ui.OnboardingCard),"Item guidance remains inside the safe area beside the hotbar");
            Click(items.UseEquippedButton);yield return Wait(()=>session.Inventory(-1).flashlightOn&&ui.IsOnboardingComplete(ArchiveOnboardingStep.Use),"Accepted flashlight activation completes first use");
            yield return Wait(()=>ArchiveTopicSession.Clock>=session.Inventory(-1).nextUseAt&&items.UseEquippedButton.interactable,"Original item cooldown and its visible button recover before toggling the light back off");
            Click(items.UseEquippedButton);yield return Wait(()=>!session.Inventory(-1).flashlightOn,"The second valid native action turns the flashlight off");
            Check(!session.Inventory(-1).flashlightOn&&session.CountItem(-1,ArchiveSurvivalItem.Medkit)==medicineBefore,"The native use check turns the light back off without spending medicine");

            Status="Native teammate GIVE and persistence";
            Check(session.RequestAdminTest(ArchiveAdminTestAction.SpawnTeammate),"The existing practice teammate is available");
            var friend=session.ActorPlayer(-2);Move(friend,player.transform.position+player.transform.right*3);Click(items.HotbarButton(2));
            yield return .2f;items.SelectTeamItemTarget(-2);
            yield return Wait(()=>ui.OnboardingVisible&&ui.OnboardingStep==ArchiveOnboardingStep.Share&&items.GiveItemButton.interactable,"An eligible nearby teammate exposes a real share action");
            Check(ui.OnboardingHighlightTarget==(RectTransform)items.GiveItemButton.transform,"Sharing guidance points to the actual GIVE button");
            ScreenCapture.CaptureScreenshot("Temp/ArchiveLearningScaffold/Share.png");yield return .1f;
            Move(friend,player.transform.position+player.transform.right*30);
            Check(!session.RequestGiveEquippedItem(-2),"The existing validator rejects a distant transfer");yield return .1f;
            Check(!ui.IsOnboardingComplete(ArchiveOnboardingStep.Share),"A rejected request never acknowledges the sharing lesson");
            Move(friend,player.transform.position+player.transform.right*3);
            yield return .3f; // The existing nearby-recipient UI refreshes on its own cadence.
            items.SelectTeamItemTarget(-2);
            yield return Wait(()=>ui.OnboardingVisible&&ui.OnboardingStep==ArchiveOnboardingStep.Share&&items.GiveItemButton.interactable,"Valid sharing guidance returns when the teammate is nearby");
            int own=session.CountItem(-1,ArchiveSurvivalItem.Medkit),other=session.CountItem(-2,ArchiveSurvivalItem.Medkit);Click(items.GiveItemButton);
            yield return Wait(()=>ui.IsOnboardingComplete(ArchiveOnboardingStep.Share),"Only an accepted local item transfer completes sharing guidance");
            Check(session.CountItem(-1,ArchiveSurvivalItem.Medkit)==own-1&&session.CountItem(-2,ArchiveSurvivalItem.Medkit)==other+1,"GIVE transfers one real item without duplication");
            Check(session.State.mistakes.Count==mistakes&&session.hunting.health.ActorHearts(-1)==hearts&&session.State.stations[0].phase==1&&session.State.stations.Skip(1).All(s=>s.phase==0)&&session.State.stations[0].timerDeadline==topicDeadline&&session.State.matchTime.deadline==matchDeadline,
                "All native tutorial actions preserve academic work, hearts, mistakes and the shared deadline");
            Check(Enum.GetValues(typeof(ArchiveOnboardingStep)).Cast<ArchiveOnboardingStep>().Where(s=>s!=ArchiveOnboardingStep.None).All(ui.IsOnboardingComplete),"Each observed mechanic is remembered locally");
            yield return .1f;Check(!ui.OnboardingVisible,"Completed controls stop prompting automatically");

            Status="Replay, skip and quiet-state cleanup";
            var state=JsonUtility.ToJson(session.State);ui.ReplayOnboarding();
            Check(!ui.IsOnboardingComplete(ArchiveOnboardingStep.Book)&&JsonUtility.ToJson(session.State)==state,"Replay resets only local tutorial preferences");
            yield return Wait(()=>ui.OnboardingVisible,"Replay presents the next relevant tip");
            var step=ui.OnboardingStep;ui.SkipOnboardingStep();Check(ui.IsOnboardingComplete(step),"Skip remembers the current mechanic without demanding an action");
            ui.DismissOnboarding();yield return .1f;Check(!ui.OnboardingVisible&&ui.OnboardingSkippedThisMatch,"Dismiss keeps the remaining tips quiet for this match");
            ui.ReplayOnboarding();yield return Wait(()=>ui.OnboardingVisible,"Replay remains available after dismissal");
            PlayerPrefs.SetInt(AutoKey,0);yield return .1f;Check(!ui.OnboardingVisible,"Existing First-visit tips OFF preference suppresses contextual prompts");
            PlayerPrefs.SetInt(AutoKey,1);yield return Wait(()=>ui.OnboardingVisible,"Enabling first-visit tips restores eligible guidance");
            GameInput.Instance.SetGameplayInputBlocked(true);yield return .1f;Check(!ui.OnboardingVisible,"Gameplay input blocking suppresses tutorial highlights");
            GameInput.Instance.SetGameplayInputBlocked(false);yield return Wait(()=>ui.OnboardingVisible,"Normal input restores the contextual tip");
            Time.timeScale=0;yield return .1f;Check(!ui.OnboardingVisible,"Paused gameplay hides transient tutorial guidance");Time.timeScale=oldTimeScale;
        }
        static void Finish(bool success)
        {
            active=false;EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayState;Application.logMessageReceived-=Log;
            stack.Clear();
            try
            {
                Time.timeScale=oldTimeScale;
                if(Application.isPlaying&&session!=null)
                {
                    GameInput.Instance.SetGameplayInputBlocked(oldInput);ui.DismissOnboarding();ui.DismissGuide();ArchiveTopicUI.Current.Close();
                    session.RequestAdminTest(ArchiveAdminTestAction.ResetMatch);session.RequestAdminTest(ArchiveAdminTestAction.RemoveTeammate);
                    Move(player,originalPosition);player.transform.rotation=originalRotation;session.suppressDefeatForVerification=oldDefeat;session.worldProgression.suppressThreatsForVerification=oldThreats;
                }
            }
            catch(Exception exception){report.errors.Add("Cleanup: "+exception.Message);}
            foreach(var entry in toolToggles)if(entry.Key!=null)entry.Key.isOn=entry.Value;
            foreach(var entry in preferences){if(entry.Value.Key)PlayerPrefs.SetInt(entry.Key,entry.Value.Value);else PlayerPrefs.DeleteKey(entry.Key);}PlayerPrefs.Save();
            report.passed=success&&report.errors.Count==0;Status=report.passed?"Passed":"Failed";
            Directory.CreateDirectory("Temp/ArchiveLearningScaffold");File.WriteAllText("Temp/ArchiveLearningScaffold/Onboarding.json",JsonUtility.ToJson(report,true));
            Debug.Log("Contextual onboarding checks: "+Status+" ("+report.checks.Count+").");
        }
    }
}
